using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace SmrtPad.Controls;

/// <summary>The role of a participant in the sidebar chat.</summary>
public enum SidebarChatRole
{
    User,
    Assistant,
}

/// <summary>
/// Represents a single message bubble in the sidebar chat history.
/// Implements <see cref="INotifyPropertyChanged"/> so individual property updates
/// are reflected in the UI without replacing the entire list item.
/// </summary>
public sealed class SidebarChatEntry : INotifyPropertyChanged
{
    private string _text;
    private bool _isStreaming;
    private string _thinkingText;
    private bool _isThinkingPhase;
    private string _thinkingLabel;

    public SidebarChatEntry(
        SidebarChatRole role,
        string text,
        bool isStreaming = false,
        string? skillKey = null,
        string thinkingText = "",
        bool isThinkingPhase = false,
        string thinkingLabel = "")
    {
        Role = role;
        _text = text;
        _isStreaming = isStreaming;
        SkillKey = skillKey;
        _thinkingText = thinkingText;
        _isThinkingPhase = isThinkingPhase;
        _thinkingLabel = thinkingLabel;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public SidebarChatRole Role { get; }
    public string? SkillKey { get; }

    public string Text
    {
        get => _text;
        set
        {
            if (SetField(ref _text, value))
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(BubbleText)));
        }
    }

    /// <summary>
    /// The text shown in the response bubble. Shows <see cref="Text"/> when non-empty;
    /// falls back to <see cref="InsertText"/> so the user can see what will be inserted
    /// even when the model put all its output inside &lt;insert&gt; tags.
    /// </summary>
    public string BubbleText => string.IsNullOrWhiteSpace(_text)
        ? (_insertText ?? string.Empty)
        : _text;

    public bool IsStreaming
    {
        get => _isStreaming;
        set => SetField(ref _isStreaming, value);
    }

    /// <summary>Thinking/reasoning content emitted between &lt;think&gt;…&lt;/think&gt; tags.</summary>
    public string ThinkingText
    {
        get => _thinkingText;
        set => SetField(ref _thinkingText, value);
    }

    /// <summary>True while the model is still inside the &lt;think&gt; block.</summary>
    public bool IsThinkingPhase
    {
        get => _isThinkingPhase;
        set => SetField(ref _isThinkingPhase, value);
    }

    /// <summary>Header label shown on the thinking expander.</summary>
    public string ThinkingLabel
    {
        get => _thinkingLabel;
        set => SetField(ref _thinkingLabel, value);
    }

    private string? _insertText;

    /// <summary>
    /// The text extracted from the model's &lt;insert&gt;…&lt;/insert&gt; region, if present.
    /// When non-null this is what the Insert button places into the document.
    /// Falls back to <see cref="Text"/> when null.
    /// </summary>
    public string? InsertText
    {
        get => _insertText;
        set
        {
            if (SetField(ref _insertText, value))
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(BubbleText)));
        }
    }

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        return true;
    }
}
