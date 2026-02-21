using Windows.Storage;

namespace SmrtPad.Helpers
{
    public static class SettingsHelper
    {
        private static ApplicationDataContainer LocalSettings => ApplicationData.Current.LocalSettings;

        public static string DefaultFontFamily
        {
            get => LocalSettings.Values.TryGetValue(nameof(DefaultFontFamily), out var v) ? (string)v : "Segoe UI";
            set => LocalSettings.Values[nameof(DefaultFontFamily)] = value;
        }

        public static double DefaultFontSize
        {
            get => LocalSettings.Values.TryGetValue(nameof(DefaultFontSize), out var v) ? (double)v : 11.0;
            set => LocalSettings.Values[nameof(DefaultFontSize)] = value;
        }

        public static bool WordWrap
        {
            get => LocalSettings.Values.TryGetValue(nameof(WordWrap), out var v) ? (bool)v : true;
            set => LocalSettings.Values[nameof(WordWrap)] = value;
        }
    }
}
