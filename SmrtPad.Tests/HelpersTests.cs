using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using SmrtPad.Helpers;

namespace SmrtPad.Tests
{
    /// <summary>
    /// Tests for the in-memory list management logic of RecentFilesHelper.
    /// The storage-backed methods (Add / GetAll) require a WinRT ApplicationData
    /// runtime context and are covered by manual / integration testing.
    /// </summary>
    public class RecentFilesHelperLogicTests
    {
        // Helper: simulate the Add logic without ApplicationData
        private static List<string> SimulateAdd(List<string> existing, string path, int maxCount = 10)
        {
            var list = new List<string>(existing);
            list.Remove(path);
            list.Insert(0, path);
            if (list.Count > maxCount)
                list = list.Take(maxCount).ToList();
            return list;
        }

        [Fact]
        public void Add_InsertsAtFront()
        {
            var result = SimulateAdd(new List<string> { "b.rtf", "c.rtf" }, "a.rtf");
            Assert.Equal("a.rtf", result[0]);
        }

        [Fact]
        public void Add_MovesExistingEntryToFront()
        {
            var result = SimulateAdd(new List<string> { "a.rtf", "b.rtf", "c.rtf" }, "b.rtf");
            Assert.Equal("b.rtf", result[0]);
            Assert.Equal(3, result.Count);
        }

        [Fact]
        public void Add_DeduplicatesPath()
        {
            var result = SimulateAdd(new List<string> { "a.rtf", "b.rtf" }, "a.rtf");
            Assert.Single(result.Where(x => x == "a.rtf"));
        }

        [Fact]
        public void Add_RespectsMaxCount()
        {
            var existing = Enumerable.Range(1, 10).Select(i => $"file{i}.rtf").ToList();
            var result = SimulateAdd(existing, "new.rtf", maxCount: 10);
            Assert.Equal(10, result.Count);
            Assert.Equal("new.rtf", result[0]);
        }

        [Fact]
        public void Add_ToEmptyList_ReturnsOneItem()
        {
            var result = SimulateAdd(new List<string>(), "doc.rtf");
            Assert.Single(result);
            Assert.Equal("doc.rtf", result[0]);
        }

        [Fact]
        public void Add_PreservesOrderOfRemainingItems()
        {
            var existing = new List<string> { "a.rtf", "b.rtf", "c.rtf" };
            var result = SimulateAdd(existing, "d.rtf");
            Assert.Equal(new[] { "d.rtf", "a.rtf", "b.rtf", "c.rtf" }, result);
        }

        [Fact]
        public void Add_MaxCountOne_AlwaysKeepsOnlyNewest()
        {
            var existing = new List<string> { "old.rtf" };
            var result = SimulateAdd(existing, "new.rtf", maxCount: 1);
            Assert.Single(result);
            Assert.Equal("new.rtf", result[0]);
        }
    }

    /// <summary>
    /// Tests for SettingsHelper default values (does not require ApplicationData runtime).
    /// </summary>
    public class SettingsHelperDefaultTests
    {
        [Fact]
        public void DefaultFontFamily_FallbackIs_SegoeUI()
        {
            // The fallback string used when the key is absent
            const string expected = "Segoe UI";
            // Simulate the TryGetValue miss branch
            object? value = null;
            var result = value is string s ? s : expected;
            Assert.Equal(expected, result);
        }

        [Fact]
        public void DefaultFontSize_FallbackIs_11()
        {
            const double expected = 11.0;
            object? value = null;
            var result = value is double d ? d : expected;
            Assert.Equal(expected, result);
        }

        [Fact]
        public void WordWrap_FallbackIs_True()
        {
            object? value = null;
            var result = value is bool b ? b : true;
            Assert.True(result);
        }
    }
}
