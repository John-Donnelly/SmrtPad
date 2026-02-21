using Microsoft.Windows.ApplicationModel.Resources;

namespace SmrtPad.Helpers
{
    public static class ResourceHelper
    {
        private static readonly ResourceLoader _loader = new();

        /// <summary>
        /// Gets a localized string by resource key.
        /// </summary>
        public static string GetString(string key)
        {
            return _loader.GetString(key);
        }

        /// <summary>
        /// Gets a localized format string and applies the given arguments.
        /// </summary>
        public static string GetFormatted(string key, params object[] args)
        {
            return string.Format(_loader.GetString(key), args);
        }
    }
}
