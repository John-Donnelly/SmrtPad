using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Xunit;
using SmrtPad.Helpers;

namespace SmrtPad.Tests
{
    public class LocalizationTests
    {
        private static readonly string? ReswPath = FindReswPath();

        private static string? FindReswPath()
        {
            string? dir = AppContext.BaseDirectory;
            for (int i = 0; i < 8 && dir is not null; i++)
            {
                string candidate = Path.Combine(dir, "Strings", "en-US", "Resources.resw");
                if (!File.Exists(candidate))
                    candidate = Path.Combine(dir, "SmrtPad", "Strings", "en-US", "Resources.resw");
                if (File.Exists(candidate))
                    return candidate;
                dir = Directory.GetParent(dir)?.FullName;
            }
            return null;
        }

        private static Dictionary<string, string> LoadResw()
        {
            Assert.NotNull(ReswPath);
            var dict = new Dictionary<string, string>(StringComparer.Ordinal);
            var doc = XDocument.Load(ReswPath!);
            foreach (var data in doc.Descendants("data"))
            {
                string? name = data.Attribute("name")?.Value;
                string? val = data.Element("value")?.Value;
                if (name is not null && val is not null)
                    dict[name] = val;
            }
            return dict;
        }

        [Fact]
        public void ReswFile_Exists()
        {
            Assert.NotNull(ReswPath);
            Assert.True(File.Exists(ReswPath));
        }

        [Fact]
        public void ReswFile_IsValidXml()
        {
            Assert.NotNull(ReswPath);
            var doc = XDocument.Load(ReswPath!);
            Assert.NotNull(doc.Root);
        }

        [Fact]
        public void ReswFile_ContainsExpectedEntries()
        {
            var dict = LoadResw();
            Assert.True(dict.Count > 100, $"Expected 100+ entries, found {dict.Count}");
        }

        [Theory]
        [InlineData("DocumentUntitled", "Untitled")]
        [InlineData("StatusReady", "Ready")]
        [InlineData("StatusNewDocument", "New document created.")]
        [InlineData("ErrorOpeningFile", "Error Opening File")]
        [InlineData("ErrorSavingFile", "Error Saving File")]
        [InlineData("ButtonSave", "Save")]
        [InlineData("ButtonCancel", "Cancel")]
        [InlineData("DlgUnsavedChanges", "Unsaved Changes")]
        [InlineData("BackstageFile", "File")]
        public void ReswFile_ContainsKey_WithExpectedValue(string key, string expectedValue)
        {
            var dict = LoadResw();
            Assert.True(dict.ContainsKey(key), $"Missing key: {key}");
            Assert.Equal(expectedValue, dict[key]);
        }

        [Theory]
        [InlineData("StatusOpened")]
        [InlineData("StatusSaved")]
        [InlineData("StatusAutoSaved")]
        [InlineData("StatusBarWords")]
        [InlineData("StatusBarCharacters")]
        [InlineData("StatusBarLineCol")]
        [InlineData("StatusBarSelection")]
        [InlineData("DlgSaveChangesMessage")]
        [InlineData("PrintDocumentLabel")]
        [InlineData("StatusInsertedTable")]
        [InlineData("AppTitle")]
        public void ReswFile_FormatStrings_ContainPlaceholders(string key)
        {
            var dict = LoadResw();
            Assert.True(dict.ContainsKey(key), $"Missing key: {key}");
            Assert.Contains("{0}", dict[key]);
        }

        [Theory]
        [InlineData("FileMenuButton.Content")]
        [InlineData("EditMenu.Title")]
        [InlineData("ViewMenu.Title")]
        [InlineData("CutMenuItem.Text")]
        [InlineData("CopyMenuItem.Text")]
        [InlineData("PasteMenuItem.Text")]
        [InlineData("ClipboardGroupLabel.Text")]
        [InlineData("FontGroupLabel.Text")]
        [InlineData("ParagraphGroupLabel.Text")]
        [InlineData("InsertGroupLabel.Text")]
        [InlineData("EditingGroupLabel.Text")]
        [InlineData("FindMatchCaseCheckBox.Content")]
        [InlineData("FindWholeWordCheckBox.Content")]
        [InlineData("NewNavItem.Content")]
        [InlineData("OpenNavItem.Content")]
        [InlineData("SaveNavItem.Content")]
        [InlineData("ExitNavItem.Content")]
        public void ReswFile_ContainsXamlUidEntries(string key)
        {
            var dict = LoadResw();
            Assert.True(dict.ContainsKey(key), $"Missing x:Uid key: {key}");
            Assert.False(string.IsNullOrWhiteSpace(dict[key]), $"Empty value for x:Uid key: {key}");
        }

