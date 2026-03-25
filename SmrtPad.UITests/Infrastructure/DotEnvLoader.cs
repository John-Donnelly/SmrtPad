using System;
using System.IO;

namespace SmrtPad.UITests.Infrastructure;

/// <summary>Loads test environment variables from the repo-root .env file when present.</summary>
internal static class DotEnvLoader
{
    private static bool s_loaded;
    private static readonly object SyncRoot = new();

    /// <summary>Loads environment variables from the nearest repo-root .env file.</summary>
    public static void EnsureLoaded()
    {
        lock (SyncRoot)
        {
            if (s_loaded)
                return;

            var envPath = FindDotEnvPath();
            if (!string.IsNullOrWhiteSpace(envPath) && File.Exists(envPath))
            {
                foreach (var rawLine in File.ReadAllLines(envPath))
                {
                    if (string.IsNullOrWhiteSpace(rawLine))
                        continue;

                    var line = rawLine.Trim();
                    if (line.StartsWith("#", StringComparison.Ordinal))
                        continue;

                    var separatorIndex = line.IndexOf('=');
                    if (separatorIndex <= 0)
                        continue;

                    var key = line[..separatorIndex].Trim();
                    var value = line[(separatorIndex + 1)..].Trim();
                    if (string.IsNullOrWhiteSpace(key))
                        continue;

                    if (value.Length >= 2 && ((value.StartsWith('"') && value.EndsWith('"')) || (value.StartsWith('\'') && value.EndsWith('\''))))
                    {
                        value = value[1..^1];
                    }

                    if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(key)))
                    {
                        Environment.SetEnvironmentVariable(key, value);
                    }
                }
            }

            s_loaded = true;
        }
    }

    private static string? FindDotEnvPath()
    {
        var directory = AppContext.BaseDirectory;
        for (var i = 0; i < 10 && !string.IsNullOrWhiteSpace(directory); i++)
        {
            var candidate = Path.Combine(directory, ".env");
            if (File.Exists(candidate))
                return candidate;

            directory = Directory.GetParent(directory)?.FullName;
        }

        return null;
    }
}
