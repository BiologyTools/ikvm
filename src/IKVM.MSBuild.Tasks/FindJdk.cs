namespace IKVM.MSBuild.Tasks
{

    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;

    using Microsoft.Build.Framework;
    using Microsoft.Build.Utilities;

    /// <summary>
    /// Finds an installed JDK for the requested major family.
    /// </summary>
    public class FindJdk : Task
    {

        /// <summary>
        /// Requested JDK family, such as 8 or 21.
        /// </summary>
        [Required]
        public string Family { get; set; }

        /// <summary>
        /// Resolved JDK home directory.
        /// </summary>
        [Output]
        public string JdkPath { get; set; }

        /// <summary>
        /// Executes the task.
        /// </summary>
        /// <returns></returns>
        public override bool Execute()
        {
            var requestedFamily = ParseMajorVersion(Family);
            foreach (var candidate in GetCandidates(requestedFamily))
            {
                if (TryNormalizeJdkHome(candidate, out var jdkHome) == false)
                    continue;

                var actualFamily = GetJdkFamily(jdkHome);
                if (requestedFamily != null && actualFamily != null && actualFamily != requestedFamily)
                    continue;

                if (requestedFamily != null && actualFamily == null)
                    continue;

                JdkPath = jdkHome;
                Log.LogMessage(MessageImportance.Low, "Located JDK family {0} at '{1}'.", Family, JdkPath);
                return true;
            }

            Log.LogMessage(MessageImportance.Low, "Unable to locate JDK family {0}.", Family);
            return true;
        }

        IEnumerable<string> GetCandidates(int? requestedFamily)
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var candidate in GetEnvironmentCandidates(requestedFamily))
                if (seen.Add(candidate))
                    yield return candidate;

            foreach (var candidate in GetPathCandidates())
                if (seen.Add(candidate))
                    yield return candidate;

            foreach (var candidate in GetWellKnownLocationCandidates())
                if (seen.Add(candidate))
                    yield return candidate;
        }

        static IEnumerable<string> GetEnvironmentCandidates(int? requestedFamily)
        {
            foreach (var variable in GetEnvironmentVariableNames(requestedFamily))
            {
                var value = Environment.GetEnvironmentVariable(variable);
                if (string.IsNullOrWhiteSpace(value) == false)
                    yield return value;
            }
        }

        static IEnumerable<string> GetEnvironmentVariableNames(int? requestedFamily)
        {
            if (requestedFamily != null)
            {
                yield return $"JDK{requestedFamily}HOME";
                yield return $"JDK{requestedFamily}_HOME";
                yield return $"JDK_{requestedFamily}_HOME";
                yield return $"JAVA{requestedFamily}HOME";
                yield return $"JAVA{requestedFamily}_HOME";
                yield return $"JAVA_{requestedFamily}_HOME";
            }

            yield return "JDK_HOME";
            yield return "JAVA_HOME";
        }

        static IEnumerable<string> GetPathCandidates()
        {
            var path = Environment.GetEnvironmentVariable("PATH");
            if (string.IsNullOrWhiteSpace(path))
                yield break;

            foreach (var entry in path.Split(Path.PathSeparator))
            {
                if (string.IsNullOrWhiteSpace(entry))
                    continue;

                string candidate = null;
                try
                {
                    var binDir = Path.GetFullPath(entry.Trim());
                    var javac = Path.Combine(binDir, IsWindows() ? "javac.exe" : "javac");
                    if (File.Exists(javac) && Directory.GetParent(binDir) is DirectoryInfo parent)
                        candidate = parent.FullName;
                }
                catch (Exception)
                {
                }

                if (string.IsNullOrWhiteSpace(candidate) == false)
                    yield return candidate;
            }
        }

        static IEnumerable<string> GetWellKnownLocationCandidates()
        {
            foreach (var root in GetWellKnownRoots())
            {
                if (string.IsNullOrWhiteSpace(root))
                    continue;

                IEnumerable<string> directories = Array.Empty<string>();
                try
                {
                    if (Directory.Exists(root))
                        directories = Directory.EnumerateDirectories(root);
                }
                catch (Exception)
                {
                }

                foreach (var directory in directories.OrderByDescending(i => i, StringComparer.OrdinalIgnoreCase))
                    yield return directory;
            }
        }

        static IEnumerable<string> GetWellKnownRoots()
        {
            if (IsWindows())
            {
                foreach (var root in GetWindowsRootCandidates())
                    yield return root;

                yield break;
            }

            yield return "/usr/lib/jvm";
            yield return "/usr/java";
            yield return "/opt/java";
            yield return "/opt/homebrew/opt/openjdk";
            yield return "/Library/Java/JavaVirtualMachines";

            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (string.IsNullOrWhiteSpace(home) == false)
                yield return Path.Combine(home, ".sdkman", "candidates", "java");
        }

        static IEnumerable<string> GetWindowsRootCandidates()
        {
            foreach (var specialFolder in new[] { Environment.SpecialFolder.ProgramFiles, Environment.SpecialFolder.ProgramFilesX86, Environment.SpecialFolder.LocalApplicationData })
            {
                var folder = Environment.GetFolderPath(specialFolder);
                if (string.IsNullOrWhiteSpace(folder))
                    continue;

                foreach (var relativePath in new[]
                {
                    "Java",
                    "Eclipse Adoptium",
                    "AdoptOpenJDK",
                    "Amazon Corretto",
                    "BellSoft",
                    "Microsoft",
                    "OpenJDK",
                    "RedHat",
                    "Zulu",
                    Path.Combine("Programs", "Eclipse Adoptium"),
                    Path.Combine("Programs", "Microsoft"),
                })
                {
                    yield return Path.Combine(folder, relativePath);
                }
            }
        }

        static bool TryNormalizeJdkHome(string candidate, out string jdkHome)
        {
            jdkHome = null;
            if (string.IsNullOrWhiteSpace(candidate))
                return false;

            foreach (var path in ExpandCandidate(candidate))
            {
                try
                {
                    var fullPath = Path.GetFullPath(path);
                    if (HasJdkExecutables(fullPath) == false)
                        continue;

                    jdkHome = fullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                    return true;
                }
                catch (Exception)
                {
                }
            }

            return false;
        }

        static IEnumerable<string> ExpandCandidate(string candidate)
        {
            yield return candidate;

            if (Directory.Exists(Path.Combine(candidate, "Contents", "Home")))
                yield return Path.Combine(candidate, "Contents", "Home");
        }

        static bool HasJdkExecutables(string directory)
        {
            return File.Exists(Path.Combine(directory, "bin", IsWindows() ? "javac.exe" : "javac")) &&
                   File.Exists(Path.Combine(directory, "bin", IsWindows() ? "java.exe" : "java"));
        }

        static int? GetJdkFamily(string jdkHome)
        {
            var releaseFile = Path.Combine(jdkHome, "release");
            if (File.Exists(releaseFile))
            {
                foreach (var line in File.ReadLines(releaseFile))
                {
                    if (line.StartsWith("JAVA_VERSION=", StringComparison.Ordinal) == false)
                        continue;

                    var value = line.Substring("JAVA_VERSION=".Length).Trim().Trim('"');
                    return ParseMajorVersion(value);
                }
            }

            return ParseMajorVersion(Path.GetFileName(jdkHome));
        }

        static int? ParseMajorVersion(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return null;

            var text = value.Trim().Trim('"');
            if (text.StartsWith("1.", StringComparison.Ordinal))
                text = text.Substring(2);

            var digits = new string(text.TakeWhile(char.IsDigit).ToArray());
            if (int.TryParse(digits, out var version))
                return version;

            var firstDigitIndex = text.ToList().FindIndex(char.IsDigit);
            if (firstDigitIndex < 0)
                return null;

            digits = new string(text.Substring(firstDigitIndex).TakeWhile(char.IsDigit).ToArray());
            return int.TryParse(digits, out version) ? version : null;
        }

        static bool IsWindows()
        {
            var platform = Environment.OSVersion.Platform;
            return platform == PlatformID.Win32NT || platform == PlatformID.Win32S || platform == PlatformID.Win32Windows || platform == PlatformID.WinCE;
        }

    }

}