        [Fact]
        public void ReswFile_NoDuplicateKeys()
        {
            Assert.NotNull(ReswPath);
            var doc = XDocument.Load(ReswPath!);
            var keys = doc.Descendants("data")
                .Select(d => d.Attribute("name")?.Value)
                .Where(n => n is not null)
                .ToList();

            var duplicates = keys.GroupBy(k => k)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key)
                .ToList();

            Assert.Empty(duplicates);
        }

        [Fact]
        public void ReswFile_NoEmptyValues()
        {
            Assert.NotNull(ReswPath);
            var doc = XDocument.Load(ReswPath!);
            var emptyEntries = doc.Descendants("data")
                .Where(d => string.IsNullOrWhiteSpace(d.Element("value")?.Value))
                .Select(d => d.Attribute("name")?.Value)
                .ToList();

            Assert.Empty(emptyEntries);
        }

        [Fact]
        public void ResourceHelper_GetString_ReturnsValue()
        {
            string result = ResourceHelper.GetString("DocumentUntitled");
            Assert.Equal("Untitled", result);
        }

        [Fact]
        public void ResourceHelper_GetString_UnknownKey_ReturnsKeyName()
        {
            string result = ResourceHelper.GetString("NonExistentKey_12345");
            Assert.Equal("NonExistentKey_12345", result);
        }

        [Fact]
        public void ResourceHelper_GetFormatted_FormatsCorrectly()
        {
            string result = ResourceHelper.GetFormatted("StatusOpened", "test.rtf");
            Assert.Equal("Opened test.rtf", result);
        }

        [Fact]
        public void ResourceHelper_GetFormatted_MultipleArgs()
        {
            string result = ResourceHelper.GetFormatted("StatusBarLineCol", 5, 12);
            Assert.Equal("Ln 5, Col 12", result);
        }

        [Fact]
        public void ResourceHelper_GetFormatted_StatusInsertedTable()
        {
            string result = ResourceHelper.GetFormatted("StatusInsertedTable", 3, 4);
            Assert.Contains("3", result);
            Assert.Contains("4", result);
        }

        [Fact]
        public void EditorViewModel_UsesLocalizedDefaults()
        {
            var vm = new ViewModels.EditorViewModel();
            Assert.Equal("Untitled", vm.DocumentTitle);
            Assert.Equal("Ready", vm.StatusMessage);
        }

        [Fact]
        public void EditorViewModel_NewDocument_UsesLocalizedStrings()
        {
            var vm = new ViewModels.EditorViewModel();
            vm.DocumentTitle = "SomeFile.rtf";
            vm.StatusMessage = "Something";

            vm.NewDocument();

            Assert.Equal("Untitled", vm.DocumentTitle);
            Assert.Equal("New document created.", vm.StatusMessage);
        }

        // ── Satellite locale parity tests ──

        private static readonly string[] SatelliteLocales =
            ["de-DE", "es-ES", "fr-FR", "ja-JP", "zh-Hans"];

        private static string? FindStringsRoot()
        {
            string? dir = AppContext.BaseDirectory;
            for (int i = 0; i < 8 && dir is not null; i++)
            {
                string candidate = Path.Combine(dir, "Strings");
                if (!Directory.Exists(candidate))
                    candidate = Path.Combine(dir, "SmrtPad", "Strings");
                if (Directory.Exists(candidate))
                    return candidate;
                dir = Directory.GetParent(dir)?.FullName;
            }
            return null;
        }

        private static Dictionary<string, string> LoadReswFrom(string path)
        {
            var dict = new Dictionary<string, string>(StringComparer.Ordinal);
            var doc = XDocument.Load(path);
            foreach (var data in doc.Descendants("data"))
            {
                string? name = data.Attribute("name")?.Value;
                string? val = data.Element("value")?.Value;
                if (name is not null && val is not null)
                    dict[name] = val;
            }
            return dict;
        }

        [Theory]
        [InlineData("de-DE")]
        [InlineData("es-ES")]
        [InlineData("fr-FR")]
        [InlineData("ja-JP")]
        [InlineData("zh-Hans")]
        public void SatelliteResw_Exists(string locale)
        {
            var root = FindStringsRoot();
            Assert.NotNull(root);
            string path = Path.Combine(root!, locale, "Resources.resw");
            Assert.True(File.Exists(path), $"Missing {locale}/Resources.resw");
        }

