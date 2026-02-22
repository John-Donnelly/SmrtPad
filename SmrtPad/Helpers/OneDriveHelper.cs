using System;
using System.IO;

namespace SmrtPad.Helpers
{
    /// <summary>
    /// Detects the user's OneDrive sync folder path from environment variables.
    /// Works for personal (Consumer) and work/school (Commercial) OneDrive accounts.
    /// </summary>
    public static class OneDriveHelper
    {
        /// <summary>
        /// Returns the OneDrive root folder path when OneDrive is installed and configured,
        /// otherwise <c>null</c>.
        /// </summary>
        public static string? GetOneDrivePath()
        {
            string?[] candidates =
            [
                Environment.GetEnvironmentVariable("OneDriveConsumer"),
                Environment.GetEnvironmentVariable("OneDriveCommercial"),
                Environment.GetEnvironmentVariable("OneDrive"),
            ];

            foreach (var path in candidates)
            {
                if (!string.IsNullOrWhiteSpace(path) && Directory.Exists(path))
                    return path;
            }

            return null;
        }

        /// <summary>Returns <c>true</c> when a valid OneDrive sync folder was found.</summary>
        public static bool IsAvailable() => GetOneDrivePath() != null;
    }
}
