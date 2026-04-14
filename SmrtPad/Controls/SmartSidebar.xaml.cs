using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using SmrtPad.Helpers;
using SmrtPad.Services;
using SmrtPad.Services.Licensing;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Windows.ApplicationModel.DataTransfer;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using Windows.Storage;

namespace SmrtPad.Controls;

/// <summary>
/// Smart Sidebar for Pro-tier AI features: summarize, tone shift, semantic search.
/// Constructed only when <see cref="Services.Licensing.FeatureFlags.IsEnabled"/> returns <c>true</c>
/// for <see cref="Services.Licensing.SmrtPadFeature.SmartSidebar"/>.
/// </summary>
public sealed partial class SmartSidebar : UserControl
{
    private const string StatusCodePending = "PENDING";
    private const string StatusCodeReady = "READY";
    private const string StatusCodeUnavailable = "UNAVAILABLE";
    private const string StatusCodeInitFailed = "PREREQ_DISPATCHER_INIT_FAILED";

    private readonly IAIDispatcher _dispatcher;
    private readonly ObservableCollection<SidebarChatEntry> _chatEntries = [];
    private CancellationTokenSource? _activeCts;
    private CancellationTokenSource? _initializationCts;
    private Task? _initializationTask;
    private Task? _activeStreamTask;
    private string _lastOcrText = string.Empty;
    private string _lastTokensPerSecond = string.Empty;
    private readonly HashSet<int> _indexedSemanticTabs = [];
    // Scroll throttle: set true when a scroll is desired; a DispatcherTimer fires it at ~30 fps
    private bool _pendingScroll;
    private readonly Microsoft.UI.Dispatching.DispatcherQueueTimer _scrollTimer;
    private readonly TextBlock _ocrResultTitleText = new() { FontWeight = FontWeights.SemiBold };
    private readonly TextBox _ocrResultTextBox = new()
    {
        AcceptsReturn = true,
        IsReadOnly = true,
        MaxHeight = 220,
        MinHeight = 120,
        TextWrapping = TextWrapping.Wrap,
    };
    private readonly Button _insertOcrButton = new() { HorizontalAlignment = HorizontalAlignment.Stretch };
    private readonly TextBlock _hardwareDetailsTitleText = new() { FontWeight = FontWeights.SemiBold };
    private readonly TextBlock _hardwareModelLabelText = new();
    private readonly TextBlock _hardwareModelValueText = new() { TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock _hardwareTokensLabelText = new();
    private readonly TextBlock _hardwareTokensValueText = new() { TextWrapping = TextWrapping.Wrap };

    private TextBlock InitializationStatusTextControl => (TextBlock)FindName("InitializationStatusText")!;

    /// <summary>Raised when the user clicks the close button.</summary>
    public event EventHandler? CloseRequested;

    /// <summary>Delegate the sidebar calls to get the currently selected text from the active editor.</summary>
    public Func<string>? GetSelectedText { get; set; }

    /// <summary>Delegate the sidebar calls to get the current rewrite source from the active editor.</summary>
    public Func<string>? GetRewriteSourceText { get; set; }

    /// <summary>Delegate the sidebar calls to get the text before the caret for inline completion.</summary>
    public Func<string>? GetTextBeforeCaret { get; set; }

    /// <summary>Delegate invoked when a tone rewrite should replace text in the active editor.</summary>
    public Action<string>? ApplyToneRewrite { get; set; }

    /// <summary>Delegate invoked when a clarity rewrite should replace text in the active editor.</summary>
    public Action<string>? ApplyClarityRewrite { get; set; }

    /// <summary>Delegate invoked when a grammar-fix rewrite should replace text in the active editor.</summary>
    public Action<string>? ApplyGrammarFix { get; set; }

    /// <summary>Delegate invoked when a shortened rewrite should replace text in the active editor.</summary>
    public Action<string>? ApplyShortenRewrite { get; set; }

    /// <summary>Delegate invoked when generated text should be inserted into the active editor.</summary>
    public Action<string>? InsertGeneratedText { get; set; }

    /// <summary>Delegate the sidebar calls to get semantic-search document snapshots from the main app.</summary>
    public Func<IReadOnlyList<SemanticSearchDocument>>? GetSemanticDocuments { get; set; }

    /// <summary>Delegate invoked when a semantic-search result should navigate to a tab and chunk.</summary>
    public Action<int, string>? NavigateToSemanticResult { get; set; }

    /// <summary>Delegate invoked with a human-readable status message each time the AI initialization stage changes.</summary>
    public Action<string>? ReportStatus { get; set; }

    public SmartSidebar(IAIDispatcher dispatcher)
    {
        ArgumentNullException.ThrowIfNull(dispatcher);
        _dispatcher = dispatcher;
        InitializeComponent();
        Loaded += SmartSidebar_Loaded;
        Unloaded += SmartSidebar_Unloaded;
        InitializeFlyouts();
        InitializeSkillButtons();
        ApplyLocalizedStrings();
        ChatHistoryList.ItemsSource = _chatEntries;
        SemanticSection.Visibility = FeatureFlags.IsEnabled(SmrtPadFeature.SemanticSearch)
            ? Visibility.Visible
            : Visibility.Collapsed;
        ApplyDispatcherPendingState();

        // Fire scroll at ~30 fps to avoid walking the visual tree on every token
        _scrollTimer = DispatcherQueue.CreateTimer();
        _scrollTimer.Interval = TimeSpan.FromMilliseconds(33);
        _scrollTimer.Tick += (_, _) =>
        {
            if (!_pendingScroll) return;
            _pendingScroll = false;
            ScrollChatToBottom();
        };
        _scrollTimer.Start();
    }

    private async void SmartSidebar_Loaded(object sender, RoutedEventArgs e)
    {
        if (_dispatcher.IsInitialized)
        {
            ApplyDispatcherReadyState();
            return;
        }

        if (_initializationTask is not null && !_initializationTask.IsCompleted)
            return;

        _initializationCts?.Dispose();
        _initializationCts = new CancellationTokenSource();
        _initializationTask = InitializeDispatcherAsync(_initializationCts.Token);

        try
        {
            await _initializationTask;
        }
        catch (OperationCanceledException) when (_initializationCts?.IsCancellationRequested == true)
        {
            // Sidebar was closed before init finished — no UI update needed.
        }
        catch (OperationCanceledException)
        {
            // Inner timeout fired while the sidebar is still open.
            ApplyDispatcherUnavailableState(ResourceHelper.GetString("SmartSidebarExecutionTimedOut"));
        }
        catch (InvalidOperationException ex)
        {
            ApplyDispatcherUnavailableState(ex.Message);
            Debug.WriteLine($"AI init failed: {ex.Message}");
        }
        catch (COMException ex)
        {
            ApplyDispatcherUnavailableState(ex.Message);
            Debug.WriteLine($"AI init failed: {ex.Message}");
        }
    }

    private void SmartSidebar_Unloaded(object sender, RoutedEventArgs e)
    {
        _scrollTimer.Stop();
        CancelActive();

        if (_initializationCts is not null)
        {
            _initializationCts.Cancel();
            _initializationCts.Dispose();
            _initializationCts = null;
        }

        _initializationTask = null;
    }

    private async Task InitializeDispatcherAsync(CancellationToken ct)
    {
        ApplyDispatcherPendingState();

        if (!_dispatcher.IsInitialized)
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(120));

            await _dispatcher.InitializeAsync(
                onProgress: token =>
                    DispatcherQueue.TryEnqueue(() =>
                    {
                        var msg = ParseProgressToken(token);
                        SetInitializationStatus(msg, isVisible: true);
                        ReportStatus?.Invoke(msg);
                    }),
                ct: timeoutCts.Token);
        }