        [Theory]
        [InlineData("de-DE")]
        [InlineData("es-ES")]
        [InlineData("fr-FR")]
        [InlineData("ja-JP")]
        [InlineData("zh-Hans")]
        public void SatelliteResw_IsValidXml(string locale)
        {
            var root = FindStringsRoot();
            Assert.NotNull(root);
            string path = Path.Combine(root!, locale, "Resources.resw");
            var doc = XDocument.Load(path);
            Assert.NotNull(doc.Root);
        }

        [Theory]
        [InlineData("de-DE")]
        [InlineData("es-ES")]
        [InlineData("fr-FR")]
        [InlineData("ja-JP")]
        [InlineData("zh-Hans")]
        public void SatelliteResw_HasAllEnUsKeys(string locale)
        {
            var root = FindStringsRoot();
            Assert.NotNull(root);
            var enUs = LoadResw();
            string path = Path.Combine(root!, locale, "Resources.resw");
            var satellite = LoadReswFrom(path);

            var missing = enUs.Keys.Except(satellite.Keys).ToList();
            Assert.True(missing.Count == 0,
                $"{locale} is missing keys: {string.Join(", ", missing)}");
        }

        [Theory]
        [InlineData("de-DE")]
        [InlineData("es-ES")]
        [InlineData("fr-FR")]
        [InlineData("ja-JP")]
        [InlineData("zh-Hans")]
        public void SatelliteResw_HasNoExtraKeys(string locale)
        {
            var root = FindStringsRoot();
            Assert.NotNull(root);
            var enUs = LoadResw();
            string path = Path.Combine(root!, locale, "Resources.resw");
            var satellite = LoadReswFrom(path);

            var extra = satellite.Keys.Except(enUs.Keys).ToList();
            Assert.True(extra.Count == 0,
                $"{locale} has extra keys: {string.Join(", ", extra)}");
        }

        [Theory]
        [InlineData("de-DE")]
        [InlineData("es-ES")]
        [InlineData("fr-FR")]
        [InlineData("ja-JP")]
        [InlineData("zh-Hans")]
        public void SatelliteResw_NoEmptyValues(string locale)
        {
            var root = FindStringsRoot();
            Assert.NotNull(root);
            string path = Path.Combine(root!, locale, "Resources.resw");
            var satellite = LoadReswFrom(path);

            var empty = satellite.Where(kv => string.IsNullOrWhiteSpace(kv.Value))
                                 .Select(kv => kv.Key).ToList();
            Assert.True(empty.Count == 0,
                $"{locale} has empty values: {string.Join(", ", empty)}");
        }

        [Theory]
        [InlineData("de-DE")]
        [InlineData("es-ES")]
        [InlineData("fr-FR")]
        [InlineData("ja-JP")]
        [InlineData("zh-Hans")]
        public void SatelliteResw_FormatPlaceholders_Match(string locale)
        {
            var root = FindStringsRoot();
            Assert.NotNull(root);
            var enUs = LoadResw();
            string path = Path.Combine(root!, locale, "Resources.resw");
            var satellite = LoadReswFrom(path);

            // Check that format strings preserve {0}, {1}, etc.
            var formatKeys = enUs.Where(kv => kv.Value.Contains("{0}"))
                                 .Select(kv => kv.Key);

            foreach (var key in formatKeys)
            {
                Assert.True(satellite.ContainsKey(key), $"{locale} missing format key: {key}");

                // Count placeholders in en-US
                int enCount = 0;
                for (int i = 0; i < 10; i++)
                    if (enUs[key].Contains($"{{{i}}}")) enCount++;

                // Same count in satellite
                int satCount = 0;
                for (int i = 0; i < 10; i++)
                    if (satellite[key].Contains($"{{{i}}}")) satCount++;

                Assert.Equal(enCount, satCount);
            }
        }

        [Theory]
        [InlineData("de-DE")]
        [InlineData("es-ES")]
        [InlineData("fr-FR")]
        [InlineData("ja-JP")]
        [InlineData("zh-Hans")]
        public void SatelliteResw_NoDuplicateKeys(string locale)
        {
            var root = FindStringsRoot();
            Assert.NotNull(root);
            string path = Path.Combine(root!, locale, "Resources.resw");
            var doc = XDocument.Load(path);
            var keys = doc.Descendants("data")
                          .Select(d => d.Attribute("name")?.Value)
                          .Where(n => n is not null)
                          .ToList();
            var duplicates = keys.GroupBy(k => k)
                                 .Where(g => g.Count() > 1)
                                 .Select(g => g.Key)
                                 .ToList();
            Assert.True(duplicates.Count == 0,
                $"{locale} has duplicate keys: {string.Join(", ", duplicates!)}");
        }
    }
}
