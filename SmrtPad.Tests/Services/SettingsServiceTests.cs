using System;
using System.IO;
using System.Text.Json;
using SmrtPad.Services;
using Xunit;

namespace SmrtPad.Tests.Services
{
    public sealed class SettingsServiceTests
    {
        [Fact]
        public void LanguageAccess_WithInvalidRecentFiles_DoesNotRewriteSettingsFile()
        {
            var existingFile = CreateTempFile();
            var invalidFile = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "missing.rtf");
            var settingsPath = CreateSettingsFile(existingFile, invalidFile);

            var service = new SettingsService(settingsPath);
            _ = service.Language;
            using var persisted = JsonDocument.Parse(File.ReadAllText(settingsPath));
            var recentFiles = persisted.RootElement.GetProperty("RecentFiles").EnumerateArray().Select(static value => value.GetString()).ToArray();

            Assert.Contains(invalidFile, recentFiles, StringComparer.Ordinal);
        }

        [Fact]
        public void RecentFilesAccess_WithInvalidRecentFiles_RemovesInvalidEntries()
        {
            var existingFile = CreateTempFile();
            var invalidFile = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "missing.rtf");
            var settingsPath = CreateSettingsFile(existingFile, invalidFile);

            var service = new SettingsService(settingsPath);

            var recentFiles = service.RecentFiles;

            Assert.Equal([existingFile], recentFiles);
        }

        [Fact]
        public void RecentFilesAccess_WithInvalidRecentFiles_PersistsCleanedList()
        {
            var existingFile = CreateTempFile();
            var invalidFile = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "missing.rtf");
            var settingsPath = CreateSettingsFile(existingFile, invalidFile);

            var service = new SettingsService(settingsPath);
            _ = service.RecentFiles;
            using var persisted = JsonDocument.Parse(File.ReadAllText(settingsPath));
            var recentFiles = persisted.RootElement.GetProperty("RecentFiles").EnumerateArray().Select(static value => value.GetString()).ToArray();

            Assert.DoesNotContain(invalidFile, recentFiles, StringComparer.Ordinal);
        }

        private static string CreateTempFile()
        {
            var path = Path.Combine(Path.GetTempPath(), "SmrtPad.Tests", Guid.NewGuid().ToString("N"), "existing.rtf");
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, "test");
            return path;
        }

        private static string CreateSettingsFile(string existingFile, string invalidFile)
        {
            var directory = Path.Combine(Path.GetTempPath(), "SmrtPad.Tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);

            var path = Path.Combine(directory, "settings.json");
            var json = JsonSerializer.Serialize(new
            {
                Language = "fr-FR",
                RecentFiles = new[] { existingFile, invalidFile }
            });

            File.WriteAllText(path, json);
            return path;
        }
    }
}
