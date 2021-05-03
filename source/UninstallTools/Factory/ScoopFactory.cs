﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using Klocman.Native;
using Klocman.Tools;
using UninstallTools.Factory.InfoAdders;
using UninstallTools.Junk.Confidence;
using UninstallTools.Junk.Containers;
using UninstallTools.Properties;

namespace UninstallTools.Factory
{
    public sealed class ScoopFactory : IIndependantUninstallerFactory
    {
        private static bool? _scoopIsAvailable;
        private static string _scoopUserPath;
        private static string _scoopGlobalPath;
        private static string _scriptPath;

        private static bool ScoopIsAvailable
        {
            get
            {
                if (!_scoopIsAvailable.HasValue)
                {
                    _scoopIsAvailable = false;
                    GetScoopInfo();
                }
                return _scoopIsAvailable.Value;
            }
        }

        private static void GetScoopInfo()
        {
            try
            {
                _scoopUserPath = Environment.GetEnvironmentVariable("SCOOP");
                if (string.IsNullOrEmpty(_scoopUserPath))
                    _scoopUserPath = Path.Combine(WindowsTools.GetEnvironmentPath(CSIDL.CSIDL_PROFILE), "scoop");

                _scoopGlobalPath = Environment.GetEnvironmentVariable("SCOOP_GLOBAL");
                if (string.IsNullOrEmpty(_scoopGlobalPath))
                    _scoopGlobalPath = Path.Combine(WindowsTools.GetEnvironmentPath(CSIDL.CSIDL_COMMON_APPDATA), "scoop");

                _scriptPath = Path.Combine(_scoopUserPath, "shims\\scoop.ps1");

                if (File.Exists(_scriptPath))
                {
                    if (!File.Exists(PathTools.GetFullPathOfExecutable("powershell.exe")))
                        throw new InvalidOperationException(@"Detected Scoop program installer, but failed to detect PowerShell");

                    _scoopIsAvailable = true;
                }
            }
            catch (SystemException ex)
            {
                Console.WriteLine(ex);
            }
        }

        // TODO read the app manifests for more info, requires json parsing - var manifest = Path.Combine(installDir, "current\\manifest.json");
        public IList<ApplicationUninstallerEntry> GetUninstallerEntries(ListGenerationProgress.ListGenerationCallback progressCallback)
        {
            var results = new List<ApplicationUninstallerEntry>();
            if (!ScoopIsAvailable) return results;

            // Make uninstaller for scoop itself
            var scoopEntry = new ApplicationUninstallerEntry
            {
                RawDisplayName = "Scoop",
                Comment = "Automated program installer",
                AboutUrl = "https://github.com/lukesampson/scoop",
                InstallLocation = _scoopUserPath
            };

            // Make sure the global directory gets removed as well
            var junk = new FileSystemJunk(new DirectoryInfo(_scoopGlobalPath), scoopEntry, null);
            junk.Confidence.Add(ConfidenceRecords.ExplicitConnection);
            junk.Confidence.Add(4);
            scoopEntry.AdditionalJunk.Add(junk);

            scoopEntry.UninstallString = MakeScoopCommand("uninstall scoop").ToString();
            scoopEntry.UninstallerKind = UninstallerType.PowerShell;
            results.Add(scoopEntry);

            // Make uninstallers for apps installed by scoop
            var result = RunScoopCommand("export");
            if (string.IsNullOrEmpty(result)) return results;

            var appEntries = result.Split(StringTools.NewLineChars.ToArray(), StringSplitOptions.RemoveEmptyEntries);
            var exeSearcher = new AppExecutablesSearcher();
            foreach (var str in appEntries)
            {
                var startIndex = str.IndexOf("(v:", StringComparison.Ordinal);
                var verEndIndex = str.IndexOf(')', startIndex);

                var name = str.Substring(0, startIndex - 1);
                var version = str.Substring(startIndex + 3, verEndIndex - startIndex - 3);
                var isGlobal = str.Substring(verEndIndex).Contains("*global*");

                var entry = new ApplicationUninstallerEntry
                {
                    RawDisplayName = name,
                    DisplayVersion = version,
                    RatingId = "Scoop " + name
                };

                var installDir = Path.Combine(isGlobal ? _scoopGlobalPath : _scoopUserPath, "apps\\" + name);
                if (Directory.Exists(installDir))
                {
                    // Avoid looking for executables in old versions
                    entry.InstallLocation = Path.Combine(installDir, "current");
                    exeSearcher.AddMissingInformation(entry);

                    entry.InstallLocation = installDir;
                }

                entry.UninstallerKind = UninstallerType.PowerShell;
                entry.UninstallString = MakeScoopCommand("uninstall " + name + (isGlobal ? " --global" : "")).ToString();

                results.Add(entry);
            }

            return results;
        }

        public bool IsEnabled() => UninstallToolsGlobalConfig.ScanScoop;
        public string DisplayName => Localisation.Progress_AppStores_Scoop;

        private static string RunScoopCommand(string scoopArgs)
        {
            var startInfo = MakeScoopCommand(scoopArgs).ToProcessStartInfo();
            startInfo.UseShellExecute = false;
            startInfo.RedirectStandardOutput = true;
            startInfo.RedirectStandardError = false;
            startInfo.CreateNoWindow = true;
            startInfo.StandardOutputEncoding = Encoding.Default;

            using (var process = Process.Start(startInfo))
            {
                var sw = Stopwatch.StartNew();
                var output = process?.StandardOutput.ReadToEnd();
                Console.WriteLine($"[Performance] Running command {startInfo.FileName} {startInfo.Arguments} took {sw.ElapsedMilliseconds}ms");
                return output;
            }
        }

        private static ProcessStartCommand MakeScoopCommand(string scoopArgs)
        {
            return new ProcessStartCommand("powershell.exe", $"-ex unrestricted \"{_scriptPath}\" {scoopArgs}");
        }
    }
}