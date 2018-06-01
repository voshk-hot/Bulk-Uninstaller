/*
    Copyright (c) 2017 Marcin Szeniak (https://github.com/Klocman/)
    Apache License Version 2.0
*/

using System;
using System.Diagnostics;
using System.IO;
using Klocman.Extensions;
using Klocman.Tools;

namespace UninstallTools.Factory
{
    public static class ApplicationEntryTools
    {
        /// <summary>
        /// Try to figure out if base uninstaller entry and other entry are pointing to the same application.
        /// Minimum score changes how similar the applications have to be (best use small values, higher is harder)
        /// </summary>
        public static bool AreEntriesRelated(ApplicationUninstallerEntry baseEntry, ApplicationUninstallerEntry otherEntry, int minimumScore = 1)
        {
            //Debug.Assert(!(otherEntry.DisplayName.Contains("vnc", StringComparison.OrdinalIgnoreCase) && 
            //    baseEntry.DisplayName.Contains("vnc", StringComparison.OrdinalIgnoreCase)));

            if (PathTools.PathsEqual(baseEntry.InstallLocation, otherEntry.InstallLocation))
                return true;

            if (!string.IsNullOrEmpty(baseEntry.UninstallString))
            {
                if (PathTools.PathsEqual(baseEntry.UninstallString, otherEntry.UninstallString))
                    return true;

                if (!string.IsNullOrEmpty(otherEntry.InstallLocation) 
                    && baseEntry.UninstallString.Contains(otherEntry.InstallLocation, StringComparison.InvariantCultureIgnoreCase))
                    return true;
            }

            if (!string.IsNullOrEmpty(baseEntry.UninstallerLocation) && !string.IsNullOrEmpty(otherEntry.InstallLocation)
                && baseEntry.UninstallerLocation.StartsWith(otherEntry.InstallLocation, StringComparison.InvariantCultureIgnoreCase))
                return true;

            minimumScore -= 1;
            var score = -1;

            if (!string.IsNullOrEmpty(baseEntry.InstallLocation) && !string.IsNullOrEmpty(otherEntry.InstallLocation))
                AddScore(ref score, -8, 0, -3, baseEntry.InstallLocation.Contains(otherEntry.InstallLocation,
                    StringComparison.InvariantCultureIgnoreCase));

            AddScore(ref score, -5, 0, 3, baseEntry.Is64Bit != MachineType.Unknown && otherEntry.Is64Bit != MachineType.Unknown ?
                baseEntry.Is64Bit == otherEntry.Is64Bit : (bool?)null);
            AddScore(ref score, -3, -1, 5, CompareDates(baseEntry.InstallDate, otherEntry.InstallDate));

            AddScore(ref score, -2, 0, 5, CompareStrings(baseEntry.DisplayVersion, otherEntry.DisplayVersion, true));
            AddScore(ref score, -5, 0, 5, CompareStrings(baseEntry.Publisher, otherEntry.Publisher));

            // Check if base entry was installed from inside other entry's install directory
            if (string.IsNullOrEmpty(baseEntry.InstallLocation) && !string.IsNullOrEmpty(baseEntry.InstallSource) &&
                !string.IsNullOrEmpty(otherEntry.InstallLocation) && otherEntry.InstallLocation.Length >= 5)
            {
                AddScore(ref score, 0, 0, 5, baseEntry.InstallSource.Contains(
                    otherEntry.InstallLocation, StringComparison.InvariantCultureIgnoreCase));
            }

            if (score <= -14 - minimumScore) return false;

            var nameSimilarity = CompareStrings(baseEntry.DisplayName, otherEntry.DisplayName);
            AddScore(ref score, -5, -2, 8, nameSimilarity);
            if (!nameSimilarity.HasValue || nameSimilarity == false)
            {
                var trimmedSimilarity = CompareStrings(baseEntry.DisplayNameTrimmed, otherEntry.DisplayNameTrimmed);
                // Don't risk it if names can't be compared at all
                //if (!trimmedSimilarity.HasValue && !nameSimilarity.HasValue) return false;
                AddScore(ref score, -5, -2, 8, trimmedSimilarity);
            }

            if (score <= -4 - minimumScore) return false;

            try
            {
                AddScore(ref score, -2, -2, 5, CompareStrings(baseEntry.DisplayNameTrimmed.Length < 5 ? baseEntry.DisplayName : baseEntry.DisplayNameTrimmed, Path.GetFileName(otherEntry.InstallLocation)));
            }
            catch (Exception ex)
            {
                Debug.Fail(ex.Message);
            }
            //Debug.Assert(score <= 0);
            return score > minimumScore;
        }

        /// <summary>
        /// Check if dates are very close together, or if they differ by a few hours.
        /// Result is null if the length of the difference can't be compared confidently.
        /// </summary>
        private static bool? CompareDates(DateTime a, DateTime b)
        {
            if (a.IsDefault() || b.IsDefault())
                return null;

            var totalHours = Math.Abs((a - b).TotalHours);

            if (totalHours > 40) return null;

            if (totalHours <= 1)
                return true;

            // One of the dates is lacking time part, so can't be compared
            if (a.TimeOfDay.TotalSeconds < 1 || b.TimeOfDay.TotalSeconds < 1)
                return null;

            return totalHours <= 1;
        }

        private static bool? CompareStrings(string a, string b, bool relaxMatchRequirement = false)
        {
            var lengthRequirement = !relaxMatchRequirement ? 5 : 4;
            if (a == null || (a.Length < lengthRequirement) || b == null || b.Length < lengthRequirement)
                return null;

            if (relaxMatchRequirement)
            {
                if (a.StartsWith(b, StringComparison.Ordinal) || b.StartsWith(a, StringComparison.Ordinal))
                    return true;
            }

            var changesRequired = Sift4.SimplestDistance(a, b, 3);
            return changesRequired == 0 || changesRequired < a.Length / 6;
        }

        private static void AddScore(ref int score, int failScore, int unsureScore, int successScore, bool? testResult)
        {
            if (!testResult.HasValue) score = score + unsureScore;
            else score = testResult.Value ? score + successScore : score + failScore;
        }

        /// <summary>
        ///     Check if path points to the windows installer program or to a .msi package
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public static bool PathPointsToMsiExec(string path)
        {
            if (string.IsNullOrEmpty(path))
                return false;

            return path.ContainsAny(new[] { "msiexec ", "msiexec.exe" }, StringComparison.OrdinalIgnoreCase)
                   || path.EndsWith(".msi", StringComparison.OrdinalIgnoreCase);
        }

        public static string CleanupDisplayVersion(string version)
        {
            return version?.Replace(", ", ".").Replace(". ", ".").Replace(",", ".").Replace(". ", ".").Trim();
        }
    }
}