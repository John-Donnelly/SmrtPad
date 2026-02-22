using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SmrtPad.Helpers
{
    /// <summary>Identifies the kind of formatting or content operation a macro command represents.</summary>
    public enum MacroCommandType
    {
        Bold,
        Italic,
        Underline,
        Strikethrough,
        Subscript,
        Superscript,
        SetAlignment,
        SetFontFamily,
        SetFontSize,
        SetListType,
        SetLineSpacing,
        InsertText,
        ClearFormatting,
        ZoomIn,
        ZoomOut,
    }

    /// <summary>A single recorded macro instruction with an optional string value.</summary>
    public class MacroCommand
    {
        public MacroCommandType Type { get; set; }
        public string? Value { get; set; }

        [JsonConstructor]
        public MacroCommand() { }

        public MacroCommand(MacroCommandType type, string? value = null)
        {
            Type = type;
            Value = value;
        }

        public override string ToString() =>
            Value is not null ? $"{Type}:{Value}" : Type.ToString();
    }

    /// <summary>
    /// Records, stores, serialises and replays sequences of <see cref="MacroCommand"/> objects.
    /// Thread-safety is not required — all calls happen on the UI thread.
    /// </summary>
    public class MacroHelper
    {
        private readonly List<MacroCommand> _commands = new();
        private bool _isRecording;

        private static readonly JsonSerializerOptions JsonOpts = new()
        {
            WriteIndented = true,
            Converters = { new JsonStringEnumConverter() }
        };

        // ── State ─────────────────────────────────────────────────────────────

        /// <summary>True while a recording session is active.</summary>
        public bool IsRecording => _isRecording;

        /// <summary>Read-only view of recorded commands.</summary>
        public IReadOnlyList<MacroCommand> Commands => _commands;

        /// <summary>Number of commands currently stored.</summary>
        public int Count => _commands.Count;

        // ── Recording ─────────────────────────────────────────────────────────

        /// <summary>Starts a new recording session, clearing any previously recorded commands.</summary>
        public void StartRecording()
        {
            _isRecording = true;
            _commands.Clear();
        }

        /// <summary>Stops the current recording session.</summary>
        public void StopRecording() => _isRecording = false;

        /// <summary>Appends a command when a recording session is active; ignored otherwise.</summary>
        public void Record(MacroCommand command)
        {
            if (_isRecording) _commands.Add(command);
        }

        /// <summary>Convenience overload — constructs and records a command.</summary>
        public void Record(MacroCommandType type, string? value = null)
            => Record(new MacroCommand(type, value));

        /// <summary>Removes all stored commands without affecting the recording state.</summary>
        public void Clear() => _commands.Clear();

        // ── Persistence ───────────────────────────────────────────────────────

        /// <summary>Serialises the command list to a JSON string.</summary>
        public string Serialize()
            => JsonSerializer.Serialize(_commands, JsonOpts);

        /// <summary>Replaces the current command list from a JSON string.</summary>
        public void Deserialize(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) throw new ArgumentException("json is null or empty.", nameof(json));
            var cmds = JsonSerializer.Deserialize<List<MacroCommand>>(json, JsonOpts)
                       ?? throw new InvalidOperationException("Deserialisation returned null.");
            _commands.Clear();
            _commands.AddRange(cmds);
        }

        /// <summary>Saves the serialised macro to a file, overwriting any existing content.</summary>
        public void Save(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("path is null or empty.", nameof(path));
            File.WriteAllText(path, Serialize());
        }

        /// <summary>Loads and deserialises a macro from a file.</summary>
        public void Load(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("path is null or empty.", nameof(path));
            Deserialize(File.ReadAllText(path));
        }
    }
}