        ct.ThrowIfCancellationRequested();
        ApplyDispatcherReadyState();
    }

    /// <summary>Converts an internal stage token into a localized, user-facing progress string.</summary>
    private string ParseProgressToken(string token)
    {
        if (token.StartsWith("AI_STAGE_DOWNLOADING\t", StringComparison.Ordinal))
        {
            var parts = token.Split('\t');
            string alias = parts.Length > 1 ? parts[1] : string.Empty;
            string mb = parts.Length > 2 ? parts[2] : "0";
            if (parts.Length > 3 && int.TryParse(parts[3], out int pct))
                return ResourceHelper.GetFormatted("SmartSidebarStageDownloadingPct", alias, mb, pct);
            return ResourceHelper.GetFormatted("SmartSidebarStageDownloading", alias, mb);
        }

        return token switch
        {
            "AI_STAGE_PROBING"   => ResourceHelper.GetString("SmartSidebarStageProbing"),
            "AI_STAGE_SELECTING" => ResourceHelper.GetString("SmartSidebarStageSelecting"),
            "AI_STAGE_SERVICE"   => ResourceHelper.GetString("SmartSidebarStageService"),
            "AI_STAGE_CACHED"    => ResourceHelper.GetString("SmartSidebarStageCached"),
            "AI_STAGE_LOADING"   => ResourceHelper.GetString("SmartSidebarStageLoading"),
            _ => token,
        };
    }

    // ── Header ──

    private void CloseButton_Click(object sender, RoutedEventArgs e) =>
        CloseRequested?.Invoke(this, EventArgs.Empty);

    private void NewSessionMenuItem_Click(object sender, RoutedEventArgs e)
    {
        CancelActive();
        _chatEntries.Clear();
    }

    private async void ModelMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not RadioMenuFlyoutItem { Tag: string alias })
            return;

        _dispatcher.SetPreferredModelAlias(alias);
        CancelActive();

        // Drain any in-flight stream before disposing the model to prevent crashes
        if (_activeStreamTask is not null)
        {
            try { await _activeStreamTask; } catch { }
            _activeStreamTask = null;
        }

        _chatEntries.Clear();

        // Cancel any in-flight initialization and drain it (stay on UI thread so continuations are safe)
        if (_initializationCts is not null)
        {
            _initializationCts.Cancel();
            _initializationCts.Dispose();
            _initializationCts = null;
        }
        if (_initializationTask is not null)
        {
            try { await _initializationTask; } catch { }
            _initializationTask = null;
        }

        // Reset dispatcher state on a background thread, then resume on the UI thread
        await Task.Run(() => _dispatcher.ResetAsync());

        _initializationCts = new CancellationTokenSource();
        _initializationTask = InitializeDispatcherAsync(_initializationCts.Token);
        try
        {
            await _initializationTask;
        }
        catch (OperationCanceledException) { }
        catch (InvalidOperationException ex)
        {
            ApplyDispatcherUnavailableState(ex.Message);
        }
        catch (COMException ex)
        {
            ApplyDispatcherUnavailableState(ex.Message);
        }
    }

    private void PopulateModelMenu()
    {
        // Choose the eligible model list based on the current execution target:
        //   CPU target → RAM-eligible models (CPU footprints)
        //   NPU target → phi-silica is auto-selected by the runtime; no user-picker needed
        //   GPU / auto → VRAM-eligible models (GPU footprints, default)
        var target = _dispatcher.PreferredExecutionTarget
            ?? _dispatcher.Availability.SelectedTarget;

        IReadOnlyList<string> aliases;
        if (string.Equals(target, "OnnxRuntimeCpu", StringComparison.Ordinal))
        {
            aliases = _dispatcher.GetEligibleCpuModelAliases();
        }
        else if (string.Equals(target, "PhiSilicaNpu", StringComparison.Ordinal))
        {
            // NPU uses Phi Silica which is auto-selected; hide the model sub-menu entirely.
            var npuSubMenu = (MenuFlyoutSubItem)OptionsFlyout.Items
                .OfType<MenuFlyoutSubItem>().First();
            npuSubMenu.Items.Clear();
            npuSubMenu.Visibility = Visibility.Collapsed;
            OptionsFlyout.Items.OfType<MenuFlyoutSeparator>().First().Visibility = Visibility.Collapsed;
            return;
        }
        else
        {
            aliases = _dispatcher.GetEligibleModelAliases();
        }

        if (aliases.Count == 0)
            return;

        var modelSubMenu = (MenuFlyoutSubItem)OptionsFlyout.Items
            .OfType<MenuFlyoutSubItem>().First();
        var separator = OptionsFlyout.Items
            .OfType<MenuFlyoutSeparator>().First();

        modelSubMenu.Items.Clear();

        var currentAlias = _dispatcher.PreferredModelAlias;

        foreach (var alias in aliases)
        {
            var item = new RadioMenuFlyoutItem
            {
                Text = alias,
                Tag = alias,
                GroupName = "ModelSelection",
                IsChecked = string.Equals(alias, currentAlias, StringComparison.OrdinalIgnoreCase),
            };
            item.Click += ModelMenuItem_Click;
            modelSubMenu.Items.Add(item);
        }

        separator.Visibility = Visibility.Visible;
        modelSubMenu.Visibility = Visibility.Visible;
    }

    private void PopulateExecutionTargetMenu()
    {
        var availability = _dispatcher.Availability;

        var targetSubMenu = OptionsFlyout.Items
            .OfType<MenuFlyoutSubItem>()
            .Skip(1)
            .FirstOrDefault();
        var targetSeparator = OptionsFlyout.Items
            .OfType<MenuFlyoutSeparator>()
            .Skip(1)
            .FirstOrDefault();

        if (targetSubMenu is null || targetSeparator is null)
            return;

        targetSubMenu.Items.Clear();

        var currentTarget = _dispatcher.PreferredExecutionTarget
            ?? _dispatcher.Availability.SelectedTarget;

        (string Key, string Label, bool IsEnabled)[] targets =
        [
            ("PhiSilicaNpu",    ResourceHelper.GetString("SmartSidebarNpu"), availability.PhiSilica.IsUsable),
            ("OnnxRuntimeGpu", ResourceHelper.GetString("SmartSidebarGpu"), availability.Gpu.IsUsable),
            ("OnnxRuntimeCpu", ResourceHelper.GetString("SmartSidebarCpu"), true),
        ];

        foreach (var (key, label, isEnabled) in targets)
        {
            var item = new RadioMenuFlyoutItem
            {
                Text = label,
                Tag = key,
                GroupName = "ExecutionTarget",
                IsEnabled = isEnabled,
                IsChecked = string.Equals(key, currentTarget, StringComparison.Ordinal),
            };
            item.Click += ExecutionTargetMenuItem_Click;
            targetSubMenu.Items.Add(item);
        }

        targetSeparator.Visibility = Visibility.Visible;
        targetSubMenu.Visibility = Visibility.Visible;
    }

    private async void ExecutionTargetMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not RadioMenuFlyoutItem { Tag: string targetKey })
            return;

        _dispatcher.SetPreferredExecutionTarget(targetKey);
        _dispatcher.SetPreferredModelAlias(null); // reset model so target is auto-selected
        CancelActive();

        // Drain any in-flight stream before disposing the model to prevent crashes
        if (_activeStreamTask is not null)
        {
            try { await _activeStreamTask; } catch { }
            _activeStreamTask = null;
        }

        _chatEntries.Clear();

        if (_initializationCts is not null)
        {
            _initializationCts.Cancel();
            _initializationCts.Dispose();
            _initializationCts = null;
        }
        if (_initializationTask is not null)
        {
            try { await _initializationTask; } catch { }
            _initializationTask = null;
        }

        await Task.Run(() => _dispatcher.ResetAsync());

        _initializationCts = new CancellationTokenSource();
        _initializationTask = InitializeDispatcherAsync(_initializationCts.Token);
        try
        {
            await _initializationTask;
        }
        catch (OperationCanceledException) { }
        catch (InvalidOperationException ex)
        {
            ApplyDispatcherUnavailableState(ex.Message);
        }
        catch (COMException ex)
        {
            ApplyDispatcherUnavailableState(ex.Message);
        }
    }

    // ── Skill dropdown ──

    private void InitializeSkillButtons()
    {
        var items = new List<SkillButtonViewModel>
        {
            new(ResourceHelper.GetString("SmartSidebarSummarize"),         "summarize"),
            new(ResourceHelper.GetString("SmartSidebarToneRewrite"),        "tone-professional"),
            new(ResourceHelper.GetString("SmartSidebarRewriteForClarity"),  "rewrite"),
            new(ResourceHelper.GetString("SmartSidebarGrammarFix"),         "grammar"),
            new(ResourceHelper.GetString("SmartSidebarShorten"),            "shorten"),
            new(ResourceHelper.GetString("SmartSidebarAutoComplete"),       "autocomplete"),
        };
        SkillDropdown.PlaceholderText = ResourceHelper.GetString("SmartSidebarSkillPlaceholder");
        SkillDropdown.ItemsSource = items;
        SkillDropdown.DisplayMemberPath = "Label";
    }

    private void SkillDropdown_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var selected = SkillDropdown.SelectedItem as SkillButtonViewModel;
        ApplySkillButton.IsEnabled = selected is not null;
        // Show tone toggle only for the tone skill
        ToneToggle.Visibility = selected?.SkillKey == "tone-professional"
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private async void ApplySkillButton_Click(object sender, RoutedEventArgs e)
    {
        if (SkillDropdown.SelectedItem is not SkillButtonViewModel skill)
            return;

        var skillKey = skill.SkillKey;

        string text = skillKey switch
        {
            "autocomplete" => GetTextBeforeCaret?.Invoke() ?? string.Empty,
            "rewrite" or "tone-professional" =>
                GetRewriteSourceText?.Invoke() ?? string.Empty,
            _ => GetSelectedText?.Invoke() ?? string.Empty,
        };

        // Resolve tone direction from toggle
        if (skillKey == "tone-professional")
            skillKey = ToneToggle.IsOn ? "tone-professional" : "tone-casual";

        if (string.IsNullOrWhiteSpace(text))
        {
            AppendChatEntry(new SidebarChatEntry(SidebarChatRole.Assistant,
                ResourceHelper.GetString("SmartSidebarSelectionRequired")));
            return;
        }

        await RunChatStreamAsync(skillKey, text);
    }

    // ── Chat input ──

    private async void SendChatButton_Click(object sender, RoutedEventArgs e) =>
        await SendChatInputAsync();

    private async void ChatInputBox_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Enter)
        {
            e.Handled = true;
            await SendChatInputAsync();
        }
    }

    private async Task SendChatInputAsync()
    {
        var input = ChatInputBox.Text?.Trim();
        if (string.IsNullOrWhiteSpace(input))
            return;

        ChatInputBox.Text = string.Empty;
        await RunChatStreamAsync("freeform", input);
    }

    private void StopChatButton_Click(object sender, RoutedEventArgs e) =>
        CancelActive();

    // ── Per-bubble Insert ──

    private void InsertBubbleButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.DataContext is SidebarChatEntry entry)
        {
            var text = entry.InsertText ?? entry.Text;
            if (!string.IsNullOrWhiteSpace(text))
                InsertGeneratedText?.Invoke(text);
        }
    }

    // ── Semantic Search ──

    private async void SemanticSearchBox_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
    {
        if (SemanticSection.Visibility != Visibility.Visible)
            return;

        var queryText = args.QueryText?.Trim();
        if (string.IsNullOrWhiteSpace(queryText))
            return;

        var documents = GetSemanticDocuments?.Invoke() ?? [];

        SearchProgress.IsActive = true;
        SearchProgress.Visibility = Visibility.Visible;
        SearchResultsList.ItemsSource = null;

        try
        {
            await RefreshSemanticIndexAsync(documents);
            var results = await _dispatcher.QuerySemanticAsync(queryText, 5);
            var tabNames = documents.ToDictionary(static document => document.TabId, static document => document.TabName);
            SearchResultsList.ItemsSource = results
                .Select(result => new SearchResultItem(
                    result.TabId,
                    tabNames.TryGetValue(result.TabId, out var tabName) ? tabName : string.Empty,
                    result.ChunkText,
                    TruncateChunk(result.ChunkText)))
                .ToArray();
        }
        finally
        {
            SearchProgress.IsActive = false;
            SearchProgress.Visibility = Visibility.Collapsed;
        }
    }

    private void SearchResultsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (SearchResultsList.SelectedItem is not SearchResultItem result)
            return;

        NavigateToSemanticResult?.Invoke(result.TabId, result.SearchText);
        SearchResultsList.SelectedItem = null;
    }

    // ── OCR ──

    private void OcrDropZone_DragOver(object sender, DragEventArgs e)
    {
        e.AcceptedOperation = DataPackageOperation.Copy;
    }

    private async void OcrDropZone_Drop(object sender, DragEventArgs e)
    {
        if (!e.DataView.Contains(StandardDataFormats.StorageItems))
            return;

        try
        {
            OcrProgress.IsActive = true;
            OcrProgress.Visibility = Visibility.Visible;

            var items = await e.DataView.GetStorageItemsAsync();
            var file = items.OfType<StorageFile>().FirstOrDefault(IsSupportedImageFile);
            if (file is null)
            {
                ShowOcrResult(ResourceHelper.GetString("SmartSidebarOcrUnavailable"));
                return;
            }

            var extractedText = await ExtractOcrTextAsync(file);
            ShowOcrResult(string.IsNullOrEmpty(extractedText)
                ? ResourceHelper.GetString("SmartSidebarOcrNoText")
                : extractedText);
        }
        catch (ArgumentException ex)
        {
            ShowOcrResult(string.Format(ResourceHelper.GetString("SmartSidebarErrorFormat"), ex.Message));
        }
        catch (InvalidOperationException ex)
        {
            ShowOcrResult(string.Format(ResourceHelper.GetString("SmartSidebarErrorFormat"), ex.Message));
        }
        catch (COMException ex)
        {
            ShowOcrResult(string.Format(ResourceHelper.GetString("SmartSidebarErrorFormat"), ex.Message));
        }
        finally
        {
            OcrProgress.IsActive = false;
            OcrProgress.Visibility = Visibility.Collapsed;
        }
    }

    private void InsertOcrButton_Click(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(_lastOcrText))
            InsertGeneratedText?.Invoke(_lastOcrText);
    }

    // ── Badge ──

    private void HardwareBadge_Tapped(object sender, Microsoft.UI.Xaml.Input.TappedRoutedEventArgs e)
    {
        FlyoutBase.ShowAttachedFlyout(HardwareBadge);
    }

    // ── Chat streaming core ──

    private async Task RunChatStreamAsync(string skillKey, string prompt)
    {
        ArgumentNullException.ThrowIfNull(skillKey);
        ArgumentNullException.ThrowIfNull(prompt);

        // Show user bubble
        AppendChatEntry(new SidebarChatEntry(SidebarChatRole.User, prompt, skillKey: skillKey));

        // Placeholder streaming assistant bubble
        var streamingEntry = new SidebarChatEntry(
            SidebarChatRole.Assistant, string.Empty,
            isStreaming: true,
            thinkingLabel: ResourceHelper.GetString("SmartSidebarThinkingLabel"));
        AppendChatEntry(streamingEntry);
        int streamingIndex = _chatEntries.Count - 1;

        CancelActive();
        _activeCts = new CancellationTokenSource();
        var ct = _activeCts.Token;

        // Separate buffers for <think>, <insert>, and actual answer content
        var thinkBuilder = new StringBuilder();
        var answerBuilder = new StringBuilder();
        var insertBuilder = new StringBuilder();
        var rawBuffer = new StringBuilder();
        bool inThinkBlock = false;
        bool inInsertBlock = false;
        var tokenCount = 0;
        var stopwatch = Stopwatch.StartNew();

        SendChatButton.Visibility = Visibility.Collapsed;
        StopChatButton.Visibility = Visibility.Visible;

        _activeStreamTask = _dispatcher.StreamResponseAsync(
            skillKey,
            prompt,
            onToken: token =>
            {
                rawBuffer.Append(token);
                tokenCount += EstimateTokenCount(token);

                // Parse <think>…</think> and <insert>…</insert> tags out of the raw stream
                ParseThinkingToken(rawBuffer, thinkBuilder, answerBuilder, insertBuilder, ref inThinkBlock, ref inInsertBlock);

                var answerSnap = answerBuilder.ToString();
                var thinkSnap = thinkBuilder.ToString();
                var insertSnap = insertBuilder.Length > 0 ? insertBuilder.ToString() : null;
                var thinkPhaseSnap = inThinkBlock;
                // When the model puts all its content inside <insert> tags (e.g. summarize, tone),
                // answerSnap is empty/whitespace. Promote the insert content to the visible bubble
                // text so the response streams in full. InsertText is kept separately for the button.
                var displaySnap = string.IsNullOrWhiteSpace(answerSnap) && insertSnap != null
                    ? insertSnap
                    : answerSnap;
                DispatcherQueue.TryEnqueue(() =>
                    UpdateStreamingEntryWithThinking(
                        streamingIndex,
                        displaySnap,
                        thinkSnap,
                        isThinkingPhase: thinkPhaseSnap,
                        insertText: insertSnap));
            },
            onComplete: () => DispatcherQueue.TryEnqueue(() =>
            {
                // Flush any remaining buffered content (e.g. a partial tag at stream end)
                if (rawBuffer.Length > 0)
                {
                    answerBuilder.Append(rawBuffer);
                    rawBuffer.Clear();
                }

                var insertContent = insertBuilder.Length > 0 ? insertBuilder.ToString() : null;
                var trimmedAnswer = skillKey == "freeform"
                    ? ResponseCleaner.Clean(answerBuilder.ToString())
                    : answerBuilder.ToString().Trim();

                // Safety net: strip any tag text the parser missed due to token-boundary edge cases.
                trimmedAnswer = StripResidualTags(trimmedAnswer);
                if (insertContent is not null)
                    insertContent = StripResidualTags(insertContent);

                // If the model put all content in <insert> tags, show that in the bubble as the
                // primary text so the full response is visible in chat.
                var finalText = string.IsNullOrWhiteSpace(trimmedAnswer) && insertContent != null
                    ? insertContent
                    : trimmedAnswer;
                FinalizeStreamingEntry(
                    streamingIndex,
                    finalText,
                    thinkBuilder.ToString(),
                    insertContent);
                if (!ct.IsCancellationRequested)
                    UpdateInferenceMetrics(tokenCount, stopwatch.Elapsed);
                SendChatButton.Visibility = Visibility.Visible;
                StopChatButton.Visibility = Visibility.Collapsed;
            }),
            onError: ex => DispatcherQueue.TryEnqueue(() =>
            {
                FinalizeStreamingEntry(
                    streamingIndex,
                    string.Format(ResourceHelper.GetString("SmartSidebarErrorFormat"), ex.Message),
                    thinkBuilder.ToString());
                SendChatButton.Visibility = Visibility.Visible;
                StopChatButton.Visibility = Visibility.Collapsed;
            }),
            ct: ct);
        await _activeStreamTask;
    }

    /// <summary>
    /// Drains <paramref name="rawBuffer"/> and routes characters into thinking vs answer vs insert.
    /// Tags &lt;think&gt;, &lt;/think&gt;, &lt;insert&gt;, and &lt;/insert&gt; are consumed and not forwarded.
    /// Content inside &lt;insert&gt;…&lt;/insert&gt; goes to <paramref name="insertBuilder"/>;
    /// content outside all tags goes to <paramref name="answerBuilder"/>.
    /// </summary>
    private static void ParseThinkingToken(
        StringBuilder rawBuffer,
        StringBuilder thinkBuilder,
        StringBuilder answerBuilder,
        StringBuilder insertBuilder,
        ref bool inThinkBlock,
        ref bool inInsertBlock)
    {
        var raw = rawBuffer.ToString();
        rawBuffer.Clear();

        int i = 0;
        while (i < raw.Length)
        {
            // Check for <think> opening tag
            if (!inThinkBlock && !inInsertBlock && raw.AsSpan(i).StartsWith("<think>", StringComparison.OrdinalIgnoreCase))
            {
                inThinkBlock = true;
                i += "<think>".Length;
                continue;
            }
            // Check for </think> closing tag.
            // Also handles implicit thinking: phi-4-mini and similar models emit reasoning content
            // with no opening <think> tag and only close with </think>. In that case, everything
            // accumulated in answerBuilder so far was actually thinking content — move it over.
            if (raw.AsSpan(i).StartsWith("</think>", StringComparison.OrdinalIgnoreCase))
            {
                if (!inThinkBlock && answerBuilder.Length > 0)
                {
                    thinkBuilder.Append(answerBuilder);
                    answerBuilder.Clear();
                }
                inThinkBlock = false;
                i += "</think>".Length;
                continue;
            }
            // Check for <insert> opening tag
            if (!inThinkBlock && !inInsertBlock && raw.AsSpan(i).StartsWith("<insert>", StringComparison.OrdinalIgnoreCase))
            {
                inInsertBlock = true;
                i += "<insert>".Length;
                continue;
            }
            // Check for </insert> closing tag
            if (inInsertBlock && raw.AsSpan(i).StartsWith("</insert>", StringComparison.OrdinalIgnoreCase))
            {
                inInsertBlock = false;
                i += "</insert>".Length;
                continue;
            }
            // Partial tag at end — keep it buffered for the next token
            if (raw[i] == '<')
            {
                int remaining = raw.Length - i;
                const int maxTagLen = 9; // "</insert>" length
                if (remaining < maxTagLen)
                {
                    // Could be an incomplete tag — put remainder back in buffer and stop
                    rawBuffer.Append(raw, i, remaining);
                    break;
                }
            }

            if (inThinkBlock)
                thinkBuilder.Append(raw[i]);
            else if (inInsertBlock)
                insertBuilder.Append(raw[i]);
            else
                answerBuilder.Append(raw[i]);

            i++;
        }
    }

    /// <summary>
    /// Strips residual &lt;insert&gt;, &lt;/insert&gt;, &lt;think&gt;, and &lt;/think&gt; tag
    /// text that may have leaked through the streaming parser due to token-boundary edge cases.
    /// </summary>
    private static string StripResidualTags(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;
        // Case-insensitive removal of our known tags
        return System.Text.RegularExpressions.Regex.Replace(
            text,
            @"</?(?:insert|think)>",
            string.Empty,
            System.Text.RegularExpressions.RegexOptions.IgnoreCase).Trim();
    }

    private void AppendChatEntry(SidebarChatEntry entry)
    {
        _chatEntries.Add(entry);
        ScrollChatToBottom();
    }

    private void UpdateStreamingEntryWithThinking(int index, string text, string thinkingText, bool isThinkingPhase, string? insertText = null)
    {
        if (index < 0 || index >= _chatEntries.Count) return;
        var entry = _chatEntries[index];
        entry.Text = text;
        entry.ThinkingText = thinkingText;
        entry.IsThinkingPhase = isThinkingPhase;
        entry.ThinkingLabel = isThinkingPhase
            ? ResourceHelper.GetString("SmartSidebarThinkingLabel")
            : ResourceHelper.GetString("SmartSidebarThinkingDoneLabel");
        if (insertText != null)
            entry.InsertText = insertText;
        _pendingScroll = true;
    }

    private void UpdateStreamingEntry(int index, string text)
    {
        if (index >= 0 && index < _chatEntries.Count)
            _chatEntries[index].Text = text;
        _pendingScroll = true;
    }

    private void FinalizeStreamingEntry(int index, string text, string thinkingText = "", string? insertText = null)
    {
        if (index >= 0 && index < _chatEntries.Count)
        {
            var entry = _chatEntries[index];
            entry.Text = text;
            entry.IsStreaming = false;
            entry.ThinkingText = thinkingText;
            entry.IsThinkingPhase = false;
            entry.ThinkingLabel = !string.IsNullOrEmpty(thinkingText)
                ? ResourceHelper.GetString("SmartSidebarThinkingDoneLabel")
                : string.Empty;
            entry.InsertText = insertText;
        }

        // Expose the insert content to the UIA/Appium tree so that benchmark tests can
        // read it without relying on DataTemplate bindings (which are not reliably reflected
        // through WinAppDriver).  Same mechanism as UpdateInferenceMetrics / HardwareBadge.
        AutomationProperties.SetHelpText(ChatHistoryList, insertText ?? string.Empty);

        ScrollChatToBottom();
    }

    private void ScrollChatToBottom()
    {
        // Walk the visual tree to find the inner ScrollViewer of the ListView
        var scrollViewer = FindDescendant<ScrollViewer>(ChatHistoryList);
        scrollViewer?.ChangeView(null, scrollViewer.ScrollableHeight, null, disableAnimation: true);
    }

    private static T? FindDescendant<T>(DependencyObject parent) where T : DependencyObject
    {
        int count = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChildrenCount(parent);
        for (int i = 0; i < count; i++)
        {
            var child = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChild(parent, i);
            if (child is T match) return match;
            var result = FindDescendant<T>(child);
            if (result is not null) return result;
        }
        return null;
    }

    // ── State management ──

    private void ApplyDispatcherPendingState()
    {
        SetAiInteractionsEnabled(false);
        SetInitializationStatus(
            ResourceHelper.GetString("SmartSidebarInitializationPending"),
            isVisible: true,
            statusCode: StatusCodePending);
        HardwareBadge.Text = "…";
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(
            HardwareBadge,
            $"AI execution: {ResourceHelper.GetString("SmartSidebarExecutionPending")}");
        _hardwareModelValueText.Text = ResourceHelper.GetString("SmartSidebarExecutionPending");
        _hardwareTokensValueText.Text = ResourceHelper.GetString("SmartSidebarExecutionPending");
        ToolTipService.SetToolTip(HardwareBadge, ResourceHelper.GetString("SmartSidebarExecutionPending"));
    }

    private void ApplyDispatcherReadyState()
    {
        SetAiInteractionsEnabled(true);
        SetInitializationStatus(
            ResourceHelper.GetFormatted("SmartSidebarExecutionReady", _dispatcher.ExecutionTargetDisplayName),
            isVisible: true,
            statusCode: StatusCodeReady);
        HardwareBadge.Text = _dispatcher.ExecutionTargetDisplayName;
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(
            HardwareBadge,
            $"AI execution: {_dispatcher.ExecutionTargetDisplayName}");
        _hardwareModelValueText.Text = GetModelName();
        _hardwareTokensValueText.Text = GetPendingMetricsText();
        ToolTipService.SetToolTip(HardwareBadge, GetHardwareTooltip());
        PopulateModelMenu();
        PopulateExecutionTargetMenu();
        // Clear the status bar now that AI is ready
        ReportStatus?.Invoke(string.Empty);
    }

    private void ApplyDispatcherUnavailableState(string? failureMessage)
    {
        SetAiInteractionsEnabled(false);
        var status = GetDispatcherUnavailableStatus(failureMessage);
        SetInitializationStatus(status.Message, isVisible: true, statusCode: status.Code);
        HardwareBadge.Text = "⚠";
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(
            HardwareBadge,
            $"AI execution: {status.Message}");
        _hardwareModelValueText.Text = status.Message;
        _hardwareTokensValueText.Text = ResourceHelper.GetString("SmartSidebarExecutionPending");
        ToolTipService.SetToolTip(HardwareBadge, status.Message);
    }

    private void SetAiInteractionsEnabled(bool isEnabled)
    {
        SkillDropdown.IsEnabled = isEnabled;
        ApplySkillButton.IsEnabled = isEnabled && SkillDropdown.SelectedItem is not null;
        SendChatButton.IsEnabled = isEnabled;
        ChatInputBox.IsEnabled = isEnabled;
    }

    private (string Message, string Code) GetDispatcherUnavailableStatus(string? failureMessage)
    {
        if (!string.IsNullOrWhiteSpace(failureMessage))
        {
            return (
                ResourceHelper.GetString("SmartSidebarPrerequisiteDispatcherInitFailedStatus"),
                StatusCodeInitFailed);
        }

        var availability = _dispatcher.Availability;
        return availability switch
        {
            { PhiSilica.Status: AIBackendAvailabilityStatus.RequiresPackageIdentity } =>
                (ResourceHelper.GetString("SmartSidebarExecutionPackageIdentityRequired"), StatusCodeUnavailable),
            { PhiSilica.Status: AIBackendAvailabilityStatus.Unsupported, Gpu.IsUsable: false } =>
                (ResourceHelper.GetString("SmartSidebarExecutionUnsupported"), StatusCodeUnavailable),
            { Gpu.Status: AIBackendAvailabilityStatus.Error } =>
                (ResourceHelper.GetFormatted("SmartSidebarErrorFormat",
                    availability.Gpu.DiagnosticMessage ?? availability.Gpu.DiagnosticCode ??
                    ResourceHelper.GetString("SmartSidebarExecutionUnavailable")), StatusCodeUnavailable),
            { PhiSilica.Status: AIBackendAvailabilityStatus.Error } =>
                (ResourceHelper.GetFormatted("SmartSidebarErrorFormat",
                    availability.PhiSilica.DiagnosticMessage ?? availability.PhiSilica.DiagnosticCode ??
                    ResourceHelper.GetString("SmartSidebarExecutionUnavailable")), StatusCodeUnavailable),
            { Gpu.Status: AIBackendAvailabilityStatus.Unavailable } =>
                (ResourceHelper.GetString("SmartSidebarExecutionUnavailable"), StatusCodeUnavailable),
            _ => (ResourceHelper.GetString("SmartSidebarExecutionUnavailable"), StatusCodeUnavailable)
        };
    }

    private void SetInitializationStatus(string text, bool isVisible, string? statusCode = null)
    {
        ArgumentNullException.ThrowIfNull(text);
        InitializationStatusTextControl.Text = text;
        var automationName = string.IsNullOrWhiteSpace(statusCode) ? text : $"{statusCode}|{text}";
        AutomationProperties.SetName(InitializationStatusTextControl, automationName);
        InitializationStatusTextControl.Visibility = isVisible ? Visibility.Visible : Visibility.Collapsed;
    }

    // ── Helpers ──

    private void InitializeFlyouts()
    {
        _insertOcrButton.Click += InsertOcrButton_Click;

        var ocrPanel = new StackPanel { MinWidth = 260, MaxWidth = 320, Spacing = 8 };
        ocrPanel.Children.Add(_ocrResultTitleText);
        ocrPanel.Children.Add(_ocrResultTextBox);
        ocrPanel.Children.Add(_insertOcrButton);
        FlyoutBase.SetAttachedFlyout(OcrDropHost, new Flyout
        {
            Placement = FlyoutPlacementMode.Top,
            Content = ocrPanel,
        });

        var modelGrid = new Grid { ColumnSpacing = 8 };
        modelGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        modelGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        modelGrid.Children.Add(_hardwareModelLabelText);
        Grid.SetColumn(_hardwareModelValueText, 1);
        modelGrid.Children.Add(_hardwareModelValueText);

        var tokensGrid = new Grid { ColumnSpacing = 8 };
        tokensGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        tokensGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        tokensGrid.Children.Add(_hardwareTokensLabelText);
        Grid.SetColumn(_hardwareTokensValueText, 1);
        tokensGrid.Children.Add(_hardwareTokensValueText);

        var hardwarePanel = new StackPanel { MinWidth = 220, Spacing = 8 };
        hardwarePanel.Children.Add(_hardwareDetailsTitleText);
        hardwarePanel.Children.Add(modelGrid);
        hardwarePanel.Children.Add(tokensGrid);
        FlyoutBase.SetAttachedFlyout(HardwareBadge, new Flyout
        {
            Placement = FlyoutPlacementMode.Top,
            Content = hardwarePanel,
        });
    }

    private void ApplyLocalizedStrings()
    {
        OcrDropPromptText.Text = ResourceHelper.GetString("SmartSidebarOcrDropPrompt");
        OcrDropHintText.Text = ResourceHelper.GetString("SmartSidebarOcrDropHint");
        _ocrResultTitleText.Text = ResourceHelper.GetString("SmartSidebarOcrInsert");
        _insertOcrButton.Content = ResourceHelper.GetString("SmartSidebarOcrInsert");
        _hardwareDetailsTitleText.Text = ResourceHelper.GetString("SmartSidebarExecutionDetailsTitle");
        _hardwareModelLabelText.Text = ResourceHelper.GetString("SmartSidebarExecutionModel");
        _hardwareTokensLabelText.Text = ResourceHelper.GetString("SmartSidebarExecutionTokensPerSecond");
        _hardwareTokensValueText.Text = ResourceHelper.GetString("SmartSidebarExecutionPending");
        ResponsibleAiNoticeText.Text = ResourceHelper.GetString("SmartSidebarResponsibleAiNotice");
        var newSessionItem = OptionsFlyout.Items.OfType<MenuFlyoutItem>().First();
        newSessionItem.Text = ResourceHelper.GetString("SmartSidebarNewSession");
        AutomationProperties.SetAutomationId(newSessionItem, "NewSessionMenuItem");
        var modelSubMenu = OptionsFlyout.Items.OfType<MenuFlyoutSubItem>().First();
        modelSubMenu.Text = ResourceHelper.GetString("SmartSidebarModelSelector");
        AutomationProperties.SetAutomationId(modelSubMenu, "ModelSubMenu");
        var executionTargetSubMenu = OptionsFlyout.Items.OfType<MenuFlyoutSubItem>().Skip(1).FirstOrDefault();
        if (executionTargetSubMenu is not null)
        {
            executionTargetSubMenu.Text = ResourceHelper.GetString("SmartSidebarExecutionTarget");
            AutomationProperties.SetAutomationId(executionTargetSubMenu, "ExecutionTargetSubMenu");
        }
        ToolTipService.SetToolTip(OptionsButton, ResourceHelper.GetString("SmartSidebarOptions"));
        ToolTipService.SetToolTip(ApplySkillButton, ResourceHelper.GetString("SmartSidebarApplySkill"));
    }

    private static int EstimateTokenCount(string chunk)
    {
        if (string.IsNullOrWhiteSpace(chunk))
            return 0;
        return chunk.Split([' ', '\r', '\n', '\t'], StringSplitOptions.RemoveEmptyEntries).Length;
    }

    private void UpdateInferenceMetrics(int tokenCount, TimeSpan elapsed)
    {
        if (tokenCount <= 0 || elapsed <= TimeSpan.Zero)
            return;
        _lastTokensPerSecond = $"{tokenCount / elapsed.TotalSeconds:0.0}";
        _hardwareTokensValueText.Text = _lastTokensPerSecond;
        var tooltip = GetHardwareTooltip();
        ToolTipService.SetToolTip(HardwareBadge, tooltip);
        AutomationProperties.SetHelpText(HardwareBadge, tooltip);
    }

    private string GetHardwareTooltip()
    {
        var speedText = string.IsNullOrEmpty(_lastTokensPerSecond)
            ? ResourceHelper.GetString("SmartSidebarExecutionPending")
            : _lastTokensPerSecond;
        return $"{_dispatcher.ExecutionTargetDisplayName} • {GetModelName()} • {speedText}";
    }

    private string GetModelName()
    {
        // Prefer the alias that was actually loaded; fall back to the preferred alias
        // that will be used on the next init, then to a display label from the target name.
        if (!string.IsNullOrEmpty(_dispatcher.ActiveModelAlias))
            return _dispatcher.ActiveModelAlias;

        if (!string.IsNullOrEmpty(_dispatcher.PreferredModelAlias))
            return _dispatcher.PreferredModelAlias;

        return _dispatcher.ExecutionTargetDisplayName switch
        {
            "⚡ NPU" => "Phi Silica",
            _ => _dispatcher.ExecutionTargetDisplayName,
        };
    }

    private string GetPendingMetricsText() =>
        string.IsNullOrEmpty(_lastTokensPerSecond)
            ? ResourceHelper.GetString("SmartSidebarExecutionPending")
            : _lastTokensPerSecond;

    private async Task<string> ExtractOcrTextAsync(StorageFile file)
    {
        ArgumentNullException.ThrowIfNull(file);

        var engine = OcrEngine.TryCreateFromUserProfileLanguages();
        if (engine is null)
            return ResourceHelper.GetString("SmartSidebarOcrUnavailable");

        using var stream = await file.OpenAsync(FileAccessMode.Read);
        var decoder = await BitmapDecoder.CreateAsync(stream);
        var bitmap = await decoder.GetSoftwareBitmapAsync();
        var ocrBitmap = bitmap.BitmapPixelFormat == BitmapPixelFormat.Bgra8 && bitmap.BitmapAlphaMode == BitmapAlphaMode.Premultiplied
            ? bitmap
            : SoftwareBitmap.Convert(bitmap, BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);

        var result = await engine.RecognizeAsync(ocrBitmap);
        return result.Text;
    }

    private static bool IsSupportedImageFile(StorageFile file)
    {
        ArgumentNullException.ThrowIfNull(file);
        return file.FileType.ToLowerInvariant() is ".png" or ".jpg" or ".jpeg" or ".bmp" or ".gif" or ".tif" or ".tiff";
    }

    private void ShowOcrResult(string text)
    {
        _lastOcrText = text;
        _ocrResultTextBox.Text = text;
        FlyoutBase.ShowAttachedFlyout(OcrDropHost);
    }

    private async Task RefreshSemanticIndexAsync(IReadOnlyList<SemanticSearchDocument> documents)
    {
        var currentIds = documents.Select(static document => document.TabId).ToHashSet();
        foreach (var removedId in _indexedSemanticTabs.Where(id => !currentIds.Contains(id)).ToArray())
        {
            _dispatcher.RemoveIndexedTab(removedId);
            _indexedSemanticTabs.Remove(removedId);
        }

        foreach (var document in documents)
        {
            await _dispatcher.IndexDocumentAsync(document.TabId, document.DocumentText);
            _indexedSemanticTabs.Add(document.TabId);
        }
    }

    private static string TruncateChunk(string chunkText)
    {
        if (chunkText.Length <= 80)
            return chunkText;
        return $"{chunkText[..77]}...";
    }

    private void CancelActive()
    {
        _activeCts?.Cancel();
        _activeCts?.Dispose();
        _activeCts = null;
    }

    private sealed record SearchResultItem(int TabId, string TabName, string SearchText, string ChunkText);

    private sealed record SkillButtonViewModel(string Label, string SkillKey);
}
