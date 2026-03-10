using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace SmrtPad.Services
{
    public class SettingsService : ISettingsService
    {
        private const int MaxRecentFiles = 10;
        private readonly string _settingsFilePath;
        private SettingsData _data;
        private bool _recentFilesValidated;

        public SettingsService()
        {
            string appDataDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "SmrtPad");
            Directory.CreateDirectory(appDataDir);
            _settingsFilePath = Path.Combine(appDataDir, "settings.json");
            _data = new SettingsData();
            Load();
        }

        public SettingsService(string settingsFilePath)
        {
            _settingsFilePath = settingsFilePath;
            string? dir = Path.GetDirectoryName(settingsFilePath);
            if (dir != null) Directory.CreateDirectory(dir);
            _data = new SettingsData();
            Load();
        }

        public string DefaultFontFamily
        {
            get => _data.DefaultFontFamily;
            set => _data.DefaultFontFamily = value;
        }

        public double DefaultFontSize
        {
            get => _data.DefaultFontSize;
            set => _data.DefaultFontSize = value;
        }

        public bool DefaultWordWrap
        {
            get => _data.DefaultWordWrap;
            set => _data.DefaultWordWrap = value;
        }

        public string DefaultSaveFormat
        {
            get => _data.DefaultSaveFormat;
            set => _data.DefaultSaveFormat = value;
        }

        public string ThemePreference
        {
            get => _data.ThemePreference;
            set => _data.ThemePreference = value;
        }

        public bool AutoSaveEnabled
        {
            get => _data.AutoSaveEnabled;
            set => _data.AutoSaveEnabled = value;
        }

        public int AutoSaveIntervalSeconds
        {
            get => _data.AutoSaveIntervalSeconds;
            set => _data.AutoSaveIntervalSeconds = value;
        }

        public string Language
        {
            get => _data.Language;
            set => _data.Language = value;
        }

        public string RulerUnits
        {
            get => _data.RulerUnits;
            set => _data.RulerUnits = value;
        }

        public bool SpellCheckEnabled
        {
            get => _data.SpellCheckEnabled;
            set => _data.SpellCheckEnabled = value;
        }

        public string PagePaperSize
        {
            get => _data.PagePaperSize;
            set => _data.PagePaperSize = value;
        }

        public string PageOrientation
        {
            get => _data.PageOrientation;
            set => _data.PageOrientation = value;
        }

        public double PageMarginTopInches
        {
            get => _data.PageMarginTopInches;
            set => _data.PageMarginTopInches = value;
        }

        public double PageMarginBottomInches
        {
            get => _data.PageMarginBottomInches;
            set => _data.PageMarginBottomInches = value;
        }

        public double PageMarginLeftInches
        {
            get => _data.PageMarginLeftInches;
            set => _data.PageMarginLeftInches = value;
        }

        public double PageMarginRightInches
        {
            get => _data.PageMarginRightInches;
            set => _data.PageMarginRightInches = value;
        }

        public bool ShowStatusBar
        {
            get => _data.ShowStatusBar;
            set => _data.ShowStatusBar = value;
        }

        public string WordWrapMode
        {
            get => _data.WordWrapMode;
            set => _data.WordWrapMode = value;
        }

        public List<string> RecentFiles
        {
            get
            {
                EnsureRecentFilesValidated();
                return _data.RecentFiles;
            }
        }

        public void AddRecentFile(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return;
            EnsureRecentFilesValidated();
            _data.RecentFiles.Remove(path);
            _data.RecentFiles.Insert(0, path);
            if (_data.RecentFiles.Count > MaxRecentFiles)
                _data.RecentFiles.RemoveRange(MaxRecentFiles, _data.RecentFiles.Count - MaxRecentFiles);
            Save();
        }

        public void ClearRecentFiles()
        {
            _recentFilesValidated = true;
            _data.RecentFiles.Clear();
            Save();
        }

        private static readonly JsonSerializerOptions s_jsonOpts = new() { WriteIndented = true };

        public void Save()
        {
            try
            {
                _data = Normalize(_data);
                var json = JsonSerializer.Serialize(_data, s_jsonOpts);
                File.WriteAllText(_settingsFilePath, json);
            }
            catch (IOException ex)
            {
                Debug.WriteLine($"SettingsService.Save failed: {ex.Message}");
            }
            catch (UnauthorizedAccessException ex)
            {
                Debug.WriteLine($"SettingsService.Save failed: {ex.Message}");
            }
        }

        public void Load()
        {
            try
            {
                if (File.Exists(_settingsFilePath))
                {
                    var json = File.ReadAllText(_settingsFilePath);
                    _data = Normalize(JsonSerializer.Deserialize<SettingsData>(json));
                    _recentFilesValidated = false;
                }
            }
            catch (JsonException ex)
            {
                Debug.WriteLine($"SettingsService.Load failed: {ex.Message}");
                _data = new SettingsData();
                _recentFilesValidated = true;
            }
            catch (IOException ex)
            {
                Debug.WriteLine($"SettingsService.Load failed: {ex.Message}");
                _data = new SettingsData();
                _recentFilesValidated = true;
            }
            catch (UnauthorizedAccessException ex)
            {
                Debug.WriteLine($"SettingsService.Load failed: {ex.Message}");
                _data = new SettingsData();
                _recentFilesValidated = true;
            }
        }

        private void EnsureRecentFilesValidated()
        {
            if (_recentFilesValidated)
                return;

            _recentFilesValidated = true;

            var validPaths = _data.RecentFiles
                .Where(static path => !string.IsNullOrWhiteSpace(path) && File.Exists(path))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(MaxRecentFiles)
                .ToList();

            if (validPaths.Count != _data.RecentFiles.Count ||
                !_data.RecentFiles.SequenceEqual(validPaths, StringComparer.OrdinalIgnoreCase))
            {
                _data.RecentFiles = validPaths;
                Save();
            }
        }

        private static SettingsData Normalize(SettingsData? data)
        {
            var defaults = new SettingsData();
            var normalized = data ?? new SettingsData();

            normalized.DefaultFontFamily = GetValueOrDefault(normalized.DefaultFontFamily, defaults.DefaultFontFamily);
            normalized.DefaultSaveFormat = GetValueOrDefault(normalized.DefaultSaveFormat, defaults.DefaultSaveFormat);
            normalized.ThemePreference = GetValueOrDefault(normalized.ThemePreference, defaults.ThemePreference);
            normalized.Language = GetValueOrDefault(normalized.Language, defaults.Language);
            normalized.RulerUnits = GetValueOrDefault(normalized.RulerUnits, defaults.RulerUnits);
            normalized.PagePaperSize = GetValueOrDefault(normalized.PagePaperSize, defaults.PagePaperSize);
            normalized.PageOrientation = GetValueOrDefault(normalized.PageOrientation, defaults.PageOrientation);
            normalized.WordWrapMode = GetValueOrDefault(normalized.WordWrapMode, defaults.WordWrapMode);
            normalized.RecentFiles ??= [];

            return normalized;
        }

        private static string GetValueOrDefault(string? value, string defaultValue)
        {
            return string.IsNullOrWhiteSpace(value) ? defaultValue : value;
        }

        private class SettingsData
        {
            public string DefaultFontFamily { get; set; } = "Segoe UI";
            public double DefaultFontSize { get; set; } = 11.0;
            public bool DefaultWordWrap { get; set; } = true;
            public string DefaultSaveFormat { get; set; } = ".rtf";
            public string ThemePreference { get; set; } = "System";
            public bool AutoSaveEnabled { get; set; } = false;
            public int AutoSaveIntervalSeconds { get; set; } = 300;
            public string Language { get; set; } = "en-US";
            public string RulerUnits { get; set; } = "in";
            public bool SpellCheckEnabled { get; set; } = true;
            public string PagePaperSize { get; set; } = "Letter";
            public string PageOrientation { get; set; } = "Portrait";
            public double PageMarginTopInches { get; set; } = 1.0;
            public double PageMarginBottomInches { get; set; } = 1.0;
            public double PageMarginLeftInches { get; set; } = 1.0;
            public double PageMarginRightInches { get; set; } = 1.0;
            public bool ShowStatusBar { get; set; } = true;
            public string WordWrapMode { get; set; } = "Wrap";
            public List<string> RecentFiles { get; set; } = [];
        }
    }
}
