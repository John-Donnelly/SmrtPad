using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.Json;

namespace SmrtPad.Services
{
    public class SettingsService : ISettingsService
    {
        private const int MaxRecentFiles = 10;
        private readonly string _settingsFilePath;
        private SettingsData _data;

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

        public List<string> RecentFiles => _data.RecentFiles;

        public void AddRecentFile(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return;
            _data.RecentFiles.Remove(path);
            _data.RecentFiles.Insert(0, path);
            if (_data.RecentFiles.Count > MaxRecentFiles)
                _data.RecentFiles.RemoveRange(MaxRecentFiles, _data.RecentFiles.Count - MaxRecentFiles);
            Save();
        }

        public void ClearRecentFiles()
        {
            _data.RecentFiles.Clear();
            Save();
        }

        public void Save()
        {
            try
            {
                var json = JsonSerializer.Serialize(_data, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_settingsFilePath, json);
            }
            catch (Exception ex)
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
                    _data = JsonSerializer.Deserialize<SettingsData>(json) ?? new SettingsData();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"SettingsService.Load failed: {ex.Message}");
                _data = new SettingsData();
            }
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
            public List<string> RecentFiles { get; set; } = new();
        }
    }
}
