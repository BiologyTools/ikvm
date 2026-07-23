using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;

using FluentAssertions;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace IKVM.MSBuild.Tasks.Tests
{

    [TestClass]
    public class IkvmFileIdentityUtilTests
    {

        [TestMethod]
        public async Task CanSaveState()
        {
            var u = new IkvmFileIdentityUtil(new IkvmAssemblyInfoUtil());

            var f = Path.GetTempFileName();
            File.WriteAllText(f, "TEST");
            var i = await u.GetIdentityForFileAsync(f, null, CancellationToken.None);

            var x = new XElement("Test");
            await u.SaveStateXmlAsync(x);

            x.Should().HaveElement("File").Which.Should().HaveAttribute("Path", f).And.HaveAttribute("Identity", i);
        }

        [TestMethod]
        public async Task SaveStateSkipsMissingFiles()
        {
            var u = new IkvmFileIdentityUtil(new IkvmAssemblyInfoUtil());

            var missing = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            (await u.GetIdentityForFileAsync(missing, null, CancellationToken.None)).Should().BeNull();

            var x = new XElement("Test");
            await u.SaveStateXmlAsync(x);

            x.Elements("File").Should().BeEmpty();
        }

        [TestMethod]
        public async Task CanResolveIdentityAfterFileAppears()
        {
            var u = new IkvmFileIdentityUtil(new IkvmAssemblyInfoUtil());

            var path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            (await u.GetIdentityForFileAsync(path, null, CancellationToken.None)).Should().BeNull();

            try
            {
                File.WriteAllText(path, "TEST");
                (await u.GetIdentityForFileAsync(path, null, CancellationToken.None)).Should().NotBeNullOrWhiteSpace();
            }
            finally
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
        }

    }

}
