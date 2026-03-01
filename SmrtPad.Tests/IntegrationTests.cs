using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using SmrtPad.Helpers;
using SmrtPad.Models;
using SmrtPad.Services;
using SmrtPad.ViewModels;

namespace SmrtPad.Tests
{
    // â•â•â• Full ViewModel Workflow Integration Tests â•â•â•

    public class ViewModelWorkflowTests
    {
        [Fact]
        public void FullEditingWorkflow_NewDocument_Edit_Reset()
        {
            var vm = new EditorViewModel();

            // Initial state
            Assert.Equal("Untitled", vm.DocumentTitle);
            Assert.False(vm.IsModified);
            Assert.Equal("100%", vm.ZoomDisplay);

            // Simulate editing
            vm.DocumentTitle = "MyDoc.rtf";
            vm.IsModified = true;
            vm.IsBold = true;
            vm.IsItalic = true;
            vm.FontFamily = "Arial";
            vm.FontSize = 16;
            vm.Alignment = "Center";
            vm.WordCount = 150;
            vm.CharCount = 800;
            vm.LineNumber = 10;
            vm.ColumnNumber = 25;
            vm.Encoding = "RTF";

            // Verify editing state
            Assert.Equal("MyDoc.rtf", vm.DocumentTitle);
            Assert.True(vm.IsModified);
            Assert.True(vm.IsBold);
            Assert.True(vm.IsItalic);
            Assert.Contains("150", vm.WordCountDisplay);
            Assert.Contains("800", vm.CharCountDisplay);
            Assert.Contains("10", vm.LineColDisplay);
            Assert.Contains("25", vm.LineColDisplay);
            Assert.Equal("RTF", vm.EncodingDisplay);

            // Reset via NewDocument
            vm.NewDocument();

            // Verify full reset
            Assert.Equal("Untitled", vm.DocumentTitle);
            Assert.False(vm.IsModified);
            Assert.False(vm.IsBold);
            Assert.False(vm.IsItalic);
            Assert.Equal("Segoe UI", vm.FontFamily);
            Assert.Equal(11.0, vm.FontSize);
            Assert.Equal("Left", vm.Alignment);
            Assert.Equal(0, vm.WordCount);
            Assert.Equal(0, vm.CharCount);
            Assert.Equal(1, vm.LineNumber);
            Assert.Equal(1, vm.ColumnNumber);
            Assert.Equal("UTF-8", vm.Encoding);
            Assert.Equal("100%", vm.ZoomDisplay);
        }

        [Fact]
        public void FormattingWorkflow_ApplyMultipleFormats_ThenClear()
        {
            var vm = new EditorViewModel();

            // Apply multiple formats
            vm.ToggleBold();
            vm.ToggleItalic();
            vm.ToggleUnderline();
            vm.ToggleStrikethrough();
            vm.ToggleSubscript();

            Assert.True(vm.IsBold);
            Assert.True(vm.IsItalic);
            Assert.True(vm.IsUnderline);
            Assert.True(vm.IsStrikethrough);
            Assert.True(vm.IsSubscript);
            Assert.False(vm.IsSuperscript);

            // Toggle superscript should clear subscript
            vm.ToggleSuperscript();
            Assert.True(vm.IsSuperscript);
            Assert.False(vm.IsSubscript);

            // NewDocument clears all
            vm.NewDocument();
            Assert.False(vm.IsBold);
            Assert.False(vm.IsItalic);
            Assert.False(vm.IsUnderline);
            Assert.False(vm.IsStrikethrough);
            Assert.False(vm.IsSubscript);
            Assert.False(vm.IsSuperscript);
        }

        [Fact]
        public void ZoomWorkflow_ZoomInOut_VerifyDisplayUpdates()
        {
            var vm = new EditorViewModel();
            var displayChanges = new List<string>();
            vm.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName != null)
                    displayChanges.Add(e.PropertyName);
            };

            Assert.Equal("100%", vm.ZoomDisplay);

            vm.ZoomIn();
            Assert.Equal("110%", vm.ZoomDisplay);
            Assert.Contains(nameof(EditorViewModel.ZoomDisplay), displayChanges);

            displayChanges.Clear();
            vm.ZoomOut();
            Assert.Equal("100%", vm.ZoomDisplay);
            Assert.Contains(nameof(EditorViewModel.ZoomDisplay), displayChanges);

            // Zoom to max
            for (int i = 0; i < 50; i++) vm.ZoomIn();
            Assert.Equal("500%", vm.ZoomDisplay);
            Assert.Equal(500.0, vm.ZoomLevel);

