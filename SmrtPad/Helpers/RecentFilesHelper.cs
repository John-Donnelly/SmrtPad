using System;
using System.Collections.Generic;
using System.Linq;
using Windows.Storage;

namespace SmrtPad.Helpers
{
    public static class RecentFilesHelper
    {
        private const string Key = "RecentFiles";
        private const int MaxCount = 10;
        private const char Separator = '\n';

        public static IReadOnlyList<string> GetAll()
        {
            if (ApplicationData.Current.LocalSettings.Values.TryGetValue(Key, out var v))
                return ((string)v).Split(Separator, StringSplitOptions.RemoveEmptyEntries).ToList();
            return Array.Empty<string>();
        }

        public static void Add(string path)
        {
            var list = GetAll().ToList();
            list.Remove(path);
            list.Insert(0, path);
            if (list.Count > MaxCount)
                list = list.Take(MaxCount).ToList();
            ApplicationData.Current.LocalSettings.Values[Key] = string.Join(Separator, list);
        }
    }
}
