using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Xml.Linq;
using Microsoft.Windows.ApplicationModel.Resources;

namespace SmrtPad.Helpers
{
    public static class ResourceHelper
    {
        private const int NamedResourceNotFoundHResult = unchecked((int)0x80073B17);

        private static readonly ResourceLoader? _loader;
        private static Dictionary<string, string>? _fallback;

        static ResourceHelper()
        {
            try
            {
                _loader = new ResourceLoader();
            }
            catch
            {
                // ResourceLoader unavailable (e.g. unit tests).
                // Fall back to parsing the .resw file directly.
                _fallback = LoadFallbackStrings();
            }
        }

        /// <summary>
        /// Gets a localized string by resource key.
        /// </summary>
        public static string GetString(string key)
        {
            if (_loader is not null)
            {
                try
                {
                    var resourceValue = _loader.GetString(key);
                    if (!string.IsNullOrEmpty(resourceValue))
                        return resourceValue;
                }
                catch (COMException ex) when (ex.HResult == NamedResourceNotFoundHResult)
                {
                }
            }

            var fallback = _fallback ??= LoadFallbackStrings();
            if (fallback.TryGetValue(key, out var value))
                return value;

            return key;
        }

        /// <summary>
        /// Gets a localized format string and applies the given arguments.
        /// </summary>
        public static string GetFormatted(string key, params object[] args)
        {
            return string.Format(GetString(key), args);
        }

        private static Dictionary<string, string> LoadFallbackStrings()
        {
            var dict = new Dictionary<string, string>(StringComparer.Ordinal);
            try
            {
                // Walk up from the executing assembly to find the resw file
                string? dir = AppContext.BaseDirectory;
                string? reswPath = null;
                for (int i = 0; i < 8 && dir is not null; i++)
                {
                    string candidate = Path.Combine(dir, "Strings", "en-US", "Resources.resw");
                    if (!File.Exists(candidate))
                        candidate = Path.Combine(dir, "SmrtPad", "Strings", "en-US", "Resources.resw");
                    if (File.Exists(candidate))
                    {
                        reswPath = candidate;
                        break;
                    }
                    dir = Directory.GetParent(dir)?.FullName;
                }

                if (reswPath is null) return dict;

                var doc = XDocument.Load(reswPath);
                foreach (var data in doc.Descendants("data"))
                {
                    string? name = data.Attribute("name")?.Value;
                    string? val = data.Element("value")?.Value;
                    if (name is not null && val is not null)
                    {
                        // Strip the property suffix (e.g. "CutMenuItem.Text" → key "CutMenuItem.Text")
                        // For code-behind keys (no dot), store as-is
                        dict[name] = val;

                        // Also store without the property suffix for convenience
                        int dot = name.IndexOf('.');
                        if (dot > 0)
                        {
                            string prefix = name[..dot];
                            if (!dict.ContainsKey(prefix))
                                dict[prefix] = val;
                        }
                    }
                }
            }
            catch
            {
                // If parsing fails, return empty dict — GetString returns key name
            }
            return dict;
        }
    }
}