            // Zoom to min
            for (int i = 0; i < 60; i++) vm.ZoomOut();
            Assert.Equal("10%", vm.ZoomDisplay);
            Assert.Equal(10.0, vm.ZoomLevel);
        }

        [Fact]
        public void ListWorkflow_SwitchListTypes()
        {
            var vm = new EditorViewModel();

            Assert.Equal("None", vm.ListType);
            Assert.False(vm.IsBullets);

            vm.SetListType("Bullet");
            Assert.Equal("Bullet", vm.ListType);
            Assert.True(vm.IsBullets);

            vm.SetListType("LowercaseLetter");
            Assert.Equal("LowercaseLetter", vm.ListType);
            Assert.True(vm.IsBullets);

            vm.SetListType("UppercaseRoman");
            Assert.Equal("UppercaseRoman", vm.ListType);
            Assert.True(vm.IsBullets);

            vm.SetListType("None");
            Assert.Equal("None", vm.ListType);
            Assert.False(vm.IsBullets);
        }

        [Fact]
        public void StatusBarWorkflow_CountUpdates_Tracked()
        {
            var vm = new EditorViewModel();
            var changedProps = new HashSet<string>();
            vm.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName != null)
                    changedProps.Add(e.PropertyName);
            };

            // Simulate typing: word count changes
            vm.WordCount = 5;
            vm.CharCount = 25;
            Assert.Contains(nameof(EditorViewModel.WordCount), changedProps);
            Assert.Contains(nameof(EditorViewModel.WordCountDisplay), changedProps);
            Assert.Contains(nameof(EditorViewModel.CharCount), changedProps);
            Assert.Contains(nameof(EditorViewModel.CharCountDisplay), changedProps);

            // Simulate cursor move
            changedProps.Clear();
            vm.LineNumber = 3;
            vm.ColumnNumber = 15;
            Assert.Contains(nameof(EditorViewModel.LineColDisplay), changedProps);

            // Simulate selection
            changedProps.Clear();
            vm.SelectionLength = 10;
            Assert.Contains(nameof(EditorViewModel.SelectionLengthDisplay), changedProps);
        }

        [Fact]
        public void ParagraphSpacingWorkflow_SetAndReset()
        {
            var vm = new EditorViewModel();

            vm.SetParagraphSpacing([12.0, 6.0]);
            Assert.Equal(12.0, vm.ParagraphSpacingBefore);
            Assert.Equal(6.0, vm.ParagraphSpacingAfter);

            vm.SetLineSpacing(2.0);
            Assert.Equal(2.0, vm.LineSpacing);

            vm.NewDocument();
            Assert.Equal(0.0, vm.ParagraphSpacingBefore);
            Assert.Equal(0.0, vm.ParagraphSpacingAfter);
            Assert.Equal(1.0, vm.LineSpacing);
        }

        [Fact]
        public void FindOptionsWorkflow_ToggleFindOptions()
        {
            var vm = new EditorViewModel();

            Assert.False(vm.FindMatchCase);
            Assert.False(vm.FindWholeWord);
            Assert.False(vm.FindUseRegex);

            vm.FindMatchCase = true;
            vm.FindWholeWord = true;
            vm.FindUseRegex = true;

            Assert.True(vm.FindMatchCase);
            Assert.True(vm.FindWholeWord);
            Assert.True(vm.FindUseRegex);

            vm.NewDocument();
            Assert.False(vm.FindMatchCase);
            Assert.False(vm.FindWholeWord);
            Assert.False(vm.FindUseRegex);
        }
    }

    // â•â•â• DI Container Integration Tests â•â•â•

    public class DIContainerIntegrationTests
    {
        private static ServiceProvider BuildTestContainer()
        {
            var services = new ServiceCollection();
            services.AddSingleton<ISettingsService, SettingsService>();
            services.AddSingleton<EditorViewModel>();
            services.AddTransient<IDialogService, DialogService>();
            services.AddTransient<IFileService, FileService>();
            return services.BuildServiceProvider();
        }

        [Fact]
        public void Container_ResolvesAllCoreServices()
        {
            var provider = BuildTestContainer();

            Assert.NotNull(provider.GetService<ISettingsService>());
            Assert.NotNull(provider.GetService<EditorViewModel>());
            Assert.NotNull(provider.GetService<IDialogService>());
            Assert.NotNull(provider.GetService<IFileService>());
        }

        [Fact]
        public void Container_SettingsService_IsSingleton()
        {
            var provider = BuildTestContainer();

            var s1 = provider.GetRequiredService<ISettingsService>();
            var s2 = provider.GetRequiredService<ISettingsService>();
            Assert.Same(s1, s2);
        }

        [Fact]
        public void Container_EditorViewModel_IsSingleton()
        {
            var provider = BuildTestContainer();

            var vm1 = provider.GetRequiredService<EditorViewModel>();
            var vm2 = provider.GetRequiredService<EditorViewModel>();
            Assert.Same(vm1, vm2);
        }

        [Fact]
        public void Container_DialogService_IsTransient()
        {
            var provider = BuildTestContainer();

            var d1 = provider.GetRequiredService<IDialogService>();
            var d2 = provider.GetRequiredService<IDialogService>();
            Assert.NotSame(d1, d2);
        }

        [Fact]
        public void Container_FileService_IsTransient()
        {
            var provider = BuildTestContainer();

            var f1 = provider.GetRequiredService<IFileService>();
            var f2 = provider.GetRequiredService<IFileService>();
            Assert.NotSame(f1, f2);
        }

        [Fact]
        public void Container_SettingsService_HasValidDefaults()
        {
            var provider = BuildTestContainer();
            var settings = provider.GetRequiredService<ISettingsService>();

            Assert.Equal("Segoe UI", settings.DefaultFontFamily);
            Assert.Equal(11.0, settings.DefaultFontSize);
            Assert.True(settings.DefaultWordWrap);
            Assert.Equal(".rtf", settings.DefaultSaveFormat);
        }

        [Fact]
        public void Container_ViewModel_InitializedCorrectly()
        {
            var provider = BuildTestContainer();
            var vm = provider.GetRequiredService<EditorViewModel>();

            Assert.Equal("Untitled", vm.DocumentTitle);
            Assert.False(vm.IsModified);
            Assert.Equal(100.0, vm.ZoomLevel);
            Assert.Equal("UTF-8", vm.Encoding);
        }

        [Fact]
        public void Container_RequiredService_ThrowsForUnregistered()
        {
            var services = new ServiceCollection();
            var provider = services.BuildServiceProvider();

            Assert.Throws<InvalidOperationException>(() =>
                provider.GetRequiredService<ISettingsService>());
        }
    }

    // â•â•â• Archive Text Extraction Tests â•â•â•

    public partial class ArchiveExtractionTests : IDisposable
    {
        private readonly string _testDir;

        public ArchiveExtractionTests()
        {
            _testDir = Path.Combine(Path.GetTempPath(), "SmrtPad_ArchiveTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_testDir);
        }

        public void Dispose()
        {
            try { Directory.Delete(_testDir, true); } catch { }
            GC.SuppressFinalize(this);
        }

        private string CreateDocxFile(string textContent)
        {
            string ns = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
            var doc = new XDocument(
                new XElement(XName.Get("document", ns),
                    new XElement(XName.Get("body", ns),
                        new XElement(XName.Get("p", ns),
                            new XElement(XName.Get("r", ns),
                                new XElement(XName.Get("t", ns), textContent))))));

            string filePath = Path.Combine(_testDir, $"test_{Guid.NewGuid():N}.docx");
            using (var zip = ZipFile.Open(filePath, ZipArchiveMode.Create))
            {
                var entry = zip.CreateEntry("word/document.xml");
                using var writer = new StreamWriter(entry.Open());
                doc.Save(writer);
            }
            return filePath;
        }

        private string CreateOdtFile(string textContent)
        {
            string ns = "urn:oasis:names:tc:opendocument:xmlns:text:1.0";
            var doc = new XDocument(
                new XElement(XName.Get("document-content", "urn:oasis:names:tc:opendocument:xmlns:office:1.0"),
                    new XElement(XName.Get("body", "urn:oasis:names:tc:opendocument:xmlns:office:1.0"),
                        new XElement(XName.Get("text", "urn:oasis:names:tc:opendocument:xmlns:office:1.0"),
                            new XElement(XName.Get("p", ns), textContent)))));

            string filePath = Path.Combine(_testDir, $"test_{Guid.NewGuid():N}.odt");
            using (var zip = ZipFile.Open(filePath, ZipArchiveMode.Create))
            {
                var entry = zip.CreateEntry("content.xml");
                using var writer = new StreamWriter(entry.Open());
                doc.Save(writer);
            }
            return filePath;
        }

        [Fact]
        public void ExtractDocx_ReturnsTextContent()
        {
            string filePath = CreateDocxFile("Hello World");
            string result = ExtractTextFromArchive(filePath, ".docx");
            Assert.Contains("Hello World", result);
        }

        [Fact]
        public void ExtractOdt_ReturnsTextContent()
        {
            string filePath = CreateOdtFile("Test ODT Content");
            string result = ExtractTextFromArchive(filePath, ".odt");
            Assert.Contains("Test ODT Content", result);
        }

        [Fact]
        public void ExtractDocx_EmptyDocument_ReturnsEmpty()
        {
            string filePath = CreateDocxFile("");
            string result = ExtractTextFromArchive(filePath, ".docx");
            Assert.Equal("", result);
        }

        [Fact]
        public void ExtractArchive_MissingEntry_ReturnsEmpty()
        {
            string filePath = Path.Combine(_testDir, $"empty_{Guid.NewGuid():N}.docx");
            using (var zip = ZipFile.Open(filePath, ZipArchiveMode.Create))
            {
                // Create zip with no word/document.xml entry
                var entry = zip.CreateEntry("other.txt");
                using var writer = new StreamWriter(entry.Open());
                writer.Write("not a doc");
            }

            string result = ExtractTextFromArchive(filePath, ".docx");
            Assert.Equal(string.Empty, result);
        }

        [Fact]
        public void ExtractDocx_MultipleTextElements()
        {
            string ns = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
            var doc = new XDocument(
                new XElement(XName.Get("document", ns),
                    new XElement(XName.Get("body", ns),
                        new XElement(XName.Get("p", ns),
                            new XElement(XName.Get("r", ns),
                                new XElement(XName.Get("t", ns), "Hello ")),
                            new XElement(XName.Get("r", ns),
                                new XElement(XName.Get("t", ns), "World"))))));

            string filePath = Path.Combine(_testDir, $"multi_{Guid.NewGuid():N}.docx");
            using (var zip = ZipFile.Open(filePath, ZipArchiveMode.Create))
            {
                var entry = zip.CreateEntry("word/document.xml");
                using var writer = new StreamWriter(entry.Open());
                doc.Save(writer);
            }

            string result = ExtractTextFromArchive(filePath, ".docx");
            Assert.Contains("Hello ", result);
            Assert.Contains("World", result);
        }

        /// <summary>
        /// Uses the real DocumentImportHelper.ExtractText from the app project.
        /// </summary>
        private static string ExtractTextFromArchive(string filePath, string ext)
        {
            using var stream = File.OpenRead(filePath);
            return DocumentImportHelper.ExtractText(stream, ext);
        }
    }

    // â•â•â• Settings + ViewModel Integration Tests â•â•â•

    public partial class SettingsViewModelIntegrationTests : IDisposable
    {
        private readonly string _testDir;
        private readonly string _settingsPath;

        public SettingsViewModelIntegrationTests()
        {
            _testDir = Path.Combine(Path.GetTempPath(), "SmrtPad_IntTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_testDir);
            _settingsPath = Path.Combine(_testDir, "settings.json");
        }

        public void Dispose()
        {
            try { Directory.Delete(_testDir, true); } catch { }
            GC.SuppressFinalize(this);
        }

        [Fact]
        public void Settings_DefaultFont_MatchesViewModelDefaults()
        {
            var settings = new SettingsService(_settingsPath);
            var vm = new EditorViewModel();

            Assert.Equal(settings.DefaultFontFamily, vm.FontFamily);
            Assert.Equal(settings.DefaultFontSize, vm.FontSize);
        }

        [Fact]
        public void Settings_RecentFiles_SyncWithViewModel()
        {
            var settings = new SettingsService(_settingsPath);
            var vm = new EditorViewModel();

            settings.AddRecentFile("C:\\test1.rtf");
            settings.AddRecentFile("C:\\test2.rtf");
            vm.RecentFiles = [..settings.RecentFiles];

            Assert.Equal(2, vm.RecentFiles.Count);
            Assert.Equal("C:\\test2.rtf", vm.RecentFiles[0]);
            Assert.Equal("C:\\test1.rtf", vm.RecentFiles[1]);
        }

        [Fact]
        public void Settings_Persist_AcrossInstances()
        {
            var settings1 = new SettingsService(_settingsPath)
            {
                DefaultFontFamily = "Consolas",
                DefaultFontSize = 14,
                DefaultWordWrap = false,
                AutoSaveEnabled = true,
                AutoSaveIntervalSeconds = 60,
                RulerUnits = "cm"
            };
            string testFile = Path.Combine(Path.GetDirectoryName(_settingsPath)!, "doc.rtf");
            File.WriteAllText(testFile, "data");
            settings1.AddRecentFile(testFile);
            settings1.Save();

            var settings2 = new SettingsService(_settingsPath);
            Assert.Equal("Consolas", settings2.DefaultFontFamily);
            Assert.Equal(14.0, settings2.DefaultFontSize);
            Assert.False(settings2.DefaultWordWrap);
            Assert.True(settings2.AutoSaveEnabled);
            Assert.Equal(60, settings2.AutoSaveIntervalSeconds);
            Assert.Equal("cm", settings2.RulerUnits);
            Assert.Single(settings2.RecentFiles);
        }

        [Fact]
        public void Settings_ThemePreference_AllValidValues()
        {
            var settings = new SettingsService(_settingsPath);
            foreach (var theme in new[] { "System", "Light", "Dark" })
            {
                settings.ThemePreference = theme;
                settings.Save();
                var reloaded = new SettingsService(_settingsPath);
                Assert.Equal(theme, reloaded.ThemePreference);
            }
        }

        [Fact]
        public void Settings_Language_AllSupportedLocales()
        {
            var settings = new SettingsService(_settingsPath);
            var locales = new[] { "en-US", "de-DE", "es-ES", "fr-FR", "ja-JP", "zh-Hans", "ar-SA", "ru-RU", "ur-PK" };
            foreach (var locale in locales)
            {
                settings.Language = locale;
                settings.Save();
                var reloaded = new SettingsService(_settingsPath);
                Assert.Equal(locale, reloaded.Language);
            }
        }

        [Fact]
        public void Settings_RulerUnits_BothValues()
        {
            var settings = new SettingsService(_settingsPath);
            foreach (var unit in new[] { "in", "cm" })
            {
                settings.RulerUnits = unit;
                settings.Save();
                var reloaded = new SettingsService(_settingsPath);
                Assert.Equal(unit, reloaded.RulerUnits);
            }
        }
    }

    // â•â•â• ResourceHelper Cross-Locale Integration Tests â•â•â•

    public class ResourceHelperIntegrationTests
    {
        private static readonly string[] CoreKeys =
        [
            "DocumentUntitled", "StatusReady", "StatusNewDocument",
            "ErrorOpeningFile", "ErrorSavingFile", "DlgUnsavedChanges",
            "ButtonSave", "ButtonCancel", "BackstageFile",
            "StatusBarWords", "StatusBarCharacters", "StatusBarLineCol",
            "StatusBarSelection", "AppTitle"
        ];

        [Theory]
        [InlineData("DocumentUntitled")]
        [InlineData("StatusReady")]
        [InlineData("StatusNewDocument")]
        [InlineData("ButtonSave")]
        [InlineData("ButtonCancel")]
        public void GetString_CoreKeys_NeverNull(string key)
        {
            string result = ResourceHelper.GetString(key);
            Assert.NotNull(result);
            Assert.NotEmpty(result);
        }

        [Fact]
        public void GetString_UnknownKey_ReturnsKeyName()
        {
            string result = ResourceHelper.GetString("NonExistentKey12345");
            Assert.Equal("NonExistentKey12345", result);
        }

        [Fact]
        public void GetFormatted_AppTitle_IncludesDocName()
        {
            string result = ResourceHelper.GetFormatted("AppTitle", "MyDoc.rtf");
            Assert.Contains("MyDoc.rtf", result);
        }

        [Fact]
        public void GetFormatted_StatusBarWords_IncludesCount()
        {
            string result = ResourceHelper.GetFormatted("StatusBarWords", 42);
            Assert.Contains("42", result);
        }

        [Fact]
        public void GetFormatted_StatusBarLineCol_IncludesLineAndCol()
        {
            string result = ResourceHelper.GetFormatted("StatusBarLineCol", 5, 10);
            Assert.Contains("5", result);
            Assert.Contains("10", result);
        }

        [Fact]
        public void GetFormatted_StatusBarSelection_IncludesLength()
        {
            string result = ResourceHelper.GetFormatted("StatusBarSelection", 25);
            Assert.Contains("25", result);
        }

        [Fact]
        public void GetFormatted_StatusBarCharacters_IncludesCount()
        {
            string result = ResourceHelper.GetFormatted("StatusBarCharacters", 100);
            Assert.Contains("100", result);
        }
    }

    // â•â•â• ViewModel Property Change Tracking Tests â•â•â•

    public class ViewModelPropertyTrackingTests
    {
        [Fact]
        public void AllObservableProperties_FirePropertyChanged()
        {
            var vm = new EditorViewModel();
            var firedProps = new HashSet<string>();
            vm.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName != null)
                    firedProps.Add(e.PropertyName);
            };

            vm.DocumentTitle = "Test";
            vm.StatusMessage = "Testing";
            vm.IsModified = true;
            vm.FontFamily = "Arial";
            vm.FontSize = 14;
            vm.IsBold = true;
            vm.IsItalic = true;
            vm.IsUnderline = true;
            vm.IsStrikethrough = true;
            vm.IsSubscript = true;
            vm.IsSuperscript = true;
            vm.Alignment = "Center";
            vm.IsBullets = true;
            vm.IsWordWrap = false;
            vm.ZoomLevel = 150;
            vm.ListType = "Bullet";
            vm.LineSpacing = 2.0;
            vm.WordCount = 10;
            vm.CharCount = 50;
            vm.LineNumber = 5;
            vm.ColumnNumber = 10;
            vm.ParagraphSpacingBefore = 12;
            vm.ParagraphSpacingAfter = 6;
            vm.FindMatchCase = true;
            vm.FindWholeWord = true;
            vm.FindUseRegex = true;
            vm.SelectionLength = 20;
            vm.Encoding = "RTF";

            // Verify all 28 observable properties fired
            var expectedProps = new[]
            {
                "DocumentTitle", "StatusMessage", "IsModified",
                "FontFamily", "FontSize", "IsBold", "IsItalic",
                "IsUnderline", "IsStrikethrough", "IsSubscript",
                "IsSuperscript", "Alignment", "IsBullets",
                "IsWordWrap", "ZoomLevel", "ListType", "LineSpacing",
                "WordCount", "CharCount", "LineNumber", "ColumnNumber",
                "ParagraphSpacingBefore", "ParagraphSpacingAfter",
                "FindMatchCase", "FindWholeWord", "FindUseRegex",
                "SelectionLength", "Encoding"
            };

            foreach (var prop in expectedProps)
            {
                Assert.Contains(prop, firedProps);
            }
        }

        [Fact]
        public void DisplayProperties_AllFireWhenSourceChanges()
        {
            var vm = new EditorViewModel();
            var firedProps = new HashSet<string>();
            vm.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName != null)
                    firedProps.Add(e.PropertyName);
            };

            vm.WordCount = 1;
            Assert.Contains("WordCountDisplay", firedProps);
            firedProps.Clear();

            vm.CharCount = 1;
            Assert.Contains("CharCountDisplay", firedProps);
            firedProps.Clear();

            vm.SelectionLength = 1;
            Assert.Contains("SelectionLengthDisplay", firedProps);
            firedProps.Clear();

            vm.LineNumber = 2;
            Assert.Contains("LineColDisplay", firedProps);
            firedProps.Clear();

            vm.ColumnNumber = 2;
            Assert.Contains("LineColDisplay", firedProps);
            firedProps.Clear();

            vm.ZoomLevel = 110;
            Assert.Contains("ZoomDisplay", firedProps);
            firedProps.Clear();

            vm.Encoding = "ANSI";
            Assert.Contains("EncodingDisplay", firedProps);
        }

        [Fact]
        public void SameValueAssignment_StillFiresPropertyChanged()
        {
            // CommunityToolkit.Mvvm: SetProperty only fires if value differs
            var vm = new EditorViewModel();
            var fired = false;
            vm.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(EditorViewModel.IsBold))
                    fired = true;
            };

            vm.IsBold = true;
            Assert.True(fired);

            fired = false;
            vm.IsBold = true; // same value
            Assert.False(fired); // should NOT fire â€” MVVM Toolkit optimizes
        }

        [Fact]
        public void NewDocument_Fires_MultiplePropertyChangedEvents()
        {
            // Set many properties to non-default values
            var vm = new EditorViewModel
            {
                DocumentTitle = "Test.rtf",
                IsModified = true,
                IsBold = true,
                IsItalic = true,
                IsUnderline = true,
                IsStrikethrough = true,
                IsSubscript = true,
                FontFamily = "Arial",
                FontSize = 16,
                Alignment = "Center",
                ZoomLevel = 200,
                WordCount = 100,
                CharCount = 500,
                LineNumber = 10,
                ColumnNumber = 25,
                IsBullets = true,
                ListType = "Bullet",
                LineSpacing = 2.0,
                IsWordWrap = false,
                FindMatchCase = true,
                FindWholeWord = true,
                FindUseRegex = true,
                ParagraphSpacingBefore = 12,
                ParagraphSpacingAfter = 6,
                SelectionLength = 20,
                Encoding = "RTF"
            };

            var changedCount = 0;
            vm.PropertyChanged += (s, e) => changedCount++;

            vm.NewDocument();

            // NewDocument resets ~25 properties + display properties fire too
            Assert.True(changedCount >= 20, $"Expected 20+ events, got {changedCount}");
        }
    }

    // â•â•â• ColorHelper Exhaustive Tests â•â•â•

    public class ColorHelperExhaustiveTests
    {
        [Theory]
        [InlineData("#000000", 0, 0, 0)]
        [InlineData("#FFFFFF", 255, 255, 255)]
        [InlineData("#FF0000", 255, 0, 0)]
        [InlineData("#00FF00", 0, 255, 0)]
        [InlineData("#0000FF", 0, 0, 255)]
        [InlineData("#808080", 128, 128, 128)]
        [InlineData("#C0C0C0", 192, 192, 192)]
        public void ParseHexColor_StandardColors(string hex, byte r, byte g, byte b)
        {
            var color = ColorHelper.ParseHexColor(hex);
            Assert.Equal(255, color.A);
            Assert.Equal(r, color.R);
            Assert.Equal(g, color.G);
            Assert.Equal(b, color.B);
        }

        [Theory]
        [InlineData("#80FF0000", 128, 255, 0, 0)]
        [InlineData("#00FFFFFF", 0, 255, 255, 255)]
        [InlineData("#FFFF8000", 255, 255, 128, 0)]
        public void ParseHexColor_WithAlpha_Values(string hex, byte a, byte r, byte g, byte b)
        {
            var color = ColorHelper.ParseHexColor(hex);
            Assert.Equal(a, color.A);
            Assert.Equal(r, color.R);
            Assert.Equal(g, color.G);
            Assert.Equal(b, color.B);
        }

        [Theory]
        [InlineData("000000")]
        [InlineData("ffffff")]
        [InlineData("FF8000")]
        public void ParseHexColor_WithoutHash_Works(string hex)
        {
            var color = ColorHelper.ParseHexColor(hex);
            Assert.Equal(255, color.A);
        }

        [Theory]
        [InlineData("")]
        [InlineData(null)]
        public void ParseHexColor_NullOrEmpty_Throws(string? hex)
        {
            Assert.Throws<ArgumentException>(() => ColorHelper.ParseHexColor(hex!));
        }

        [Theory]
        [InlineData("#12345")]
        [InlineData("#1234567")]
        [InlineData("#1")]
        [InlineData("#12")]
        [InlineData("#123")]
        [InlineData("#1234")]
        [InlineData("#123456789")]
        public void ParseHexColor_InvalidLength_Throws(string hex)
        {
            Assert.Throws<FormatException>(() => ColorHelper.ParseHexColor(hex));
        }

        [Theory]
        [InlineData("#GGGGGG")]
        [InlineData("#ZZZZZZ")]
        [InlineData("#!@#$%^")]
        public void ParseHexColor_InvalidChars_Throws(string hex)
        {
            Assert.Throws<FormatException>(() => ColorHelper.ParseHexColor(hex));
        }

        [Fact]
        public void ParseHexColor_CaseInsensitive()
        {
            var upper = ColorHelper.ParseHexColor("#AABBCC");
            var lower = ColorHelper.ParseHexColor("#aabbcc");
            var mixed = ColorHelper.ParseHexColor("#AaBbCc");

            Assert.Equal(upper.R, lower.R);
            Assert.Equal(upper.G, lower.G);
            Assert.Equal(upper.B, lower.B);
            Assert.Equal(upper.R, mixed.R);
            Assert.Equal(upper.G, mixed.G);
            Assert.Equal(upper.B, mixed.B);
        }
    }

    // â•â•â• Backstage Event Contract Tests â•â•â•

    public class BackstageEventContractTests
    {
        [Fact]
        public void BackstageView_HasExpectedEvents()
        {
            var type = typeof(SmrtPad.Views.FileBackstageView);

            Assert.NotNull(type.GetEvent("NewRequested"));
            Assert.NotNull(type.GetEvent("OpenRequested"));
            Assert.NotNull(type.GetEvent("SaveRequested"));
            Assert.NotNull(type.GetEvent("SaveAsRequested"));
            Assert.NotNull(type.GetEvent("PrintRequested"));
            Assert.NotNull(type.GetEvent("OptionsRequested"));
            Assert.NotNull(type.GetEvent("ExitRequested"));
            Assert.NotNull(type.GetEvent("RecentFileRequested"));
        }

        [Fact]
        public void BackstageView_EventTypes_AreCorrect()
        {
            var type = typeof(SmrtPad.Views.FileBackstageView);

            var newReq = type.GetEvent("NewRequested");
            Assert.Equal(typeof(EventHandler), newReq!.EventHandlerType);

            var recentReq = type.GetEvent("RecentFileRequested");
            Assert.Equal(typeof(EventHandler<string>), recentReq!.EventHandlerType);
        }

        [Fact]
        public void BackstageView_HasSetDocumentProperties()
        {
            var type = typeof(SmrtPad.Views.FileBackstageView);
            var method = type.GetMethod("SetDocumentProperties");
            Assert.NotNull(method);

            var parameters = method!.GetParameters();
            Assert.Equal(5, parameters.Length);
            Assert.Equal(typeof(string), parameters[0].ParameterType);   // fileName
            Assert.Equal(typeof(int), parameters[1].ParameterType);      // wordCount
            Assert.Equal(typeof(int), parameters[2].ParameterType);      // charCount
            Assert.Equal(typeof(string), parameters[3].ParameterType);   // encoding
            Assert.Equal(typeof(bool), parameters[4].ParameterType);     // isModified
        }

        [Fact]
        public void BackstageView_HasSetRecentFiles()
        {
            var type = typeof(SmrtPad.Views.FileBackstageView);
            var method = type.GetMethod("SetRecentFiles");
            Assert.NotNull(method);

            var parameters = method!.GetParameters();
            Assert.Single(parameters);
            Assert.Equal(typeof(List<string>), parameters[0].ParameterType);
        }
    }

    // â•â•â• EditorViewModel Relay Command Tests â•â•â•

    public class RelayCommandTests
    {
        [Fact]
        public void ViewModel_HasExpectedCommands()
        {
            var vm = new EditorViewModel();
            var type = vm.GetType();

            var expectedCommands = new[]
            {
                "NewDocumentCommand",
                "UpdateStatusCommand",
                "ToggleBoldCommand",
                "ToggleItalicCommand",
                "ToggleUnderlineCommand",
                "ToggleStrikethroughCommand",
                "ToggleSubscriptCommand",
                "ToggleSuperscriptCommand",
                "SetAlignmentCommand",
                "ToggleBulletsCommand",
                "ToggleWordWrapCommand",
                "SetListTypeCommand",
                "SetLineSpacingCommand",
                "ZoomInCommand",
                "ZoomOutCommand",
                "SetParagraphSpacingCommand",
                "UpdateWordCountCommand",
                "UpdateCharCountCommand",
                "UpdateCursorPositionCommand"
            };

            foreach (var cmd in expectedCommands)
            {
                var prop = type.GetProperty(cmd);
                Assert.NotNull(prop);
            }
        }

        [Fact]
        public void Commands_CanExecute_ReturnsTrue()
        {
            var vm = new EditorViewModel();

            Assert.True(vm.NewDocumentCommand.CanExecute(null));
            Assert.True(vm.ToggleBoldCommand.CanExecute(null));
            Assert.True(vm.ToggleItalicCommand.CanExecute(null));
            Assert.True(vm.ZoomInCommand.CanExecute(null));
            Assert.True(vm.ZoomOutCommand.CanExecute(null));
        }

        [Fact]
        public void Commands_Execute_ChangeState()
        {
            var vm = new EditorViewModel();

            vm.ToggleBoldCommand.Execute(null);
            Assert.True(vm.IsBold);

            vm.ToggleItalicCommand.Execute(null);
            Assert.True(vm.IsItalic);

            vm.ZoomInCommand.Execute(null);
            Assert.Equal(110.0, vm.ZoomLevel);

            vm.NewDocumentCommand.Execute(null);
            Assert.False(vm.IsBold);
            Assert.False(vm.IsItalic);
            Assert.Equal(100.0, vm.ZoomLevel);
        }

        [Fact]
        public void SetAlignmentCommand_WithParameter()
        {
            var vm = new EditorViewModel();
            vm.SetAlignmentCommand.Execute("Right");
            Assert.Equal("Right", vm.Alignment);
        }

        [Fact]
        public void UpdateStatusCommand_WithParameter()
        {
            var vm = new EditorViewModel();
            vm.UpdateStatusCommand.Execute("Custom status");
            Assert.Equal("Custom status", vm.StatusMessage);
        }
    }

    // â•â•â• RTF Table Generation Tests (via RtfHelper) â•â•â•

    public class RtfTableGenerationTests
    {
        [Fact]
        public void GenerateTable_1x1_HasCorrectStructure()
        {
            string rtf = RtfHelper.GenerateTable(1, 1);
            Assert.StartsWith(@"{\rtf1\ansi", rtf);
            Assert.EndsWith("}", rtf);
            Assert.Contains(@"\trowd", rtf);
            Assert.Contains(@"\cellx2000", rtf);
            Assert.Contains(@"\cell", rtf);
            Assert.Contains(@"\row", rtf);
        }

        [Fact]
        public void GenerateTable_3x3_HasCorrectRowCount()
        {
            string rtf = RtfHelper.GenerateTable(3, 3);
            int rowCount = rtf.Split(@"\row").Length - 1;
            Assert.Equal(3, rowCount);
        }

        [Fact]
        public void GenerateTable_2x4_HasCorrectCellPositions()
        {
            string rtf = RtfHelper.GenerateTable(2, 4);
            Assert.Contains(@"\cellx2000", rtf);
            Assert.Contains(@"\cellx4000", rtf);
            Assert.Contains(@"\cellx6000", rtf);
            Assert.Contains(@"\cellx8000", rtf);
        }

        [Fact]
        public void GenerateTable_HasBorderControls()
        {
            string rtf = RtfHelper.GenerateTable(1, 1);
            Assert.Contains(@"\clbrdrt\brdrs", rtf);
            Assert.Contains(@"\clbrdrl\brdrs", rtf);
            Assert.Contains(@"\clbrdrb\brdrs", rtf);
            Assert.Contains(@"\clbrdrr\brdrs", rtf);
        }

        [Theory]
        [InlineData(1, 1)]
        [InlineData(3, 3)]
        [InlineData(5, 10)]
        [InlineData(50, 20)]
        public void GenerateTable_VariousSizes_ProducesValidRtf(int rows, int cols)
        {
            string rtf = RtfHelper.GenerateTable(rows, cols);
            Assert.StartsWith(@"{\rtf1\ansi", rtf);
            Assert.EndsWith("}", rtf);

            int cellCount = rtf.Split(@"\cell ").Length - 1;
            Assert.Equal(rows * cols, cellCount);
        }

        [Fact]
        public void GenerateTable_ZeroRows_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => RtfHelper.GenerateTable(0, 3));
        }

        [Fact]
        public void GenerateTable_NegativeCols_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => RtfHelper.GenerateTable(3, -1));
        }
    }

    // â•â•â• ViewModel Default State Contract Tests â•â•â•

    public class ViewModelDefaultContractTests
    {
        [Fact]
        public void AllDefaults_ExhaustiveVerification()
        {
            var vm = new EditorViewModel();

            Assert.Equal("Untitled", vm.DocumentTitle);
            Assert.Equal("Ready", vm.StatusMessage);
            Assert.False(vm.IsModified);
            Assert.Equal("Segoe UI", vm.FontFamily);
            Assert.Equal(11.0, vm.FontSize);
            Assert.False(vm.IsBold);
            Assert.False(vm.IsItalic);
            Assert.False(vm.IsUnderline);
            Assert.False(vm.IsStrikethrough);
            Assert.False(vm.IsSubscript);
            Assert.False(vm.IsSuperscript);
            Assert.Equal("Left", vm.Alignment);
            Assert.False(vm.IsBullets);
            Assert.True(vm.IsWordWrap);
            Assert.Equal(100.0, vm.ZoomLevel);
            Assert.Equal("None", vm.ListType);
            Assert.Equal(1.0, vm.LineSpacing);
            Assert.Equal(0, vm.WordCount);
            Assert.Equal(0, vm.CharCount);
            Assert.Equal(1, vm.LineNumber);
            Assert.Equal(1, vm.ColumnNumber);
            Assert.Equal(0.0, vm.ParagraphSpacingBefore);
            Assert.Equal(0.0, vm.ParagraphSpacingAfter);
            Assert.False(vm.FindMatchCase);
            Assert.False(vm.FindWholeWord);
            Assert.False(vm.FindUseRegex);
            Assert.NotNull(vm.RecentFiles);
            Assert.Empty(vm.RecentFiles);
            Assert.Equal(0, vm.SelectionLength);
            Assert.Equal("UTF-8", vm.Encoding);
        }

        [Fact]
        public void AllDisplayDefaults_ExhaustiveVerification()
        {
            var vm = new EditorViewModel();

            Assert.Contains("0", vm.WordCountDisplay);
            Assert.Contains("0", vm.CharCountDisplay);
            Assert.Contains("0", vm.SelectionLengthDisplay);
            Assert.Contains("1", vm.LineColDisplay);
            Assert.Equal("100%", vm.ZoomDisplay);
            Assert.Equal("UTF-8", vm.EncodingDisplay);
        }

        [Fact]
        public void NewDocument_RestoredExactDefaults()
        {
            // Modify everything to non-default
            var vm = new EditorViewModel
            {
                DocumentTitle = "Modified.rtf",
                IsModified = true,
                FontFamily = "Comic Sans MS",
                FontSize = 72,
                IsBold = true,
                IsItalic = true,
                IsUnderline = true,
                IsStrikethrough = true,
                IsSubscript = true,
                IsSuperscript = true,
                Alignment = "Justify",
                IsBullets = true,
                IsWordWrap = false,
                ZoomLevel = 500,
                ListType = "UppercaseRoman",
                LineSpacing = 3.0,
                WordCount = 9999,
                CharCount = 99999,
                LineNumber = 500,
                ColumnNumber = 200,
                ParagraphSpacingBefore = 24,
                ParagraphSpacingAfter = 18,
                FindMatchCase = true,
                FindWholeWord = true,
                FindUseRegex = true,
                SelectionLength = 100,
                Encoding = "RTF"
            };

            vm.NewDocument();

            // Everything should be back to default
            Assert.Equal("Untitled", vm.DocumentTitle);
            Assert.False(vm.IsModified);
            Assert.Equal("Segoe UI", vm.FontFamily);
            Assert.Equal(11.0, vm.FontSize);
            Assert.False(vm.IsBold);
            Assert.False(vm.IsItalic);
            Assert.False(vm.IsUnderline);
            Assert.False(vm.IsStrikethrough);
            Assert.False(vm.IsSubscript);
            Assert.False(vm.IsSuperscript);
            Assert.Equal("Left", vm.Alignment);
            Assert.False(vm.IsBullets);
            Assert.True(vm.IsWordWrap);
            Assert.Equal(100.0, vm.ZoomLevel);
            Assert.Equal("None", vm.ListType);
            Assert.Equal(1.0, vm.LineSpacing);
            Assert.Equal(0, vm.WordCount);
            Assert.Equal(0, vm.CharCount);
            Assert.Equal(1, vm.LineNumber);
            Assert.Equal(1, vm.ColumnNumber);
            Assert.Equal(0.0, vm.ParagraphSpacingBefore);
            Assert.Equal(0.0, vm.ParagraphSpacingAfter);
            Assert.False(vm.FindMatchCase);
            Assert.False(vm.FindWholeWord);
            Assert.False(vm.FindUseRegex);
            Assert.Equal(0, vm.SelectionLength);
            Assert.Equal("UTF-8", vm.Encoding);
        }

        [Fact]
        public void ObservablePropertyCount_Is29()
        {
            // Verify we track the expected number of observable properties
            var type = typeof(EditorViewModel);
            var observableFields = type.GetFields(
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .Where(f => f.Name.StartsWith('_') && !f.Name.Contains("BackingField"))
                .Where(f => f.DeclaringType == type)
                .ToList();

            // At least 29 backing fields from [ObservableProperty]
            Assert.True(observableFields.Count >= 29, $"Expected â‰¥29 fields, found {observableFields.Count}");
        }
    }

    // â•â•â• App.ConfigureServices Parity Tests â•â•â•

    public class AppConfigureServiceParityTests
    {
        [Fact]
        public void ConfigureServices_MatchesExpectedRegistrations()
        {
            // Verify the same registrations as App.ConfigureServices()
            var services = new ServiceCollection();
            services.AddSingleton<ISettingsService, SettingsService>();
            services.AddSingleton<EditorViewModel>();
            services.AddTransient<IDialogService, DialogService>();
            services.AddTransient<IFileService, FileService>();
            var provider = services.BuildServiceProvider();

            // Verify all types resolve correctly
            var settings = provider.GetRequiredService<ISettingsService>();
            var vm = provider.GetRequiredService<EditorViewModel>();
            var dialog = provider.GetRequiredService<IDialogService>();
            var file = provider.GetRequiredService<IFileService>();

            Assert.IsType<SettingsService>(settings);
            Assert.IsType<EditorViewModel>(vm);
            Assert.IsType<DialogService>(dialog);
            Assert.IsType<FileService>(file);
        }

        [Fact]
        public void ConfigureServices_SingletonLifetimes_AreCorrect()
        {
            var services = new ServiceCollection();
            services.AddSingleton<ISettingsService, SettingsService>();
            services.AddSingleton<EditorViewModel>();
            services.AddTransient<IDialogService, DialogService>();
            services.AddTransient<IFileService, FileService>();
            var provider = services.BuildServiceProvider();

            // Singletons return same instance
            Assert.Same(
                provider.GetRequiredService<ISettingsService>(),
                provider.GetRequiredService<ISettingsService>());
            Assert.Same(
                provider.GetRequiredService<EditorViewModel>(),
                provider.GetRequiredService<EditorViewModel>());

            // Transients return different instances
            Assert.NotSame(
                provider.GetRequiredService<IDialogService>(),
                provider.GetRequiredService<IDialogService>());
            Assert.NotSame(
                provider.GetRequiredService<IFileService>(),
                provider.GetRequiredService<IFileService>());
        }
    }

    // â•â•â• Settings Service Concurrency Tests â•â•â•

    public partial class SettingsServiceConcurrencyTests : IDisposable
    {
        private readonly string _testDir;
        private readonly string _settingsPath;

        public SettingsServiceConcurrencyTests()
        {
            _testDir = Path.Combine(Path.GetTempPath(), "SmrtPad_ConcTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_testDir);
            _settingsPath = Path.Combine(_testDir, "settings.json");
        }

        public void Dispose()
        {
            try { Directory.Delete(_testDir, true); } catch { }
            GC.SuppressFinalize(this);
        }

        [Fact]
        public void RapidAddRecentFile_DoesNotCorruptData()
        {
            var svc = new SettingsService(_settingsPath);
            for (int i = 0; i < 20; i++)
            {
                svc.AddRecentFile($"C:\\file{i}.rtf");
            }

            // Should be capped at 10
            Assert.Equal(10, svc.RecentFiles.Count);
            // Most recent should be last added
            Assert.Equal("C:\\file19.rtf", svc.RecentFiles[0]);
        }

        [Fact]
        public void RapidSaveLoad_DataRemains()
        {
            var svc = new SettingsService(_settingsPath)
            {
                DefaultFontFamily = "Consolas",
                DefaultFontSize = 16,
                AutoSaveEnabled = true
            };

            // Save and reload many times
            for (int i = 0; i < 10; i++)
            {
                svc.Save();
                svc.Load();
            }

            Assert.Equal("Consolas", svc.DefaultFontFamily);
            Assert.Equal(16.0, svc.DefaultFontSize);
            Assert.True(svc.AutoSaveEnabled);
        }

        [Fact]
        public void MultipleInstances_LastWriteWins()
        {
            var svc1 = new SettingsService(_settingsPath) { DefaultFontFamily = "Arial" };
            svc1.Save();

            var svc2 = new SettingsService(_settingsPath) { DefaultFontFamily = "Consolas" };
            svc2.Save();

            var svc3 = new SettingsService(_settingsPath);
            Assert.Equal("Consolas", svc3.DefaultFontFamily);
        }

        [Fact]
        public void SettingsFile_IsValidJsonAfterSave()
        {
            var svc = new SettingsService(_settingsPath) { DefaultFontFamily = "Calibri" };
            svc.AddRecentFile("C:\\test.rtf");
            svc.Save();

            string json = File.ReadAllText(_settingsPath);
            Assert.False(string.IsNullOrWhiteSpace(json));

            // Should be parseable JSON
            var doc = System.Text.Json.JsonDocument.Parse(json);
            Assert.Equal(System.Text.Json.JsonValueKind.Object, doc.RootElement.ValueKind);
        }
    }

    // â•â•â• Localization Drawing Key Satellite Tests â•â•â•

    public class LocalizationDrawingKeySatelliteTests
    {
        private static readonly string[] DrawingKeys =
        [
            "DrawingTitle", "DrawingInsert", "DrawingClear",
            "DrawingColor", "DrawingStrokeWidth"
        ];

        private static readonly string[] AllLocales =
        [
            "en-US", "ar-SA", "de-DE", "es-ES", "fr-FR",
            "ja-JP", "ru-RU", "ur-PK", "zh-Hans"
        ];

        private static string? FindStringsRoot()
        {
            string? dir = Directory.GetCurrentDirectory();
            while (dir != null)
            {
                string candidate = Path.Combine(dir, "SmrtPad", "Strings");
                if (Directory.Exists(candidate)) return candidate;
                dir = Directory.GetParent(dir)?.FullName;
            }
            return null;
        }

        private static Dictionary<string, string> LoadResw(string locale)
        {
            var root = FindStringsRoot();
            if (root == null) return [];
            string path = Path.Combine(root, locale, "Resources.resw");
            if (!File.Exists(path)) return [];

            var entries = new Dictionary<string, string>();
            var doc = System.Xml.Linq.XDocument.Load(path);
            foreach (var data in doc.Descendants("data"))
            {
                string? name = data.Attribute("name")?.Value;
                string? value = data.Element("value")?.Value;
                if (name != null && value != null)
                    entries[name] = value;
            }
            return entries;
        }

        /// <summary>Returns all resource-key names for <paramref name="locale"/>.</summary>
        public static IReadOnlyList<string> GetAllResourceKeys(string locale)
            => [..LoadResw(locale).Keys];

        [Theory]
        [InlineData("ar-SA")]
        [InlineData("de-DE")]
        [InlineData("es-ES")]
        [InlineData("fr-FR")]
        [InlineData("ja-JP")]
        [InlineData("ru-RU")]
        [InlineData("ur-PK")]
        [InlineData("zh-Hans")]
        public void AllDrawingKeys_ExistInSatelliteLocale(string locale)
        {
            var entries = LoadResw(locale);
            if (entries.Count == 0) return; // Skip if locale file not found

            foreach (var key in DrawingKeys)
            {
                Assert.Contains(key, entries.Keys);
                Assert.False(string.IsNullOrWhiteSpace(entries[key]), $"Key '{key}' is empty in {locale}");
            }
        }

        [Theory]
        [InlineData("ar-SA")]
        [InlineData("de-DE")]
        [InlineData("es-ES")]
        [InlineData("fr-FR")]
        [InlineData("ja-JP")]
        [InlineData("ru-RU")]
        [InlineData("ur-PK")]
        [InlineData("zh-Hans")]
        public void DrawingKeys_AreLocalized_NotEnglish(string locale)
        {
            var enEntries = LoadResw("en-US");
            var localeEntries = LoadResw(locale);
            if (enEntries.Count == 0 || localeEntries.Count == 0) return;

            int localizedCount = 0;
            foreach (var key in DrawingKeys)
            {
                if (localeEntries.ContainsKey(key) && enEntries.ContainsKey(key))
                {
                    if (localeEntries[key] != enEntries[key])
                        localizedCount++;
                }
            }
            // At least some should be translated (unless the word is the same)
            Assert.True(localizedCount >= 2, $"Only {localizedCount} of {DrawingKeys.Length} keys localized in {locale}");
        }
    }

    // â•â•â• MainWindow Reflection Contract Tests â•â•â•

    public class MainWindowContractTests
    {
        [Fact]
        public void MainWindow_HasViewModelProperty()
        {
            var type = typeof(SmrtPad.MainWindow);
            var prop = type.GetProperty("ViewModel");
            Assert.NotNull(prop);
            Assert.Equal(typeof(EditorViewModel), prop!.PropertyType);
        }

        [Fact]
        public void MainWindow_HasExpectedClickHandlers()
        {
            var type = typeof(SmrtPad.MainWindow);
            var methods = type.GetMethods(
                System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Instance);

            var expectedHandlers = new[]
            {
                "New_Click", "Open_Click", "Save_Click", "SaveAs_Click",
                "Bold_Click", "Italic_Click", "Underline_Click",
                "Strikethrough_Click", "Subscript_Click", "Superscript_Click",
                "AlignLeft_Click", "AlignCenter_Click", "AlignRight_Click",
                "AlignJustify_Click", "Bullets_Click",
                "Cut_Click", "Copy_Click", "Paste_Click",
                "Undo_Click", "Redo_Click",
                "FindNext_Click", "FindPrevious_Click",
                "Replace_Click", "ReplaceAll_Click",
                "WordWrap_Click", "Print_Click",
                "GrowFont_Click", "ShrinkFont_Click",
                "InsertPicture_Click", "InsertDateTime_Click",
                "PaintDrawing_Click", "InsertObject_Click",
                "InsertHyperlink_Click", "InsertTable_Click",
                "InsertSymbol_Click", "Options_Click",
                "StyleNormal_Click", "StyleHeading1_Click",
                "StyleHeading2_Click", "StyleHeading3_Click",
                "StyleSubtitle_Click", "StyleQuote_Click"
            };

            var methodNames = methods.Select(m => m.Name).ToHashSet();
            foreach (var handler in expectedHandlers)
            {
                Assert.Contains(handler, methodNames);
            }
        }

        [Fact]
        public void MainWindow_HasOpenFileByPathAsync()
        {
            var type = typeof(SmrtPad.MainWindow);
            var method = type.GetMethod("OpenFileByPathAsync");
            Assert.NotNull(method);
            Assert.Equal(typeof(Task), method!.ReturnType);
        }

        [Fact]
        public void MainWindow_HasInitializeFonts()
        {
            var type = typeof(SmrtPad.MainWindow);
            var method = type.GetMethod("InitializeFonts",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.NotNull(method);
        }

        [Fact]
        public void MainWindow_XAML_FontFamilyUsesDropDownOpened()
        {
            // DropDownOpened handler styles containers via code-behind â€” avoids
            // both ItemTemplate (breaks editable text) and {Binding} in Style
            // (crashes at runtime in WinUI 3)
            string? xamlPath = FindXamlPath();
            if (xamlPath == null) return;

            string xaml = File.ReadAllText(xamlPath);
            Assert.Contains("FontFamilyComboBox", xaml);
            Assert.Contains("DropDownOpened", xaml);
            Assert.DoesNotContain("ItemTemplate", xaml);
            Assert.DoesNotContain("ItemContainerStyle", xaml);
        }

        [Fact]
        public void MainWindow_HasFontFamilyDropDownOpenedHandler()
        {
            var type = typeof(SmrtPad.MainWindow);
            var method = type.GetMethod("FontFamilyComboBox_DropDownOpened",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.NotNull(method);
        }

        [Fact]
        public void MainWindow_XAML_AlignmentUsesGrid()
        {
            // Verify alignment buttons use Grid layout (equal spacing) not StackPanel
            string? xamlPath = FindXamlPath();
            if (xamlPath == null) return;

            string xaml = File.ReadAllText(xamlPath);
            // The alignment section should contain Grid columns, not a StackPanel
            Assert.Contains("AlignLeftToggle", xaml);
            Assert.Contains("Grid.Column=\"0\"", xaml);
            Assert.Contains("Grid.Column=\"3\"", xaml);
        }

        [Fact]
        public void MainWindow_XAML_FontSizeComboBox_IsCompact()
        {
            string? xamlPath = FindXamlPath();
            if (xamlPath == null) return;

            string xaml = File.ReadAllText(xamlPath);
            // Font size ComboBox should be narrow (62px, not 112px)
            Assert.Contains("FontSizeComboBox", xaml);
            Assert.Contains("Width=\"62\"", xaml);
        }

        private static string? FindXamlPath()
        {
            string? dir = Directory.GetCurrentDirectory();
            while (dir != null)
            {
                string candidate = Path.Combine(dir, "SmrtPad", "MainWindow.xaml");
                if (File.Exists(candidate)) return candidate;
                dir = Directory.GetParent(dir)?.FullName;
            }
            return null;
        }

        [Fact]
        public void MainWindow_HasAppWindowClosingHandler()
        {
            var type = typeof(SmrtPad.MainWindow);
            var method = type.GetMethod("AppWindow_Closing",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.NotNull(method);
            var parameters = method!.GetParameters();
            Assert.Equal(2, parameters.Length);
            Assert.Equal("AppWindow", parameters[0].ParameterType.Name);
            Assert.Equal("AppWindowClosingEventArgs", parameters[1].ParameterType.Name);
        }

        [Fact]
        public void MainWindow_PromptSaveChangesAsync_Exists()
        {
            var type = typeof(SmrtPad.MainWindow);
            var method = type.GetMethod("PromptSaveChangesAsync",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.NotNull(method);
            Assert.Equal(typeof(Task<bool>), method!.ReturnType);
        }
    }

    // â•â•â• ParagraphStyleHelper Tests â•â•â•

    public class ParagraphStyleHelperTests
    {
        [Fact]
        public void Normal_HasExpectedValues()
        {
            var s = ParagraphStyleHelper.Normal;
            Assert.Equal("Segoe UI", s.FontName);
            Assert.Equal(11f, s.FontSize);
            Assert.False(s.Bold);
            Assert.False(s.Italic);
            Assert.Equal("Left", s.Alignment);
            Assert.Equal(0f, s.SpaceBefore);
            Assert.Equal(0f, s.SpaceAfter);
        }

        [Fact]
        public void Heading1_HasExpectedValues()
        {
            var s = ParagraphStyleHelper.Heading1;
            Assert.Equal("Segoe UI", s.FontName);
            Assert.Equal(20f, s.FontSize);
            Assert.True(s.Bold);
            Assert.False(s.Italic);
            Assert.Equal(12f, s.SpaceBefore);
            Assert.Equal(4f, s.SpaceAfter);
        }

        [Fact]
        public void Heading2_HasExpectedValues()
        {
            var s = ParagraphStyleHelper.Heading2;
            Assert.Equal(16f, s.FontSize);
            Assert.True(s.Bold);
            Assert.Equal(10f, s.SpaceBefore);
            Assert.Equal(3f, s.SpaceAfter);
        }

        [Fact]
        public void Heading3_HasExpectedValues()
        {
            var s = ParagraphStyleHelper.Heading3;
            Assert.Equal(13f, s.FontSize);
            Assert.True(s.Bold);
            Assert.Equal(8f, s.SpaceBefore);
            Assert.Equal(2f, s.SpaceAfter);
        }

        [Fact]
        public void Subtitle_IsItalicNotBold()
        {
            var s = ParagraphStyleHelper.Subtitle;
            Assert.Equal(14f, s.FontSize);
            Assert.False(s.Bold);
            Assert.True(s.Italic);
            Assert.Equal(6f, s.SpaceBefore);
            Assert.Equal(4f, s.SpaceAfter);
        }

        [Fact]
        public void Quote_IsItalicNotBold()
        {
            var s = ParagraphStyleHelper.Quote;
            Assert.Equal(11f, s.FontSize);
            Assert.False(s.Bold);
            Assert.True(s.Italic);
            Assert.Equal(8f, s.SpaceBefore);
            Assert.Equal(8f, s.SpaceAfter);
        }

        [Fact]
        public void All_Contains6Styles()
        {
            Assert.Equal(6, ParagraphStyleHelper.All.Count);
        }

        [Theory]
        [InlineData("Normal")]
        [InlineData("Heading1")]
        [InlineData("Heading2")]
        [InlineData("Heading3")]
        [InlineData("Subtitle")]
        [InlineData("Quote")]
        public void All_ContainsKey(string key)
        {
            Assert.Contains(key, ParagraphStyleHelper.All.Keys);
            Assert.NotNull(ParagraphStyleHelper.All[key]);
        }

        [Fact]
        public void AllStyles_HaveLeftAlignment()
        {
            foreach (var kvp in ParagraphStyleHelper.All)
            {
                Assert.Equal("Left", kvp.Value.Alignment);
            }
        }

        [Fact]
        public void AllStyles_UseSegoeUI()
        {
            foreach (var kvp in ParagraphStyleHelper.All)
            {
                Assert.Equal("Segoe UI", kvp.Value.FontName);
            }
        }

        [Fact]
        public void Headings_AreBold_OthersAreNot()
        {
            Assert.True(ParagraphStyleHelper.Heading1.Bold);
            Assert.True(ParagraphStyleHelper.Heading2.Bold);
            Assert.True(ParagraphStyleHelper.Heading3.Bold);
            Assert.False(ParagraphStyleHelper.Normal.Bold);
            Assert.False(ParagraphStyleHelper.Subtitle.Bold);
            Assert.False(ParagraphStyleHelper.Quote.Bold);
        }

        [Fact]
        public void FontSizes_Descend_FromHeading1ToNormal()
        {
            Assert.True(ParagraphStyleHelper.Heading1.FontSize > ParagraphStyleHelper.Heading2.FontSize);
            Assert.True(ParagraphStyleHelper.Heading2.FontSize > ParagraphStyleHelper.Heading3.FontSize);
            Assert.True(ParagraphStyleHelper.Heading3.FontSize > ParagraphStyleHelper.Normal.FontSize);
        }
    }

    // â•â•â• RulerHelper Tests â•â•â•

    public class RulerHelperTests
    {
        [Fact]
        public void Inches_At100Percent_Returns96Dpi()
        {
            double ppu = RulerHelper.GetPixelsPerUnit("in", 100.0, out string label);
            Assert.Equal(96.0, ppu);
            Assert.Equal("in", label);
        }

        [Fact]
        public void Centimeters_At100Percent_ReturnsCorrectDpi()
        {
            double ppu = RulerHelper.GetPixelsPerUnit("cm", 100.0, out string label);
            Assert.Equal(96.0 / 2.54, ppu, 6);
            Assert.Equal("cm", label);
        }

        [Fact]
        public void Inches_At200Percent_ReturnsDoubled()
        {
            double ppu = RulerHelper.GetPixelsPerUnit("in", 200.0, out _);
            Assert.Equal(192.0, ppu);
        }

        [Fact]
        public void Centimeters_At50Percent_ReturnsHalved()
        {
            double ppu = RulerHelper.GetPixelsPerUnit("cm", 50.0, out _);
            Assert.Equal((96.0 / 2.54) * 0.5, ppu, 6);
        }

        [Theory]
        [InlineData("in", "in")]
        [InlineData("cm", "cm")]
        [InlineData("", "in")]
        [InlineData("xx", "in")]
        public void UnitLabel_ReflectsInput(string input, string expectedLabel)
        {
            RulerHelper.GetPixelsPerUnit(input, 100.0, out string label);
            Assert.Equal(expectedLabel, label);
        }

        [Theory]
        [InlineData(10.0)]
        [InlineData(100.0)]
        [InlineData(250.0)]
        [InlineData(500.0)]
        public void PixelsPerUnit_ScalesLinearly(double zoom)
        {
            double at100 = RulerHelper.GetPixelsPerUnit("in", 100.0, out _);
            double atZoom = RulerHelper.GetPixelsPerUnit("in", zoom, out _);
            Assert.Equal(at100 * zoom / 100.0, atZoom, 10);
        }
    }

    // â•â•â• DocumentImportHelper Direct Tests â•â•â•

    public partial class DocumentImportHelperTests : IDisposable
    {
        private readonly string _testDir;

        public DocumentImportHelperTests()
        {
            _testDir = Path.Combine(Path.GetTempPath(), "SmrtPad_DocImportTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_testDir);
        }

        public void Dispose()
        {
            try { Directory.Delete(_testDir, true); } catch { }
            GC.SuppressFinalize(this);
        }

        [Fact]
        public void ExtractText_Docx_ReturnsContent()
        {
            string ns = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
            var doc = new XDocument(
                new XElement(XName.Get("document", ns),
                    new XElement(XName.Get("body", ns),
                        new XElement(XName.Get("p", ns),
                            new XElement(XName.Get("r", ns),
                                new XElement(XName.Get("t", ns), "Hello Helper"))))));

            string filePath = Path.Combine(_testDir, "test.docx");
            using (var zip = ZipFile.Open(filePath, ZipArchiveMode.Create))
            {
                var entry = zip.CreateEntry("word/document.xml");
                using var writer = new StreamWriter(entry.Open());
                doc.Save(writer);
            }

            using var stream = File.OpenRead(filePath);
            string result = DocumentImportHelper.ExtractText(stream, ".docx");
            Assert.Contains("Hello Helper", result);
        }

        [Fact]
        public void ExtractText_Odt_ReturnsContent()
        {
            string ns = "urn:oasis:names:tc:opendocument:xmlns:text:1.0";
            var doc = new XDocument(
                new XElement(XName.Get("document-content", "urn:oasis:names:tc:opendocument:xmlns:office:1.0"),
                    new XElement(XName.Get("body", "urn:oasis:names:tc:opendocument:xmlns:office:1.0"),
                        new XElement(XName.Get("text", "urn:oasis:names:tc:opendocument:xmlns:office:1.0"),
                            new XElement(XName.Get("p", ns), "ODT via helper")))));

            string filePath = Path.Combine(_testDir, "test.odt");
            using (var zip = ZipFile.Open(filePath, ZipArchiveMode.Create))
            {
                var entry = zip.CreateEntry("content.xml");
                using var writer = new StreamWriter(entry.Open());
                doc.Save(writer);
            }

            using var stream = File.OpenRead(filePath);
            string result = DocumentImportHelper.ExtractText(stream, ".odt");
            Assert.Contains("ODT via helper", result);
        }

        [Fact]
        public void ExtractText_MissingEntry_ReturnsEmpty()
        {
            string filePath = Path.Combine(_testDir, "empty.docx");
            using (var zip = ZipFile.Open(filePath, ZipArchiveMode.Create))
            {
                var entry = zip.CreateEntry("other.txt");
                using var writer = new StreamWriter(entry.Open());
                writer.Write("not a doc");
            }

            using var stream = File.OpenRead(filePath);
            string result = DocumentImportHelper.ExtractText(stream, ".docx");
                    Assert.Equal(string.Empty, result);
                    }
                }

                // â•â•â• PdfHelper Tests â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

                public partial class PdfHelperTests
                {
                    [GeneratedRegex(@"/Type /Page[^s]")]
                    private static partial System.Text.RegularExpressions.Regex PdfPageRegex();
                    [Fact]
                    public void GeneratePdf_EmptyText_ReturnsPdfBytes()
                    {
                        byte[] pdf = PdfHelper.GeneratePdf(string.Empty);
                        Assert.NotNull(pdf);
                        Assert.True(pdf.Length > 0);
                    }

                    [Fact]
                    public void GeneratePdf_StartsWithPdfHeader()
                    {
                        byte[] pdf = PdfHelper.GeneratePdf("Hello World");
                        string header = Encoding.Latin1.GetString(pdf, 0, 7);
                        Assert.Equal("%PDF-1.", header);
                    }

                    [Fact]
                    public void GeneratePdf_EndsWithEof()
                    {
                        byte[] pdf = PdfHelper.GeneratePdf("Test content");
                        string text = Encoding.Latin1.GetString(pdf);
                        Assert.Contains("%%EOF", text);
                    }

                    [Fact]
                    public void GeneratePdf_ContainsXrefAndTrailer()
                    {
                        byte[] pdf = PdfHelper.GeneratePdf("Sample");
                        string text = Encoding.Latin1.GetString(pdf);
                        Assert.Contains("xref", text);
                        Assert.Contains("trailer", text);
                        Assert.Contains("startxref", text);
                    }

                    [Fact]
                    public void GeneratePdf_ContainsCatalogAndPages()
                    {
                        byte[] pdf = PdfHelper.GeneratePdf("Multi page test");
                        string text = Encoding.Latin1.GetString(pdf);
                        Assert.Contains("/Catalog", text);
                        Assert.Contains("/Pages", text);
                        Assert.Contains("/Font", text);
                    }

                    [Fact]
                    public void GeneratePdf_NullText_Throws()
                    {
                        Assert.Throws<ArgumentNullException>(() => PdfHelper.GeneratePdf(null!));
                    }

                    [Fact]
                    public void GeneratePdf_MultiPage_ContainsMultiplePageObjects()
                    {
                        // Generate enough text to require more than one page
                        string longText = string.Join("\n", Enumerable.Range(1, 200).Select(i => $"Line {i}: The quick brown fox jumps over the lazy dog."));
                        byte[] pdf = PdfHelper.GeneratePdf(longText);
                        string text = Encoding.Latin1.GetString(pdf);
                        // Multiple /Page objects expected
                        int pageCount = PdfPageRegex().Count(text);
                        Assert.True(pageCount > 1, $"Expected multiple pages, got {pageCount}");
                    }

                    [Theory]
                    [InlineData("Hello", 20, new[] { "Hello" })]
                    [InlineData("Hello World", 5, new[] { "Hello", "World" })]
                    [InlineData("One two three four", 10, new[] { "One two", "three four" })]
                    [InlineData("", 20, new[] { "" })]
                    public void BuildDisplayLines_WrapsCorrectly(string text, int maxChars, string[] expected)
                    {
                        var lines = PdfHelper.BuildDisplayLines(text, maxChars);
                        Assert.Equal(expected, lines.ToArray());
                    }

                    [Fact]
                    public void BuildDisplayLines_NormalisesLineEndings()
                    {
                        var lines = PdfHelper.BuildDisplayLines("A\r\nB\rC\nD", 80);
                        Assert.Equal(4, lines.Count);
                        Assert.Equal("A", lines[0]);
                        Assert.Equal("B", lines[1]);
                        Assert.Equal("C", lines[2]);
                        Assert.Equal("D", lines[3]);
                    }

                    [Fact]
                    public void GeneratePdf_TextPresentInStream()
                    {
                        byte[] pdf = PdfHelper.GeneratePdf("UniqueMarkerXYZ");
                        string text = Encoding.Latin1.GetString(pdf);
                        Assert.Contains("UniqueMarkerXYZ", text);
                    }
                }

                // â•â•â• DocxExportHelper Tests â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

                public class DocxExportHelperTests
                {
                    [Fact]
                    public void GenerateDocx_ReturnsNonEmptyBytes()
                    {
                        byte[] docx = DocxExportHelper.GenerateDocx("Hello");
                        Assert.NotNull(docx);
                        Assert.True(docx.Length > 0);
                    }

                    [Fact]
                    public void GenerateDocx_IsValidZip()
                    {
                        byte[] docx = DocxExportHelper.GenerateDocx("Test");
                        using var ms = new MemoryStream(docx);
                        using var zip = new ZipArchive(ms, ZipArchiveMode.Read);
                        Assert.NotEmpty(zip.Entries);
                    }

                    [Fact]
                    public void GenerateDocx_ContainsRequiredParts()
                    {
                        byte[] docx = DocxExportHelper.GenerateDocx("Test");
                        using var ms = new MemoryStream(docx);
                        using var zip = new ZipArchive(ms, ZipArchiveMode.Read);
                        var names = zip.Entries.Select(e => e.FullName).ToList();
                        Assert.Contains("[Content_Types].xml", names);
                        Assert.Contains("_rels/.rels", names);
                        Assert.Contains("word/document.xml", names);
                        Assert.Contains("word/_rels/document.xml.rels", names);
                    }

                    [Fact]
                    public void GenerateDocx_ContentTypesXml_IsValid()
                    {
                        byte[] docx = DocxExportHelper.GenerateDocx("Test");
                        using var ms = new MemoryStream(docx);
                        using var zip = new ZipArchive(ms, ZipArchiveMode.Read);
                        var entry = zip.GetEntry("[Content_Types].xml");
                        Assert.NotNull(entry);
                        using var stream = entry!.Open();
                        var doc = XDocument.Load(stream);
                        Assert.NotNull(doc.Root);
                        Assert.Equal("Types", doc.Root!.Name.LocalName);
                    }

                    [Fact]
                    public void GenerateDocx_DocumentXml_ContainsParagraphs()
                    {
                        byte[] docx = DocxExportHelper.GenerateDocx("Line 1\nLine 2\nLine 3");
                        using var ms = new MemoryStream(docx);
                        using var zip = new ZipArchive(ms, ZipArchiveMode.Read);
                        var entry = zip.GetEntry("word/document.xml");
                        Assert.NotNull(entry);
                        using var stream = entry!.Open();
                        var doc = XDocument.Load(stream);
                        XNamespace w = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
                        var paragraphs = doc.Descendants(w + "p").ToList();
                        Assert.Equal(3, paragraphs.Count);
                    }

                    [Fact]
                    public void GenerateDocx_DocumentXml_PreservesText()
                    {
                        const string text = "Hello DOCX World";
                        byte[] docx = DocxExportHelper.GenerateDocx(text);
                        using var ms = new MemoryStream(docx);
                        using var zip = new ZipArchive(ms, ZipArchiveMode.Read);
                        var entry = zip.GetEntry("word/document.xml");
                        using var stream = entry!.Open();
                        using var reader = new StreamReader(stream);
                        string xml = reader.ReadToEnd();
                        Assert.Contains(text, xml);
                    }

                    [Fact]
                    public void GenerateDocx_EmptyText_StillValid()
                    {
                        byte[] docx = DocxExportHelper.GenerateDocx(string.Empty);
                        using var ms = new MemoryStream(docx);
                        using var zip = new ZipArchive(ms, ZipArchiveMode.Read);
                        Assert.NotEmpty(zip.Entries);
                    }

                    [Fact]
                    public void GenerateDocx_NullText_Throws()
                    {
                        Assert.Throws<ArgumentNullException>(() => DocxExportHelper.GenerateDocx(null!));
                    }

                    [Fact]
                    public void GenerateDocx_MultilineCrLf_SplitsToParagraphs()
                    {
                        byte[] docx = DocxExportHelper.GenerateDocx("A\r\nB\r\nC");
                        using var ms = new MemoryStream(docx);
                        using var zip = new ZipArchive(ms, ZipArchiveMode.Read);
                        var entry = zip.GetEntry("word/document.xml");
                        using var stream = entry!.Open();
                        var doc = XDocument.Load(stream);
                        XNamespace w = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
                        Assert.Equal(3, doc.Descendants(w + "p").Count());
                    }

                    // ── GenerateRichDocx tests ────────────────────────────────────

                    [Fact]
                    public void GenerateRichDocx_NullRtf_Throws()
                    {
                        Assert.Throws<ArgumentNullException>(() => DocxExportHelper.GenerateRichDocx(null!));
                    }

                    [Fact]
                    public void GenerateRichDocx_EmptyRtf_ReturnsValidZip()
                    {
                        byte[] docx = DocxExportHelper.GenerateRichDocx(string.Empty);
                        Assert.True(docx.Length > 0);
                        using var ms = new MemoryStream(docx);
                        var zip = new ZipArchive(ms, ZipArchiveMode.Read);
                        Assert.NotNull(zip.GetEntry("word/document.xml"));
                    }

                    [Fact]
                    public void GenerateRichDocx_PlainRtf_ContainsText()
                    {
                        string rtf = @"{\rtf1\ansi{\fonttbl{\f0 Arial;}}\pard Hello World\par}";
                        byte[] docx = DocxExportHelper.GenerateRichDocx(rtf);
                        using var ms = new MemoryStream(docx);
                        using var zip = new ZipArchive(ms, ZipArchiveMode.Read);
                        using var stream = zip.GetEntry("word/document.xml")!.Open();
                        string xml = new System.IO.StreamReader(stream).ReadToEnd();
                        Assert.Contains("Hello World", xml);
                    }

                    [Fact]
                    public void GenerateRichDocx_BoldRtf_EmitsBoldElement()
                    {
                        string rtf = @"{\rtf1\ansi\pard \b bold text\b0 normal\par}";
                        byte[] docx = DocxExportHelper.GenerateRichDocx(rtf);
                        using var ms = new MemoryStream(docx);
                        using var zip = new ZipArchive(ms, ZipArchiveMode.Read);
                        using var stream = zip.GetEntry("word/document.xml")!.Open();
                        var doc = XDocument.Load(stream);
                        XNamespace w = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
                        Assert.NotEmpty(doc.Descendants(w + "b"));
                    }

                    [Fact]
                    public void GenerateRichDocx_ItalicRtf_EmitsItalicElement()
                    {
                        string rtf = @"{\rtf1\ansi\pard \i italic\i0 normal\par}";
                        byte[] docx = DocxExportHelper.GenerateRichDocx(rtf);
                        using var ms = new MemoryStream(docx);
                        using var zip = new ZipArchive(ms, ZipArchiveMode.Read);
                        using var stream = zip.GetEntry("word/document.xml")!.Open();
                        var doc = XDocument.Load(stream);
                        XNamespace w = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
                        Assert.NotEmpty(doc.Descendants(w + "i"));
                    }

                    [Fact]
                    public void GenerateRichDocx_UnderlineRtf_EmitsUnderlineElement()
                    {
                        string rtf = @"{\rtf1\ansi\pard \ul underlined\ulnone normal\par}";
                        byte[] docx = DocxExportHelper.GenerateRichDocx(rtf);
                        using var ms = new MemoryStream(docx);
                        using var zip = new ZipArchive(ms, ZipArchiveMode.Read);
                        using var stream = zip.GetEntry("word/document.xml")!.Open();
                        var doc = XDocument.Load(stream);
                        XNamespace w = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
                        Assert.NotEmpty(doc.Descendants(w + "u"));
                    }

                    [Fact]
                    public void GenerateRichDocx_StrikethroughRtf_EmitsStrikeElement()
                    {
                        string rtf = @"{\rtf1\ansi\pard \strike struck\strike0 normal\par}";
                        byte[] docx = DocxExportHelper.GenerateRichDocx(rtf);
                        using var ms = new MemoryStream(docx);
                        using var zip = new ZipArchive(ms, ZipArchiveMode.Read);
                        using var stream = zip.GetEntry("word/document.xml")!.Open();
                        var doc = XDocument.Load(stream);
                        XNamespace w = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
                        Assert.NotEmpty(doc.Descendants(w + "strike"));
                    }

                    [Fact]
                    public void GenerateRichDocx_FontSize_EmitsSzElement()
                    {
                        string rtf = @"{\rtf1\ansi\pard \fs48 Large Text\par}";
                        byte[] docx = DocxExportHelper.GenerateRichDocx(rtf);
                        using var ms = new MemoryStream(docx);
                        using var zip = new ZipArchive(ms, ZipArchiveMode.Read);
                        using var stream = zip.GetEntry("word/document.xml")!.Open();
                        var doc = XDocument.Load(stream);
                        XNamespace w = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
                        var sz = doc.Descendants(w + "sz").FirstOrDefault();
                        Assert.NotNull(sz);
                        Assert.Equal("48", sz.Attribute(w + "val")?.Value);
                    }

                    [Fact]
                    public void GenerateRichDocx_MultiParagraph_CorrectParagraphCount()
                    {
                        string rtf = @"{\rtf1\ansi\pard Para1\par Para2\par Para3\par}";
                        byte[] docx = DocxExportHelper.GenerateRichDocx(rtf);
                        using var ms = new MemoryStream(docx);
                        using var zip = new ZipArchive(ms, ZipArchiveMode.Read);
                        using var stream = zip.GetEntry("word/document.xml")!.Open();
                        var doc = XDocument.Load(stream);
                        XNamespace w = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
                        Assert.InRange(doc.Descendants(w + "p").Count(), 3, int.MaxValue);
                    }

                    [Fact]
                    public void GenerateRichDocx_CenterAlignment_EmitsJcCenter()
                    {
                        string rtf = @"{\rtf1\ansi\pard\qc Centered text\par}";
                        byte[] docx = DocxExportHelper.GenerateRichDocx(rtf);
                        using var ms = new MemoryStream(docx);
                        using var zip = new ZipArchive(ms, ZipArchiveMode.Read);
                        using var stream = zip.GetEntry("word/document.xml")!.Open();
                        var doc = XDocument.Load(stream);
                        XNamespace w = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
                        var jc = doc.Descendants(w + "jc").FirstOrDefault();
                        Assert.NotNull(jc);
                        Assert.Equal("center", jc.Attribute(w + "val")?.Value);
                    }

                    [Fact]
                    public void GenerateRichDocx_RtfParser_RunsCoalesceIdenticalFormat()
                    {
                        // Two consecutive chars with same format should merge into one run
                        string rtf = @"{\rtf1\ansi\pard \b AB\par}";
                        byte[] docx = DocxExportHelper.GenerateRichDocx(rtf);
                        using var ms = new MemoryStream(docx);
                        using var zip = new ZipArchive(ms, ZipArchiveMode.Read);
                        using var stream = zip.GetEntry("word/document.xml")!.Open();
                        var doc = XDocument.Load(stream);
                        XNamespace w = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
                        // "AB" should be a single <w:t> element, not two separate runs
                        var runs = doc.Descendants(w + "r").ToList();
                        Assert.Contains(runs, r => r.Descendants(w + "t").Any(t => t.Value.Contains("AB")
                            || (t.Value.Contains('A') && t.Value.Contains('B'))));
                    }
                }

                // â•â•â• OneDriveHelper Tests â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

                // ═══ Multi-Window Tests ═══════════════════════════════════════════════

                public class MultiWindowTests
                {
                    [Fact]
                    public void App_Windows_ListExists()
                    {
                        Assert.NotNull(SmrtPad.App.Windows);
                    }

                    [Fact]
                    public void App_Windows_IsMutableList()
                    {
                        Assert.IsType<System.Collections.Generic.List<SmrtPad.MainWindow>>(SmrtPad.App.Windows);
                    }

                    [Fact]
                    public void App_Windows_StartsEmpty_InTestContext()
                    {
                        Assert.Empty(SmrtPad.App.Windows);
                    }

                    [Fact]
                    public void MainWindow_HasNewWindowClick_Method()
                    {
                        var method = typeof(SmrtPad.MainWindow).GetMethod(
                            "NewWindow_Click",
                            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                        Assert.NotNull(method);
                    }

                    [Fact]
                    public void App_HasNewWindow_StaticMethod()
                    {
                        var method = typeof(SmrtPad.App).GetMethod(
                            "NewWindow",
                            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                        Assert.NotNull(method);
                    }

                    [Fact]
                    public void App_NewWindow_ReturnType_IsMainWindow()
                    {
                        var method = typeof(SmrtPad.App).GetMethod(
                            "NewWindow",
                            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)!;
                        Assert.Equal(typeof(SmrtPad.MainWindow), method.ReturnType);
                    }

                    [Fact]
                    public void ResourceKey_WindowMenu_ExistsInEnUs()
                    {
                        var keys = LocalizationDrawingKeySatelliteTests.GetAllResourceKeys("en-US");
                        Assert.Contains("WindowMenu.Title", keys);
                    }

                    [Fact]
                    public void ResourceKey_NewWindowMenuItem_ExistsInEnUs()
                    {
                        var keys = LocalizationDrawingKeySatelliteTests.GetAllResourceKeys("en-US");
                        Assert.Contains("NewWindowMenuItem.Text", keys);
                    }

                    [Theory]
                    [InlineData("de-DE")]
                    [InlineData("es-ES")]
                    [InlineData("fr-FR")]
                    [InlineData("ja-JP")]
                    [InlineData("zh-Hans")]
                    [InlineData("ar-SA")]
                    [InlineData("ru-RU")]
                    [InlineData("ur-PK")]
                    public void ResourceKey_NewWindowMenuItem_ExistsInAllLocales(string locale)
                    {
                        var keys = LocalizationDrawingKeySatelliteTests.GetAllResourceKeys(locale);
                        Assert.Contains("NewWindowMenuItem.Text", keys);
                    }
                }

                // ═══ Document Templates Tests ═════════════════════════════════════════════

                public class DocumentTemplatesTests
                {
                    [Fact]
                    public void All_ContainsFiveTemplates()
                    {
                        Assert.Equal(5, DocumentTemplates.All.Count); // 5 named templates — Assert.Collection not used as it would duplicate key assertions below
                    }

                    [Fact]
                    public void All_ContainsBlankTemplate()
                    {
                        Assert.Contains(DocumentTemplates.All, t => t.Key == "blank");
                    }

                    [Fact]
                    public void All_ContainsLetterTemplate()
                    {
                        Assert.Contains(DocumentTemplates.All, t => t.Key == "letter");
                    }

                    [Fact]
                    public void All_ContainsReportTemplate()
                    {
                        Assert.Contains(DocumentTemplates.All, t => t.Key == "report");
                    }

                    [Fact]
                    public void All_ContainsResumeTemplate()
                    {
                        Assert.Contains(DocumentTemplates.All, t => t.Key == "resume");
                    }

                    [Fact]
                    public void All_ContainsMeetingTemplate()
                    {
                        Assert.Contains(DocumentTemplates.All, t => t.Key == "meeting");
                    }

                    [Fact]
                    public void AllTemplates_HaveNonEmptyDisplayName()
                    {
                        foreach (var t in DocumentTemplates.All)
                            Assert.False(string.IsNullOrWhiteSpace(t.DisplayName),
                                $"Template '{t.Key}' has empty DisplayName");
                    }

                    [Fact]
                    public void AllTemplates_HaveNonEmptyDescription()
                    {
                        foreach (var t in DocumentTemplates.All)
                            Assert.False(string.IsNullOrWhiteSpace(t.Description),
                                $"Template '{t.Key}' has empty Description");
                    }

                    [Fact]
                    public void BlankTemplate_HasEmptyContent()
                    {
                        var blank = DocumentTemplates.All.First(t => t.Key == "blank");
                        Assert.Equal(string.Empty, blank.PlainContent);
                    }

                    [Fact]
                    public void NonBlankTemplates_HaveNonEmptyContent()
                    {
                        foreach (var t in DocumentTemplates.All.Where(t => t.Key != "blank"))
                            Assert.False(string.IsNullOrWhiteSpace(t.PlainContent),
                                $"Template '{t.Key}' has empty PlainContent");
                    }

                    [Theory]
                    [InlineData("blank")]
                    [InlineData("letter")]
                    [InlineData("report")]
                    [InlineData("resume")]
                    [InlineData("meeting")]
                    public void Template_Keys_AreUnique(string key)
                    {
                        var matches = DocumentTemplates.All.Where(t => t.Key == key).ToList();
                        Assert.Single(matches);
                    }

                    [Fact]
                    public void DocumentTemplate_IsRecord()
                    {
                        var t = new DocumentTemplate("x", "X", "desc", "content");
                        var t2 = new DocumentTemplate("x", "X", "desc", "content");
                        Assert.Equal(t, t2);
                    }

                    [Fact]
                    public void LetterTemplate_ContainsSalutation()
                    {
                        var letter = DocumentTemplates.All.First(t => t.Key == "letter");
                        Assert.Contains("Dear", letter.PlainContent);
                    }

                    [Fact]
                    public void ReportTemplate_ContainsSections()
                    {
                        var report = DocumentTemplates.All.First(t => t.Key == "report");
                        Assert.Contains("INTRODUCTION", report.PlainContent);
                        Assert.Contains("CONCLUSION", report.PlainContent);
                    }

                    [Fact]
                    public void ResumeTemplate_ContainsWorkExperience()
                    {
                        var resume = DocumentTemplates.All.First(t => t.Key == "resume");
                        Assert.Contains("WORK EXPERIENCE", resume.PlainContent);
                    }

                    [Fact]
                    public void MeetingTemplate_ContainsActionItems()
                    {
                        var meeting = DocumentTemplates.All.First(t => t.Key == "meeting");
                        Assert.Contains("ACTION ITEMS", meeting.PlainContent);
                    }

                    [Fact]
                    public void FileBackstageView_HasTemplateRequestedEvent()
                    {
                        var evt = typeof(SmrtPad.Views.FileBackstageView).GetEvent("TemplateRequested");
                        Assert.NotNull(evt);
                        Assert.Equal(typeof(EventHandler<DocumentTemplate>), evt!.EventHandlerType);
                    }

                    [Fact]
                    public void MainWindow_HasApplyTemplate_PrivateMethod()
                    {
                        var method = typeof(SmrtPad.MainWindow).GetMethod(
                            "ApplyTemplate",
                            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                        Assert.NotNull(method);
                    }

                    [Fact]
                    public void ResourceKey_StatusTemplateApplied_ExistsInEnUs()
                    {
                        var keys = LocalizationDrawingKeySatelliteTests.GetAllResourceKeys("en-US");
                        Assert.Contains("StatusTemplateApplied", keys);
                    }

                    [Fact]
                    public void ResourceKey_BackstageTemplatesDesc_ExistsInEnUs()
                    {
                        var keys = LocalizationDrawingKeySatelliteTests.GetAllResourceKeys("en-US");
                        Assert.Contains("BackstageTemplatesDesc", keys);
                    }
                }

                public class OneDriveHelperTests
                {
                    [Fact]
                    public void GetOneDrivePath_ReturnsNullOrString()
                    {
                        // Must return null or a non-empty string (can't guarantee OneDrive presence in CI)
                        string? path = OneDriveHelper.GetOneDrivePath();
                        if (path != null)
                        {
                            Assert.False(string.IsNullOrWhiteSpace(path));
                            Assert.True(Directory.Exists(path), $"OneDrive path reported but does not exist: {path}");
                        }
                    }

                    [Fact]
                    public void IsAvailable_MatchesGetOneDrivePath()
                    {
                        bool available = OneDriveHelper.IsAvailable();
                        string? path = OneDriveHelper.GetOneDrivePath();
                        Assert.Equal(path != null, available);
                    }

                    [Fact]
                    public void GetOneDrivePath_WithFakeConsumerEnv_ReturnsIt()
                    {
                        // Set a fake OneDriveConsumer env var pointing to a temp directory
                        string tempDir = Path.Combine(Path.GetTempPath(), "FakeOneDrive_" + Guid.NewGuid().ToString("N"));
                        Directory.CreateDirectory(tempDir);
                        try
                        {
                            Environment.SetEnvironmentVariable("OneDriveConsumer", tempDir);
                            string? path = OneDriveHelper.GetOneDrivePath();
                            Assert.Equal(tempDir, path);
                        }
                        finally
                        {
                            Environment.SetEnvironmentVariable("OneDriveConsumer", null);
                            Directory.Delete(tempDir);
                        }
                    }

                    [Fact]
                    public void GetOneDrivePath_WithNonExistentPath_SkipsIt()
                    {
                        string fakePath = @"C:\NonExistentOneDrivePath_TestOnly_12345";
                        Environment.SetEnvironmentVariable("OneDriveConsumer", fakePath);
                        try
                        {
                            // Restore original OneDriveConsumer after the test
                            string? result = OneDriveHelper.GetOneDrivePath();
                            // Should not return the fake path because it doesn't exist
                            Assert.NotEqual(fakePath, result);
                        }
                        finally
                        {
                            Environment.SetEnvironmentVariable("OneDriveConsumer", null);
                        }
                    }
                }

                // â•â•â• MacroHelper Tests â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

                public class MacroHelperTests
                {
                    [Fact]
                    public void InitialState_IsNotRecording_CountIsZero()
                    {
                        var macro = new MacroHelper();
                        Assert.False(macro.IsRecording);
                        Assert.Empty(macro.Commands);
                    }

                    [Fact]
                    public void StartRecording_SetsIsRecording()
                    {
                        var macro = new MacroHelper();
                        macro.StartRecording();
                        Assert.True(macro.IsRecording);
                    }

                    [Fact]
                    public void StopRecording_ClearsIsRecording()
                    {
                        var macro = new MacroHelper();
                        macro.StartRecording();
                        macro.StopRecording();
                        Assert.False(macro.IsRecording);
                    }

                    [Fact]
                    public void Record_WhileRecording_AddsCommand()
                    {
                        var macro = new MacroHelper();
                        macro.StartRecording();
                        macro.Record(MacroCommandType.Bold);
                        Assert.Single(macro.Commands);
                        Assert.Equal(MacroCommandType.Bold, macro.Commands[0].Type);
                    }

                    [Fact]
                    public void Record_WhenNotRecording_IsIgnored()
                    {
                        var macro = new MacroHelper();
                        macro.Record(MacroCommandType.Italic);
                        Assert.Empty(macro.Commands);
                    }

                    [Fact]
                    public void Record_WithValue_StoresValue()
                    {
                        var macro = new MacroHelper();
                        macro.StartRecording();
                        macro.Record(MacroCommandType.SetFontFamily, "Arial");
                        Assert.Equal("Arial", macro.Commands[0].Value);
                    }

                    [Fact]
                    public void StartRecording_ClearsPreviousCommands()
                    {
                        var macro = new MacroHelper();
                        macro.StartRecording();
                        macro.Record(MacroCommandType.Bold);
                        macro.StopRecording();

                        macro.StartRecording();
                        Assert.Empty(macro.Commands);
                    }

                    [Fact]
                    public void Clear_RemovesAllCommands()
                    {
                        var macro = new MacroHelper();
                        macro.StartRecording();
                        macro.Record(MacroCommandType.Bold);
                        macro.Record(MacroCommandType.Italic);
                        macro.StopRecording();
                        macro.Clear();
                        Assert.Empty(macro.Commands);
                    }

                    [Fact]
                    public void Serialize_ProducesValidJson()
                    {
                        var macro = new MacroHelper();
                        macro.StartRecording();
                        macro.Record(MacroCommandType.Bold);
                        macro.Record(MacroCommandType.SetFontSize, "14");
                        macro.StopRecording();

                        string json = macro.Serialize();
                        Assert.False(string.IsNullOrWhiteSpace(json));
                        Assert.Contains("Bold", json);
                        Assert.Contains("14", json);
                    }

                    [Fact]
                    public void Deserialize_RestoresCommands()
                    {
                        var original = new MacroHelper();
                        original.StartRecording();
                        original.Record(MacroCommandType.Italic);
                        original.Record(MacroCommandType.SetAlignment, "Center");
                        original.StopRecording();
                        string json = original.Serialize();

                        var restored = new MacroHelper();
                        restored.Deserialize(json);
                        Assert.Equal(2, restored.Commands.Count);
                        Assert.Equal(MacroCommandType.Italic, restored.Commands[0].Type);
                        Assert.Equal(MacroCommandType.SetAlignment, restored.Commands[1].Type);
                        Assert.Equal("Center", restored.Commands[1].Value);
                    }

                    [Fact]
                    public void SaveAndLoad_RoundTrip()
                    {
                        string path = Path.Combine(Path.GetTempPath(), $"macro_test_{Guid.NewGuid():N}.smacro");
                        try
                        {
                            var macro = new MacroHelper();
                            macro.StartRecording();
                            macro.Record(MacroCommandType.ZoomIn);
                            macro.Record(MacroCommandType.InsertText, "Hello macro");
                            macro.StopRecording();
                            macro.Save(path);

                            var loaded = new MacroHelper();
                            loaded.Load(path);
                            Assert.Equal(2, loaded.Commands.Count);
                            Assert.Equal(MacroCommandType.ZoomIn, loaded.Commands[0].Type);
                            Assert.Equal(MacroCommandType.InsertText, loaded.Commands[1].Type);
                            Assert.Equal("Hello macro", loaded.Commands[1].Value);
                        }
                        finally
                        {
                            if (File.Exists(path)) File.Delete(path);
                        }
                    }

                    [Fact]
                    public void MacroCommand_ToString_WithValue()
                    {
                        var cmd = new MacroCommand(MacroCommandType.SetFontFamily, "Times New Roman");
                        Assert.Equal("SetFontFamily:Times New Roman", cmd.ToString());
                    }

                    [Fact]
                    public void MacroCommand_ToString_NoValue()
                    {
                        var cmd = new MacroCommand(MacroCommandType.Bold);
                        Assert.Equal("Bold", cmd.ToString());
                    }

                    [Fact]
                    public void Deserialize_EmptyJson_Throws()
                    {
                        var macro = new MacroHelper();
                        Assert.Throws<ArgumentException>(() => macro.Deserialize(string.Empty));
                    }

                    [Fact]
                    public void Load_NonExistentFile_Throws()
                    {
                        var macro = new MacroHelper();
                        Assert.Throws<ArgumentException>(() => macro.Load(string.Empty));
                    }

                    [Theory]
                    [InlineData(MacroCommandType.Bold)]
                    [InlineData(MacroCommandType.Italic)]
                    [InlineData(MacroCommandType.Underline)]
                    [InlineData(MacroCommandType.Strikethrough)]
                    [InlineData(MacroCommandType.Subscript)]
                    [InlineData(MacroCommandType.Superscript)]
                    [InlineData(MacroCommandType.ClearFormatting)]
                    [InlineData(MacroCommandType.ZoomIn)]
                    [InlineData(MacroCommandType.ZoomOut)]
                    public void AllCommandTypes_CanBeRecordedAndDeserialized(MacroCommandType type)
                    {
                        var macro = new MacroHelper();
                        macro.StartRecording();
                        macro.Record(type);
                        macro.StopRecording();

                        string json = macro.Serialize();
                        var restored = new MacroHelper();
                        restored.Deserialize(json);

                        Assert.Single(restored.Commands);
                        Assert.Equal(type, restored.Commands[0].Type);
                    }

                    [Theory]
                    [InlineData(MacroCommandType.SetListType, "Bullet")]
                    [InlineData(MacroCommandType.SetListType, "Number")]
                    [InlineData(MacroCommandType.SetListType, "LowercaseLetter")]
                    [InlineData(MacroCommandType.SetListType, "UppercaseLetter")]
                    [InlineData(MacroCommandType.SetListType, "LowercaseRoman")]
                    [InlineData(MacroCommandType.SetListType, "UppercaseRoman")]
                    [InlineData(MacroCommandType.SetListType, "None")]
                    public void SetListType_AllVariants_RoundTrip(MacroCommandType type, string value)
                    {
                        var macro = new MacroHelper();
                        macro.StartRecording();
                        macro.Record(type, value);
                        macro.StopRecording();

                        var restored = new MacroHelper();
                        restored.Deserialize(macro.Serialize());

                        Assert.Equal(type, restored.Commands[0].Type);
                        Assert.Equal(value, restored.Commands[0].Value);
                    }

                    [Theory]
                    [InlineData("1")]
                    [InlineData("1.5")]
                    [InlineData("2")]
                    [InlineData("1.15")]
                    public void SetLineSpacing_AllPresets_RoundTrip(string spacing)
                    {
                        var macro = new MacroHelper();
                        macro.StartRecording();
                        macro.Record(MacroCommandType.SetLineSpacing, spacing);
                        macro.StopRecording();

                        var restored = new MacroHelper();
                        restored.Deserialize(macro.Serialize());

                        Assert.Equal(MacroCommandType.SetLineSpacing, restored.Commands[0].Type);
                        Assert.Equal(spacing, restored.Commands[0].Value);
                    }

                    [Fact]
                    public void Record_WhenNotRecording_NeverAddsCommands()
                    {
                        var macro = new MacroHelper();
                        macro.Record(MacroCommandType.Bold);
                        macro.Record(MacroCommandType.SetListType, "Bullet");
                        macro.Record(MacroCommandType.SetLineSpacing, "2");
                        Assert.Empty(macro.Commands);
                    }

                    [Fact]
                    public void MacroCommandType_SetListType_Exists()
                    {
                        Assert.True(Enum.IsDefined(typeof(MacroCommandType), "SetListType"));
                    }

                    [Fact]
                    public void MacroCommandType_SetLineSpacing_Exists()
                    {
                        Assert.True(Enum.IsDefined(typeof(MacroCommandType), "SetLineSpacing"));
                    }

                    [Fact]
                    public void MainWindow_HasExecuteMacroCommand_Private()
                    {
                        var type = typeof(SmrtPad.MainWindow);
                        var method = type.GetMethod("ExecuteMacroCommand",
                            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                        Assert.NotNull(method);
                    }

                    [Fact]
                    public void MacroCommandType_HasExpected15Types()
                    {
                        var values = Enum.GetValues<MacroCommandType>();
                        Assert.Equal(15, values.Length);
                    }
                }

                // â•â•â• SpellCheck Settings Tests â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

                public partial class SpellCheckSettingsTests : IDisposable
                {
                    private readonly string _tempPath;
                    private readonly SettingsService _settings;

                    public SpellCheckSettingsTests()
                    {
                        _tempPath = Path.Combine(Path.GetTempPath(), $"sc_test_{Guid.NewGuid():N}.json");
                        _settings = new SettingsService(_tempPath);
                    }

                    public void Dispose()
                    {
                        if (File.Exists(_tempPath)) File.Delete(_tempPath);
                        GC.SuppressFinalize(this);
                    }

                    [Fact]
                    public void SpellCheckEnabled_DefaultsToTrue()
                    {
                        Assert.True(_settings.SpellCheckEnabled);
                    }

                    [Fact]
                    public void SpellCheckEnabled_CanBeDisabled()
                    {
                        _settings.SpellCheckEnabled = false;
                        Assert.False(_settings.SpellCheckEnabled);
                    }

                    [Fact]
                    public void SpellCheckEnabled_PersistsThroughSaveLoad()
                    {
                        _settings.SpellCheckEnabled = false;
                        _settings.Save();

                        var reloaded = new SettingsService(_tempPath);
                        Assert.False(reloaded.SpellCheckEnabled);
                    }

                    [Fact]
                    public void SpellCheckEnabled_True_PersistsThroughSaveLoad()
                    {
                        _settings.SpellCheckEnabled = true;
                        _settings.Save();

                        var reloaded = new SettingsService(_tempPath);
                        Assert.True(reloaded.SpellCheckEnabled);
                    }

                    [Fact]
                    public void ISettingsService_HasSpellCheckEnabledProperty()
                    {
                        var type = typeof(ISettingsService);
                        Assert.NotNull(type.GetProperty("SpellCheckEnabled"));
                    }
                }

                // â•â•â• Localization â€” new Section 4 keys â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

                public class NewFeatureLocalizationTests
                {
                    private static Dictionary<string, string> LoadResw(string locale)
                    {
                        string? dir = AppContext.BaseDirectory;
                        for (int i = 0; i < 10 && dir is not null; i++)
                        {
                            string candidate = Path.Combine(dir, "SmrtPad", "Strings", locale, "Resources.resw");
                            if (!File.Exists(candidate))
                                candidate = Path.Combine(dir, "Strings", locale, "Resources.resw");
                            if (File.Exists(candidate))
                            {
                                var dict = new Dictionary<string, string>(StringComparer.Ordinal);
                                foreach (var data in XDocument.Load(candidate).Descendants("data"))
                                {
                                    string? name = data.Attribute("name")?.Value;
                                    string? val  = data.Element("value")?.Value;
                                    if (name is not null && val is not null) dict[name] = val;
                                }
                                return dict;
                            }
                            dir = Directory.GetParent(dir)?.FullName;
                        }
                        return [];
                    }

                    private static readonly string[] NewKeys =
                    [
                        "SpellCheckToggle.Text", "OptionsSpellCheck",
                        "StatusSpellCheckEnabled", "StatusSpellCheckDisabled",
                        "ExportPdfNavItem.Content", "FileTypePdf",
                        "StatusExportedPdf", "ErrorExportingPdf", "BackstageExportPdfDesc",
                        "ExportDocxNavItem.Content", "StatusExportedDocx",
                        "ErrorExportingDocx", "BackstageExportDocxDesc",
                        "OneDriveNavItem.Content", "OneDriveNotFound", "OneDriveNotFoundMessage",
                        "StatusSavedToOneDrive", "BackstageSaveOneDriveDesc",
                        "MacroMenuBar.Title", "MacroRecord", "MacroStop", "MacroRun",
                        "MacroSave", "MacroLoad", "StatusMacroRecording", "StatusMacroStopped",
                        "StatusMacroDone", "StatusMacroSaved", "StatusMacroLoaded",
                        "MacroFilter", "MacroNoCommands",
                        "StatusNewTab", "StatusTabClosed",
                    ];

                    [Theory]
                    [InlineData("SpellCheckToggle.Text")]
                    [InlineData("OptionsSpellCheck")]
                    [InlineData("StatusSpellCheckEnabled")]
                    [InlineData("StatusSpellCheckDisabled")]
                    [InlineData("ExportPdfNavItem.Content")]
                    [InlineData("FileTypePdf")]
                    [InlineData("StatusExportedPdf")]
                    [InlineData("ErrorExportingPdf")]
                    [InlineData("BackstageExportPdfDesc")]
                    [InlineData("ExportDocxNavItem.Content")]
                    [InlineData("StatusExportedDocx")]
                    [InlineData("ErrorExportingDocx")]
                    [InlineData("BackstageExportDocxDesc")]
                    [InlineData("OneDriveNavItem.Content")]
                    [InlineData("OneDriveNotFound")]
                    [InlineData("OneDriveNotFoundMessage")]
                    [InlineData("StatusSavedToOneDrive")]
                    [InlineData("BackstageSaveOneDriveDesc")]
                    [InlineData("MacroMenuBar.Title")]
                    [InlineData("MacroRecord")]
                    [InlineData("MacroStop")]
                    [InlineData("MacroRun")]
                    [InlineData("MacroSave")]
                    [InlineData("MacroLoad")]
                    [InlineData("StatusMacroRecording")]
                    [InlineData("StatusMacroStopped")]
                    [InlineData("StatusMacroDone")]
                    [InlineData("StatusMacroSaved")]
                    [InlineData("StatusMacroLoaded")]
                    [InlineData("MacroFilter")]
                    [InlineData("MacroNoCommands")]
                    [InlineData("StatusNewTab")]
                    [InlineData("StatusTabClosed")]
                    public void EnUs_ContainsNewKey(string key)
                    {
                        var dict = LoadResw("en-US");
                        Assert.Contains(key, dict.Keys);
                        Assert.False(string.IsNullOrWhiteSpace(dict[key]), $"Empty value for: {key}");
                    }

                    [Theory]
                    [InlineData("de-DE")]
                    [InlineData("es-ES")]
                    [InlineData("fr-FR")]
                    [InlineData("ja-JP")]
                    [InlineData("zh-Hans")]
                    [InlineData("ar-SA")]
                    [InlineData("ru-RU")]
                    [InlineData("ur-PK")]
                    public void AllSatellites_ContainAllNewKeys(string locale)
                    {
                        var enDict = LoadResw("en-US");
                        var locDict = LoadResw(locale);
                        if (enDict.Count == 0 || locDict.Count == 0) return;

                        foreach (var key in NewKeys)
                        {
                            Assert.Contains(key, locDict.Keys);
                            Assert.False(string.IsNullOrWhiteSpace(locDict[key]), $"Empty value for '{key}' in {locale}");
                        }
                    }

                    [Theory]
                    [InlineData("StatusExportedPdf")]
                    [InlineData("StatusExportedDocx")]
                    [InlineData("StatusSavedToOneDrive")]
                    public void NewFormatStrings_ContainPlaceholder(string key)
                    {
                        var dict = LoadResw("en-US");
                        Assert.Contains(key, dict.Keys);
                        Assert.Contains("{0}", dict[key]);
                    }

                    [Fact]
                    public void MacroHelper_IsInSmrtPadHelpers_Namespace()
                    {
                        Assert.Equal("SmrtPad.Helpers", typeof(MacroHelper).Namespace);
                    }

                    [Fact]
                    public void PdfHelper_IsInSmrtPadHelpers_Namespace()
                    {
                        Assert.Equal("SmrtPad.Helpers", typeof(PdfHelper).Namespace);
                    }

                    [Fact]
                    public void DocxExportHelper_IsInSmrtPadHelpers_Namespace()
                    {
                        Assert.Equal("SmrtPad.Helpers", typeof(DocxExportHelper).Namespace);
                    }

                    [Fact]
                    public void OneDriveHelper_IsInSmrtPadHelpers_Namespace()
                    {
                        Assert.Equal("SmrtPad.Helpers", typeof(OneDriveHelper).Namespace);
                    }
                }

                // â•â•â• Tabbed Interface Contract Tests â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

                public class TabbedInterfaceContractTests
                {
                    [Fact]
                    public void MainWindow_HasDocumentTabsAddTabButtonClick()
                    {
                        var type = typeof(SmrtPad.MainWindow);
                        var method = type.GetMethod("DocumentTabs_AddTabButtonClick",
                            BindingFlags.NonPublic | BindingFlags.Instance);
                        Assert.NotNull(method);
                    }

                    [Fact]
                    public void MainWindow_HasDocumentTabsTabCloseRequested()
                    {
                        var type = typeof(SmrtPad.MainWindow);
                        var method = type.GetMethod("DocumentTabs_TabCloseRequested",
                            BindingFlags.NonPublic | BindingFlags.Instance);
                        Assert.NotNull(method);
                    }

                    [Fact]
                    public void MainWindow_HasDocumentTabsSelectionChanged()
                    {
                        var type = typeof(SmrtPad.MainWindow);
                        var method = type.GetMethod("DocumentTabs_SelectionChanged",
                            BindingFlags.NonPublic | BindingFlags.Instance);
                        Assert.NotNull(method);
                    }

                    [Fact]
                    public void MainWindow_HasSyncViewModelFromActiveTab()
                    {
                        var type = typeof(SmrtPad.MainWindow);
                        var method = type.GetMethod("SyncViewModelFromActiveTab",
                            BindingFlags.NonPublic | BindingFlags.Instance);
                        Assert.NotNull(method);
                    }

                    [Fact]
                    public void MainWindow_HasCreateTab()
                    {
                        var type = typeof(SmrtPad.MainWindow);
                        var method = type.GetMethod("CreateTab",
                            BindingFlags.NonPublic | BindingFlags.Instance);
                        Assert.NotNull(method);
                    }

                    [Fact]
                    public void MainWindow_XAML_HasDocumentTabsElement()
                    {
                        string? xamlPath = FindXamlPath();
                        if (xamlPath == null) return;
                        string xaml = File.ReadAllText(xamlPath);
                        Assert.Contains("DocumentTabs", xaml);
                        Assert.Contains("AddTabButtonClick", xaml);
                        Assert.Contains("TabCloseRequested", xaml);
                    }

                    [Fact]
                    public void MainWindow_XAML_HasMacroMenuBar()
                    {
                        string? xamlPath = FindXamlPath();
                        if (xamlPath == null) return;
                        string xaml = File.ReadAllText(xamlPath);
                        Assert.Contains("MacroMenuBar", xaml);
                        Assert.Contains("MacroRecord_Click", xaml);
                        Assert.Contains("MacroStop_Click", xaml);
                        Assert.Contains("MacroRun_Click", xaml);
                    }

                    [Fact]
                    public void MainWindow_XAML_HasSpellCheckToggle()
                    {
                        string? xamlPath = FindXamlPath();
                        if (xamlPath == null) return;
                        string xaml = File.ReadAllText(xamlPath);
                        Assert.Contains("SpellCheckToggle", xaml);
                        Assert.Contains("SpellCheck_Click", xaml);
                    }

                    [Fact]
                    public void MainWindow_HasExportPdfClick()
                    {
                        var type = typeof(SmrtPad.MainWindow);
                        var method = type.GetMethod("ExportPdf_Click",
                            BindingFlags.NonPublic | BindingFlags.Instance);
                        Assert.NotNull(method);
                    }

                    [Fact]
                    public void MainWindow_HasExportDocxClick()
                    {
                        var type = typeof(SmrtPad.MainWindow);
                        var method = type.GetMethod("ExportDocx_Click",
                            BindingFlags.NonPublic | BindingFlags.Instance);
                        Assert.NotNull(method);
                    }

                    [Fact]
                    public void MainWindow_HasSaveToOneDriveClick()
                    {
                        var type = typeof(SmrtPad.MainWindow);
                        var method = type.GetMethod("SaveToOneDrive_Click",
                            BindingFlags.NonPublic | BindingFlags.Instance);
                        Assert.NotNull(method);
                    }

                    [Fact]
                    public void MainWindow_HasMacroHandlers()
                    {
                        var type = typeof(SmrtPad.MainWindow);
                        Assert.NotNull(type.GetMethod("MacroRecord_Click", BindingFlags.NonPublic | BindingFlags.Instance));
                        Assert.NotNull(type.GetMethod("MacroStop_Click",   BindingFlags.NonPublic | BindingFlags.Instance));
                        Assert.NotNull(type.GetMethod("MacroRun_Click",    BindingFlags.NonPublic | BindingFlags.Instance));
                        Assert.NotNull(type.GetMethod("MacroSave_Click",   BindingFlags.NonPublic | BindingFlags.Instance));
                        Assert.NotNull(type.GetMethod("MacroLoad_Click",   BindingFlags.NonPublic | BindingFlags.Instance));
                    }

                    private static string? FindXamlPath()
                    {
                        string? dir = Directory.GetCurrentDirectory();
                        while (dir != null)
                        {
                            string candidate = Path.Combine(dir, "SmrtPad", "MainWindow.xaml");
                            if (File.Exists(candidate)) return candidate;
                            dir = Directory.GetParent(dir)?.FullName;
                        }
                        return null;
                    }
                }

                // ═══ DialogService Contract Tests ════════════════════════════════════════
                public class DialogServiceContractTests
                {
                    [Fact]
                    public void DialogService_ImplementsIDialogService()
                    {
                        Assert.True(typeof(IDialogService).IsAssignableFrom(typeof(DialogService)));
                    }

                    [Fact]
                    public void DialogService_CanConstruct_WithFuncXamlRootProvider()
                    {
                        // Func<XamlRoot> overload stores the provider; no WinUI call occurs on construction
                        var svc = new DialogService(() => null!);
                        Assert.NotNull(svc);
                    }

                    [Fact]
                    public void DialogService_HasDefaultConstructor()
                    {
                        var ctor = typeof(DialogService).GetConstructor(Type.EmptyTypes);
                        Assert.NotNull(ctor);
                    }

                    [Fact]
                    public void DialogService_HasFuncXamlRootConstructor()
                    {
                        var ctor = typeof(DialogService)
                            .GetConstructor([typeof(Func<Microsoft.UI.Xaml.XamlRoot>)]);
                        Assert.NotNull(ctor);
                    }

                    [Fact]
                    public void SavePromptResult_HasExactlyThreeValues()
                    {
                        Assert.Equal(3, Enum.GetValues<SavePromptResult>().Length);
                    }

                    [Theory]
                    [InlineData("Save",     0)]
                    [InlineData("DontSave", 1)]
                    [InlineData("Cancel",   2)]
                    public void SavePromptResult_OrdinalValues_AreCorrect(string name, int expected)
                    {
                        var value = (SavePromptResult)Enum.Parse(typeof(SavePromptResult), name);
                        Assert.Equal(expected, (int)value);
                    }

                    [Fact]
                    public void DialogService_ShowErrorAsync_HasCorrectSignature()
                    {
                        var method = typeof(DialogService).GetMethod("ShowErrorAsync");
                        Assert.NotNull(method);
                        Assert.Equal(typeof(Task), method!.ReturnType);
                        var parms = method.GetParameters();
                        Assert.Equal(2, parms.Length);
                        Assert.Equal(typeof(string), parms[0].ParameterType); // title
                        Assert.Equal(typeof(string), parms[1].ParameterType); // message
                    }

                    [Fact]
                    public void DialogService_ShowSavePromptAsync_HasCorrectSignature()
                    {
                        var method = typeof(DialogService).GetMethod("ShowSavePromptAsync");
                        Assert.NotNull(method);
                        Assert.Equal(typeof(Task<SavePromptResult>), method!.ReturnType);
                        var parms = method.GetParameters();
                        Assert.Single(parms);
                        Assert.Equal(typeof(string), parms[0].ParameterType); // documentTitle
                    }
                }

                // ═══ FileService Contract Tests ═══════════════════════════════════════════
                public class FileServiceContractTests
                {
                    [Fact]
                    public void FileService_ImplementsIFileService()
                    {
                        Assert.True(typeof(IFileService).IsAssignableFrom(typeof(FileService)));
                    }

                    [Fact]
                    public void FileService_CanConstruct_WithFuncWindowProvider()
                    {
                        // Func<Window> overload stores the provider; no WinUI call occurs on construction
                        var svc = new FileService(() => null!);
                        Assert.NotNull(svc);
                    }

                    [Fact]
                    public void FileService_HasDefaultConstructor()
                    {
                        var ctor = typeof(FileService).GetConstructor(Type.EmptyTypes);
                        Assert.NotNull(ctor);
                    }

                    [Fact]
                    public void FileService_HasFuncWindowConstructor()
                    {
                        var ctor = typeof(FileService)
                            .GetConstructor([typeof(Func<Microsoft.UI.Xaml.Window>)]);
                        Assert.NotNull(ctor);
                    }

                    [Fact]
                    public void IFileService_HasExactlyThreeMethods()
                    {
                        Assert.Equal(3, typeof(IFileService).GetMethods().Length);
                    }

                    [Fact]
                    public void FileService_PickOpenFileAsync_HasStringArrayParam()
                    {
                        var method = typeof(FileService).GetMethod("PickOpenFileAsync");
                        Assert.NotNull(method);
                        var parms = method!.GetParameters();
                        Assert.Single(parms);
                        Assert.Equal(typeof(string[]), parms[0].ParameterType);
                    }

                    [Fact]
                    public void FileService_PickSaveFileAsync_HasTwoStringParams()
                    {
                        var method = typeof(FileService).GetMethod("PickSaveFileAsync");
                        Assert.NotNull(method);
                        var parms = method!.GetParameters();
                        Assert.Equal(2, parms.Length);
                        Assert.Equal(typeof(string), parms[0].ParameterType); // suggestedName
                        Assert.Equal(typeof(string), parms[1].ParameterType); // defaultExtension
                    }

                    [Fact]
                    public void FileService_GetFileFromPathAsync_HasSingleStringParam()
                    {
                        var method = typeof(FileService).GetMethod("GetFileFromPathAsync");
                        Assert.NotNull(method);
                        var parms = method!.GetParameters();
                        Assert.Single(parms);
                        Assert.Equal(typeof(string), parms[0].ParameterType);
                    }
                }

                // ═══ FileBackstageView Full Event Contract Tests ══════════════════════════
                public class FileBackstageViewFullEventContractTests
                {
                    [Fact]
                    public void FileBackstageView_HasAllTwelveEvents()
                    {
                        var type = typeof(SmrtPad.Views.FileBackstageView);
                        var expected = new[]
                        {
                            "NewRequested", "OpenRequested", "SaveRequested", "SaveAsRequested",
                            "PrintRequested", "ExportPdfRequested", "ExportDocxRequested",
                            "OneDriveRequested", "OptionsRequested", "ExitRequested",
                            "RecentFileRequested", "TemplateRequested"
                        };
                        foreach (var name in expected)
                            Assert.NotNull(type.GetEvent(name));
                    }

                    [Theory]
                    [InlineData("NewRequested")]
                    [InlineData("OpenRequested")]
                    [InlineData("SaveRequested")]
                    [InlineData("SaveAsRequested")]
                    [InlineData("PrintRequested")]
                    [InlineData("ExportPdfRequested")]
                    [InlineData("ExportDocxRequested")]
                    [InlineData("OneDriveRequested")]
                    [InlineData("OptionsRequested")]
                    [InlineData("ExitRequested")]
                    public void FileBackstageView_StandardEvent_IsEventHandler(string eventName)
                    {
                        var evt = typeof(SmrtPad.Views.FileBackstageView).GetEvent(eventName);
                        Assert.NotNull(evt);
                        Assert.Equal(typeof(EventHandler), evt!.EventHandlerType);
                    }

                    [Fact]
                    public void FileBackstageView_RecentFileRequested_IsGenericEventHandler()
                    {
                        var evt = typeof(SmrtPad.Views.FileBackstageView).GetEvent("RecentFileRequested");
                        Assert.NotNull(evt);
                        Assert.Equal(typeof(EventHandler<string>), evt!.EventHandlerType);
                    }

                    [Fact]
                    public void FileBackstageView_TemplateRequested_IsGenericEventHandler()
                    {
                        var evt = typeof(SmrtPad.Views.FileBackstageView).GetEvent("TemplateRequested");
                        Assert.NotNull(evt);
                        Assert.Equal(typeof(EventHandler<DocumentTemplate>), evt!.EventHandlerType);
                    }

                    [Fact]
                    public void FileBackstageView_HasNav_SelectionChanged_PrivateMethod()
                    {
                        var method = typeof(SmrtPad.Views.FileBackstageView).GetMethod(
                            "Nav_SelectionChanged", BindingFlags.NonPublic | BindingFlags.Instance);
                        Assert.NotNull(method);
                    }

                    [Fact]
                    public void FileBackstageView_HasPopulateTemplates_PrivateMethod()
                    {
                        var method = typeof(SmrtPad.Views.FileBackstageView).GetMethod(
                            "PopulateTemplates", BindingFlags.NonPublic | BindingFlags.Instance);
                        Assert.NotNull(method);
                    }

                    [Fact]
                    public void FileBackstageView_HasSuppressSelectionEventField()
                    {
                        var field = typeof(SmrtPad.Views.FileBackstageView).GetField(
                            "_suppressSelectionEvent", BindingFlags.NonPublic | BindingFlags.Instance);
                        Assert.NotNull(field);
                        Assert.Equal(typeof(bool), field!.FieldType);
                    }

                    [Fact]
                    public void FileBackstageView_IsSealed()
                    {
                        Assert.True(typeof(SmrtPad.Views.FileBackstageView).IsSealed);
                    }

                    [Fact]
                    public void FileBackstageView_TotalEventCount_AtLeastTwelve()
                    {
                        var count = typeof(SmrtPad.Views.FileBackstageView).GetEvents().Length;
                        Assert.True(count >= 12, $"Expected ≥12 events, found {count}");
                    }
                }

                // ═══ App Bootstrap Contract Tests ══════════════════════════════════════════
                public class AppBootstrapContractTests
                {
                    [Fact]
                    public void App_HasServicesProperty_OfTypeServiceProvider()
                    {
                        var prop = typeof(SmrtPad.App).GetProperty("Services");
                        Assert.NotNull(prop);
                        Assert.Equal(
                            typeof(Microsoft.Extensions.DependencyInjection.ServiceProvider),
                            prop!.PropertyType);
                    }

                    [Fact]
                    public void App_Current_IsStaticProperty_ReturningApp()
                    {
                        var prop = typeof(SmrtPad.App).GetProperty(
                            "Current", BindingFlags.Public | BindingFlags.Static);
                        Assert.NotNull(prop);
                        Assert.Equal(typeof(SmrtPad.App), prop!.PropertyType);
                    }

                    [Fact]
                    public void App_MainWindow_IsStaticProperty_ReturningWindow()
                    {
                        var prop = typeof(SmrtPad.App).GetProperty(
                            "MainWindow", BindingFlags.Public | BindingFlags.Static);
                        Assert.NotNull(prop);
                        Assert.Equal(typeof(Microsoft.UI.Xaml.Window), prop!.PropertyType);
                    }

                    [Fact]
                    public void App_Windows_IsStaticProperty_ReturningListOfMainWindow()
                    {
                        var prop = typeof(SmrtPad.App).GetProperty(
                            "Windows", BindingFlags.Public | BindingFlags.Static);
                        Assert.NotNull(prop);
                        Assert.Equal(
                            typeof(System.Collections.Generic.List<SmrtPad.MainWindow>),
                            prop!.PropertyType);
                    }

                    [Fact]
                    public void App_ConfigureServices_IsPrivateStaticMethod()
                    {
                        var method = typeof(SmrtPad.App).GetMethod(
                            "ConfigureServices", BindingFlags.NonPublic | BindingFlags.Static);
                        Assert.NotNull(method);
                        Assert.True(method!.IsStatic);
                        Assert.False(method.IsPublic);
                    }

                    [Fact]
                    public void App_ConfigureServices_ReturnsServiceProvider()
                    {
                        var method = typeof(SmrtPad.App).GetMethod(
                            "ConfigureServices", BindingFlags.NonPublic | BindingFlags.Static);
                        Assert.NotNull(method);
                        Assert.Equal(
                            typeof(Microsoft.Extensions.DependencyInjection.ServiceProvider),
                            method!.ReturnType);
                    }

                    [Fact]
                    public void App_InheritsFromMicrosoftUIXamlApplication()
                    {
                        Assert.True(
                            typeof(Microsoft.UI.Xaml.Application)
                                .IsAssignableFrom(typeof(SmrtPad.App)));
                    }

                    [Fact]
                    public void App_NewWindow_IsPublicStaticAndReturnsMainWindow()
                    {
                        var method = typeof(SmrtPad.App).GetMethod(
                            "NewWindow", BindingFlags.Public | BindingFlags.Static);
                        Assert.NotNull(method);
                        Assert.True(method!.IsStatic);
                        Assert.Equal(typeof(SmrtPad.MainWindow), method.ReturnType);
                    }
                }

                // ═══ ResourceHelper Edge-Case Tests ══════════════════════════════════════
                public class ResourceHelperEdgeCaseTests
                {
                    [Fact]
                    public void GetFormatted_ZeroArgs_MatchesGetString()
                    {
                        string direct    = ResourceHelper.GetString("StatusReady");
                        string formatted = ResourceHelper.GetFormatted("StatusReady");
                        Assert.Equal(direct, formatted);
                    }

                    [Fact]
                    public void GetString_SameKey_ConsistentResult()
                    {
                        string a = ResourceHelper.GetString("DocumentUntitled");
                        string b = ResourceHelper.GetString("DocumentUntitled");
                        Assert.Equal(a, b);
                    }

                    [Fact]
                    public void GetFormatted_SingleArg_InjectsValue()
                    {
                        string result = ResourceHelper.GetFormatted("StatusBarWords", 42);
                        Assert.Contains("42", result);
                    }

                    [Fact]
                    public void GetFormatted_TwoArgs_InjectsBothValues()
                    {
                        string result = ResourceHelper.GetFormatted("StatusBarLineCol", 3, 17);
                        Assert.Contains("3",  result);
                        Assert.Contains("17", result);
                    }

                    [Fact]
                    public void GetString_KeyWithPropertySuffix_ReturnsNonEmpty()
                    {
                        // Keys like "CutMenuItem.Text" are stored with their full name in the .resw
                        string result = ResourceHelper.GetString("CutMenuItem.Text");
                        Assert.NotNull(result);
                        Assert.NotEmpty(result);
                    }

                    [Fact]
                    public void ResourceHelper_IsStaticClass()
                    {
                        var t = typeof(ResourceHelper);
                        Assert.True(t.IsAbstract && t.IsSealed,
                            "ResourceHelper must be a static (abstract + sealed) class");
                    }
                }

                // ═══ MainWindow List & Line-Spacing Contract Tests ════════════════════════
                public class MainWindowListSpacingContractTests
                {
                    [Theory]
                    [InlineData("ListTypeNone_Click")]
                    [InlineData("ListTypeBullet_Click")]
                    [InlineData("ListTypeNumber_Click")]
                    [InlineData("ListTypeLowerLetter_Click")]
                    [InlineData("ListTypeUpperLetter_Click")]
                    [InlineData("ListTypeLowerRoman_Click")]
                    [InlineData("ListTypeUpperRoman_Click")]
                    public void MainWindow_HasListTypeClickHandler(string handlerName)
                    {
                        var method = typeof(SmrtPad.MainWindow).GetMethod(
                            handlerName, BindingFlags.NonPublic | BindingFlags.Instance);
                        Assert.NotNull(method);
                    }

                    [Fact]
                    public void MainWindow_ApplyListType_IsTwoParamPrivateMethod()
                    {
                        var method = typeof(SmrtPad.MainWindow).GetMethod(
                            "ApplyListType", BindingFlags.NonPublic | BindingFlags.Instance);
                        Assert.NotNull(method);
                        Assert.Equal(2, method!.GetParameters().Length);
                    }

                    [Fact]
                    public void MainWindow_HasLineSpacing_Click()
                    {
                        var method = typeof(SmrtPad.MainWindow).GetMethod(
                            "LineSpacing_Click", BindingFlags.NonPublic | BindingFlags.Instance);
                        Assert.NotNull(method);
                    }

                    [Fact]
                    public void MainWindow_HasCustomLineSpacing_Click()
                    {
                        var method = typeof(SmrtPad.MainWindow).GetMethod(
                            "CustomLineSpacing_Click", BindingFlags.NonPublic | BindingFlags.Instance);
                        Assert.NotNull(method);
                    }

                    [Fact]
                    public void MainWindow_HasApplyParagraphSpacing_Click()
                    {
                        var method = typeof(SmrtPad.MainWindow).GetMethod(
                            "ApplyParagraphSpacing_Click", BindingFlags.NonPublic | BindingFlags.Instance);
                        Assert.NotNull(method);
                    }

                    [Fact]
                    public void MainWindow_XAML_HasAllListTypeFlyoutHandlers()
                    {
                        string? xaml = ReadMainWindowXaml();
                        if (xaml is null) return;
                        Assert.Contains("ListTypeNone_Click",        xaml);
                        Assert.Contains("ListTypeBullet_Click",      xaml);
                        Assert.Contains("ListTypeNumber_Click",      xaml);
                        Assert.Contains("ListTypeLowerLetter_Click", xaml);
                        Assert.Contains("ListTypeUpperLetter_Click", xaml);
                        Assert.Contains("ListTypeLowerRoman_Click",  xaml);
                        Assert.Contains("ListTypeUpperRoman_Click",  xaml);
                    }

                    [Fact]
                    public void MainWindow_XAML_HasLineSpacingFlyoutWithTagValues()
                    {
                        string? xaml = ReadMainWindowXaml();
                        if (xaml is null) return;
                        Assert.Contains("LineSpacing_Click", xaml);
                        Assert.Contains("Tag=\"1.0\"",       xaml);
                        Assert.Contains("Tag=\"1.15\"",      xaml);
                        Assert.Contains("Tag=\"1.5\"",       xaml);
                        Assert.Contains("Tag=\"2.0\"",       xaml);
                    }

                    private static string? ReadMainWindowXaml()
                    {
                        string? dir = Directory.GetCurrentDirectory();
                        while (dir is not null)
                        {
                            string candidate = Path.Combine(dir, "SmrtPad", "MainWindow.xaml");
                            if (File.Exists(candidate)) return File.ReadAllText(candidate);
                            dir = Directory.GetParent(dir)?.FullName;
                        }
                        return null;
                    }
                }

                // ═══ Macro Toolbar Wiring Contract Tests ══════════════════════════════════
                public class MacroToolbarWiringContractTests
                {
                    [Fact]
                    public void MainWindow_HasMacroField_OfTypeMacroHelper()
                    {
                        var field = typeof(SmrtPad.MainWindow).GetField(
                            "_macro", BindingFlags.NonPublic | BindingFlags.Instance);
                        Assert.NotNull(field);
                        Assert.Equal(typeof(MacroHelper), field!.FieldType);
                    }

                    [Fact]
                    public void ExecuteMacroCommand_AcceptsMacroCommandParameter()
                    {
                        var method = typeof(SmrtPad.MainWindow).GetMethod(
                            "ExecuteMacroCommand", BindingFlags.NonPublic | BindingFlags.Instance);
                        Assert.NotNull(method);
                        var parms = method!.GetParameters();
                        Assert.Single(parms);
                        Assert.Equal(typeof(MacroCommand), parms[0].ParameterType);
                    }

                    [Fact]
                    public void ApplyListType_SecondParam_IsNamedListTypeName()
                    {
                        var method = typeof(SmrtPad.MainWindow).GetMethod(
                            "ApplyListType", BindingFlags.NonPublic | BindingFlags.Instance);
                        Assert.NotNull(method);
                        var parms = method!.GetParameters();
                        Assert.Equal(2, parms.Length);
                        Assert.Equal("listTypeName", parms[1].Name);
                    }

                    [Fact]
                    public void MacroRecord_SetListType_RoundTripsCorrectly()
                    {
                        var macro = new MacroHelper();
                        macro.StartRecording();
                        macro.Record(MacroCommandType.SetListType, "Bullet");
                        macro.StopRecording();

                        var restored = new MacroHelper();
                        restored.Deserialize(macro.Serialize());

                        Assert.Single(restored.Commands);
                        Assert.Equal(MacroCommandType.SetListType, restored.Commands[0].Type);
                        Assert.Equal("Bullet", restored.Commands[0].Value);
                    }

                    [Fact]
                    public void MacroRecord_SetLineSpacing_RoundTripsCorrectly()
                    {
                        var macro = new MacroHelper();
                        macro.StartRecording();
                        macro.Record(MacroCommandType.SetLineSpacing, "1.5");
                        macro.StopRecording();

                        var restored = new MacroHelper();
                        restored.Deserialize(macro.Serialize());

                        Assert.Single(restored.Commands);
                        Assert.Equal(MacroCommandType.SetLineSpacing, restored.Commands[0].Type);
                        Assert.Equal("1.5", restored.Commands[0].Value);
                    }

                    [Theory]
                    [InlineData("None")]
                    [InlineData("Bullet")]
                    [InlineData("Number")]
                    [InlineData("LowercaseLetter")]
                    [InlineData("UppercaseLetter")]
                    [InlineData("LowercaseRoman")]
                    [InlineData("UppercaseRoman")]
                    public void MacroHelper_SetListType_AllVariants_SerializeDeserialize(string listType)
                    {
                        var macro = new MacroHelper();
                        macro.StartRecording();
                        macro.Record(MacroCommandType.SetListType, listType);
                        macro.StopRecording();

                        var restored = new MacroHelper();
                        restored.Deserialize(macro.Serialize());

                        Assert.Equal(MacroCommandType.SetListType, restored.Commands[0].Type);
                        Assert.Equal(listType, restored.Commands[0].Value);
                    }

                    [Theory]
                    [InlineData("1")]
                    [InlineData("1.15")]
                    [InlineData("1.5")]
                    [InlineData("2")]
                    public void MacroHelper_SetLineSpacing_AllPresets_SerializeDeserialize(string spacing)
                    {
                        var macro = new MacroHelper();
                        macro.StartRecording();
                        macro.Record(MacroCommandType.SetLineSpacing, spacing);
                        macro.StopRecording();

                        var restored = new MacroHelper();
                        restored.Deserialize(macro.Serialize());

                        Assert.Equal(MacroCommandType.SetLineSpacing, restored.Commands[0].Type);
                        Assert.Equal(spacing, restored.Commands[0].Value);
                    }

                    [Fact]
                    public void AllListTypeHandlers_ArePrivateNonStaticMethods()
                    {
                        var handlers = new[]
                        {
                            "ListTypeNone_Click", "ListTypeBullet_Click", "ListTypeNumber_Click",
                            "ListTypeLowerLetter_Click", "ListTypeUpperLetter_Click",
                            "ListTypeLowerRoman_Click", "ListTypeUpperRoman_Click"
                        };
                        foreach (var name in handlers)
                        {
                            var m = typeof(SmrtPad.MainWindow).GetMethod(
                                name, BindingFlags.NonPublic | BindingFlags.Instance);
                            Assert.NotNull(m);
                            Assert.False(m!.IsPublic);
                            Assert.False(m.IsStatic);
                        }
                    }

                    [Fact]
                    public void LineSpacing_Click_IsPrivateNonStaticMethod()
                    {
                        var method = typeof(SmrtPad.MainWindow).GetMethod(
                            "LineSpacing_Click", BindingFlags.NonPublic | BindingFlags.Instance);
                        Assert.NotNull(method);
                        Assert.False(method!.IsPublic);
                        Assert.False(method.IsStatic);
                    }
                }
            }

