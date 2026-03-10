using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace SmrtPad.Services
{
    /// <summary>
    /// Persists editor session state for crash recovery and restore prompts.
    /// </summary>
    public interface ISessionRestoreService
    {
        /// <summary>
        /// Saves the current session tab states.
        /// </summary>
        Task SaveSessionAsync(IReadOnlyList<SessionTabState> tabs, CancellationToken ct = default);

        /// <summary>
        /// Loads the most recently saved session tab states.
        /// </summary>
        Task<IReadOnlyList<SessionTabState>> LoadSessionAsync(CancellationToken ct = default);

        /// <summary>
        /// Clears any previously saved session state.
        /// </summary>
        Task ClearSessionAsync(CancellationToken ct = default);
    }

    /// <summary>
    /// Represents the persisted state for a single editor tab.
    /// </summary>
    /// <param name="Title">The tab title shown to the user.</param>
    /// <param name="FilePath">The original file path when the tab is backed by a file.</param>
    /// <param name="TempBackupPath">A temporary recovery file path for unsaved content.</param>
    /// <param name="CursorPosition">The caret position to restore.</param>
    public sealed record SessionTabState(string Title, string? FilePath, string? TempBackupPath, int CursorPosition);

    /// <summary>
    /// Stores session state as JSON under the user's local app data folder.
    /// </summary>
    public sealed class SessionRestoreService : ISessionRestoreService
    {
        private static readonly JsonSerializerOptions s_serializerOptions = new()
        {
            WriteIndented = true,
        };

        private readonly string _sessionFilePath;

        /// <summary>
        /// Creates a new session restore service.
        /// </summary>
        /// <param name="sessionFilePath">
        /// Optional override for the session file path. When omitted, the default local app data path is used.
        /// </param>
        public SessionRestoreService(string? sessionFilePath = null)
        {
            _sessionFilePath = string.IsNullOrWhiteSpace(sessionFilePath)
                ? Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "SmrtPad",
                    "session.json")
                : sessionFilePath;
        }

        /// <inheritdoc/>
        public async Task SaveSessionAsync(IReadOnlyList<SessionTabState> tabs, CancellationToken ct = default)
        {
            ArgumentNullException.ThrowIfNull(tabs);
            ct.ThrowIfCancellationRequested();

            var directory = Path.GetDirectoryName(_sessionFilePath);
            if (!string.IsNullOrWhiteSpace(directory))
                Directory.CreateDirectory(directory);

            await using var stream = File.Create(_sessionFilePath);
            await JsonSerializer.SerializeAsync(stream, tabs, s_serializerOptions, ct).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async Task<IReadOnlyList<SessionTabState>> LoadSessionAsync(CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();

            if (!File.Exists(_sessionFilePath))
                return [];

            try
            {
                await using var stream = File.OpenRead(_sessionFilePath);
                var tabs = await JsonSerializer.DeserializeAsync<List<SessionTabState>>(stream, s_serializerOptions, ct)
                    .ConfigureAwait(false);
                return tabs ?? [];
            }
            catch (IOException)
            {
                return [];
            }
            catch (JsonException)
            {
                return [];
            }
        }

        /// <inheritdoc/>
        public Task ClearSessionAsync(CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();

            if (File.Exists(_sessionFilePath))
                File.Delete(_sessionFilePath);

            return Task.CompletedTask;
        }
    }
}
