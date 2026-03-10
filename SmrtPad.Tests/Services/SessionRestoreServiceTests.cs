using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SmrtPad.Services;
using Xunit;

namespace SmrtPad.Tests.Services
{
    public sealed class SessionRestoreServiceTests
    {
        [Fact]
        public async Task SaveSession_ThenLoadSession_ReturnsEquivalentTabs()
        {
            var service = CreateService();
            var expected = CreateTabs();

            await service.SaveSessionAsync(expected);
            var actual = await service.LoadSessionAsync();

            Assert.Equal(expected, actual);
        }

        [Fact]
        public async Task SaveSession_EmptyList_SavesValidJson()
        {
            var (service, path) = CreateServiceWithPath();

            await service.SaveSessionAsync([]);

            var json = await File.ReadAllTextAsync(path);
            Assert.Equal("[]", json.Trim());
        }

        [Fact]
        public async Task LoadSession_NoSavedFile_ReturnsEmptyList()
        {
            var service = CreateService();

            var result = await service.LoadSessionAsync();

            Assert.Empty(result);
        }

        [Fact]
        public async Task LoadSession_CorruptedJson_ReturnsEmptyList()
        {
            var (service, path) = CreateServiceWithPath();
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            await File.WriteAllTextAsync(path, "{not valid json}");

            var result = await service.LoadSessionAsync();

            Assert.Empty(result);
        }

        [Fact]
        public async Task ClearSession_AfterSave_LoadReturnsEmptyList()
        {
            var service = CreateService();

            await service.SaveSessionAsync(CreateTabs());
            await service.ClearSessionAsync();
            var result = await service.LoadSessionAsync();

            Assert.Empty(result);
        }

        [Fact]
        public async Task SaveSession_NullList_ThrowsArgumentNullException()
        {
            var service = CreateService();

            await Assert.ThrowsAsync<ArgumentNullException>(() => service.SaveSessionAsync(null!));
        }

        [Fact]
        public async Task SaveSession_CancellationRequested_ThrowsOperationCanceledException()
        {
            var service = CreateService();
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            await Assert.ThrowsAsync<OperationCanceledException>(() => service.SaveSessionAsync(CreateTabs(), cts.Token));
        }

        [Fact]
        public async Task LoadSession_CancellationRequested_ThrowsOperationCanceledException()
        {
            var service = CreateService();
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            await Assert.ThrowsAsync<OperationCanceledException>(() => service.LoadSessionAsync(cts.Token));
        }

        [Fact]
        public async Task ClearSession_WhenNoFileExists_DoesNotThrow()
        {
            var (service, path) = CreateServiceWithPath();

            await service.ClearSessionAsync();

            Assert.False(File.Exists(path));
        }

        [Fact]
        public async Task SaveSession_MultipleTabs_AllTabsPreserved()
        {
            var service = CreateService();

            await service.SaveSessionAsync(CreateTabs());
            var result = await service.LoadSessionAsync();

            Assert.Equal(3, result.Count);
        }

        [Fact]
        public async Task SaveSession_TabWithNullFilePath_Preserved()
        {
            var service = CreateService();
            var tabs = new[] { new SessionTabState("Draft", null, "backup.rtf", 12) };

            await service.SaveSessionAsync(tabs);
            var result = await service.LoadSessionAsync();

            Assert.Null(result.Single().FilePath);
        }

        [Fact]
        public async Task SaveSession_TabWithNullTempBackupPath_Preserved()
        {
            var service = CreateService();
            var tabs = new[] { new SessionTabState("Saved", "file.rtf", null, 34) };

            await service.SaveSessionAsync(tabs);
            var result = await service.LoadSessionAsync();

            Assert.Null(result.Single().TempBackupPath);
        }

        [Fact]
        public async Task SaveSession_OverwritesPreviousSave()
        {
            var service = CreateService();
            var first = new[] { new SessionTabState("First", "first.rtf", null, 1) };
            var second = new[] { new SessionTabState("Second", "second.rtf", "backup.rtf", 2) };

            await service.SaveSessionAsync(first);
            await service.SaveSessionAsync(second);
            var result = await service.LoadSessionAsync();

            Assert.Equal(second, result);
        }

        [Fact]
        public async Task LoadSession_ValidFile_PreservesTabOrder()
        {
            var service = CreateService();
            var expected = CreateTabs();

            await service.SaveSessionAsync(expected);
            var result = await service.LoadSessionAsync();

            Assert.Equal(expected.Select(static tab => tab.Title), result.Select(static tab => tab.Title));
        }

        private static SessionRestoreService CreateService()
        {
            var (service, _) = CreateServiceWithPath();
            return service;
        }

        private static (SessionRestoreService Service, string Path) CreateServiceWithPath()
        {
            var root = Path.Combine(Path.GetTempPath(), "SmrtPad.Tests", Guid.NewGuid().ToString("N"));
            var path = Path.Combine(root, "session.json");
            return (new SessionRestoreService(path), path);
        }

        private static IReadOnlyList<SessionTabState> CreateTabs() =>
        [
            new SessionTabState("Draft", null, @"C:\backups\tab_1.rtf", 12),
            new SessionTabState("Saved", @"C:\docs\saved.rtf", null, 34),
            new SessionTabState("Recovered", null, @"C:\backups\tab_3.rtf", 56),
        ];
    }
}
