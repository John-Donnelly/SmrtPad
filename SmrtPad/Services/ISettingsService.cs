using System.Collections.Generic;

namespace SmrtPad.Services
{
    public interface ISettingsService
    {
        string DefaultFontFamily { get; set; }
        double DefaultFontSize { get; set; }
        bool DefaultWordWrap { get; set; }
        string DefaultSaveFormat { get; set; }
        string ThemePreference { get; set; }
        bool AutoSaveEnabled { get; set; }
        int AutoSaveIntervalSeconds { get; set; }
        string Language { get; set; }
        string RulerUnits { get; set; }
        bool SpellCheckEnabled { get; set; }
        List<string> RecentFiles { get; }
        void AddRecentFile(string path);
        void ClearRecentFiles();
        void Save();
        void Load();
    }
}
