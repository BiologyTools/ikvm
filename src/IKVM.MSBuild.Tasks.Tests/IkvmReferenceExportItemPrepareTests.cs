using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using FluentAssertions;

using IKVM.Tests.Util;

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using Moq;

namespace IKVM.MSBuild.Tasks.Tests
{

    [TestClass]
    public class IkvmReferenceExportItemPrepareTests
    {

        [DataTestMethod]
        [DataRow("net472", ".NETFramework", "4.7.2")]
        [DataRow("net48", ".NETFramework", "4.8")]
        [DataRow("net6.0", ".NET", "6.0")]
        [DataRow("net7.0", ".NET", "7.0")]
        [DataRow("net8.0", ".NET", "8.0")]
        [DataRow("net10.0", ".NET", "10.0")]
        public void CanPrepare(string tfm, string targetFrameworkIdentifier, string targetFrameworkVersion)
        {
            var engine = new Mock<IBuildEngine7>();
            var errors = new List<BuildErrorEventArgs>();
            engine.Setup(x => x.LogErrorEvent(It.IsAny<BuildErrorEventArgs>())).Callback((BuildErrorEventArgs e) => errors.Add(e));

            var a = new List<TaskItem>();
            foreach (var i in DotNetSdkUtil.GetPathToReferenceAssemblies(tfm, targetFrameworkIdentifier, targetFrameworkVersion))
                foreach (var r in Directory.GetFiles(i, "*.dll"))
                    a.Add(new TaskItem(r));

            var t = new IkvmReferenceExportItemPrepare();
            t.BuildEngine = engine.Object;
            t.ToolFramework = tfm;
            t.ToolVersion = "";
            t.StateFile = Path.GetTempFileName();
            t.Items = a.ToArray();
            t.References = a.ToArray();
            t.Execute().Should().BeTrue();
        }

        [TestMethod]
        public void CanPrepareWhenReferencedAssemblyDisappearsAfterPreload()
        {
            var engine = new Mock<IBuildEngine7>();
            var errors = new List<BuildErrorEventArgs>();
            engine.Setup(x => x.LogErrorEvent(It.IsAny<BuildErrorEventArgs>())).Callback((BuildErrorEventArgs e) => errors.Add(e));

            var refsDir = DotNetSdkUtil.GetPathToReferenceAssemblies("net472", ".NETFramework", "4.7.2")[0];
            var itemPath = Path.Combine(refsDir, "System.Collections.dll");
            var referencePath = Path.Combine(refsDir, "mscorlib.dll");
            var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);

            try
            {
                var copiedItemPath = Path.Combine(tempDir, Path.GetFileName(itemPath));
                var copiedReferencePath = Path.Combine(tempDir, Path.GetFileName(referencePath));
                File.Copy(itemPath, copiedItemPath, true);
                File.Copy(referencePath, copiedReferencePath, true);

                var t = new DeleteReferenceAfterPreloadTask(copiedReferencePath)
                {
                    BuildEngine = engine.Object,
                    ToolFramework = "net472",
                    ToolVersion = "",
                    StateFile = Path.Combine(tempDir, "state.cache"),
                    Items = new[] { new TaskItem(copiedItemPath) },
                    References = new[] { new TaskItem(copiedItemPath), new TaskItem(copiedReferencePath) },
                };

                t.Execute().Should().BeTrue();
                errors.Should().BeEmpty();
                File.Exists(copiedReferencePath).Should().BeFalse();
                t.Items.Should().ContainSingle();
                t.Items[0].GetMetadata("IkvmIdentity").Should().NotBeNullOrWhiteSpace();
            }
            finally
            {
                if (Directory.Exists(tempDir))
                    Directory.Delete(tempDir, true);
            }
        }

        sealed class DeleteReferenceAfterPreloadTask : IkvmReferenceExportItemPrepare
        {

            readonly string pathToDelete;

            public DeleteReferenceAfterPreloadTask(string pathToDelete)
            {
                this.pathToDelete = pathToDelete;
            }

            protected override System.Threading.Tasks.Task OnAssembliesPreLoadedAsync(CancellationToken cancellationToken)
            {
                File.Delete(pathToDelete);
                return System.Threading.Tasks.Task.CompletedTask;
            }

        }

    }

}
