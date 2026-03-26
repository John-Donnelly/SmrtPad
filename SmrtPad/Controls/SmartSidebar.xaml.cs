using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml.Controls.Primitives;
using SmrtPad.Helpers;
using SmrtPad.Services;
using SmrtPad.Services.Licensing;
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
    private readonly IAIDispatcher _dispatcher;
    private CancellationTokenSource? _activeCts;
    private CancellationTokenSource? _initializationCts;
    private Task? _initializationTask;
    private string _lastOcrText = string.Empty;
    private string _lastToneRewrite = string.Empty;
    private string _lastClarityRewrite = string.Empty;
    private string _lastGrammarFix = string.Empty;
    private string _lastShortenedText = string.Empty;
    private string _lastAutoCompleteText = string.Empty;
    private string _lastTokensPerSecond = string.Empty;
    private readonly HashSet<int> _indexedSemanticTabs = [];
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

    public SmartSidebar(IAIDispatcher dispatcher)
    {
        ArgumentNullException.ThrowIfNull(dispatcher);
        _dispatcher = dispatcher;
        InitializeComponent();
        Loaded += SmartSidebar_Loaded;
        Unloaded += SmartSidebar_Unloaded;
        InitializeFlyouts();
        ApplyLocalizedStrings();
        SemanticSection.Visibility = FeatureFlags.IsEnabled(SmrtPadFeature.SemanticSearch)
            ? Visibility.Visible
            : Visibility.Collapsed;
        ApplyDispatcherPendingState();
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
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(30));
            await _dispatcher.InitializeAsync(timeoutCts.Token);
        }

        ct.ThrowIfCancellationRequested();
        ApplyDispatcherReadyState();
    }

    // ── Header ──

    private void CloseButton_Click(object sender, RoutedEventArgs e) =>
        CloseRequested?.Invoke(this, EventArgs.Empty);

    // ── Summarize ──

    private async void SummarizeButton_Click(object sender, RoutedEventArgs e)
    {
        var text = GetSelectedText?.Invoke() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(text))
            return;

        await RunStreamingOperationAsync(
            ResourceHelper.GetString("SmartSidebarErrorFormat"),
            $"Summarize the following text concisely:\n\n{text}",
            SummarizeOutput,
            SummarizeProgress,
            StopSummarizeButton,
            onCompletedText: null);
    }

    private void StopSummarizeButton_Click(object sender, RoutedEventArgs e) =>
        CancelActive();

    // ── Tone ──

    private async void RewriteButton_Click(object sender, RoutedEventArgs e)
    {
        var text = GetRewriteSourceText?.Invoke() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(text))
        {
            ShowValidationMessage(ToneOutput);
            return;
        }

        var tone = ToneToggle.IsOn ? "professional" : "casual";
        await RunStreamingOperationAsync(
            ResourceHelper.GetString("SmartSidebarErrorFormat"),
            $"Rewrite the following text in a {tone} tone:\n\n{text}",
            ToneOutput,
            ToneProgress,
            StopToneRewriteButtonControl,
            finalText =>
            {
                _lastToneRewrite = finalText;
                DispatcherQueue.TryEnqueue(() =>
                {
                    ApplyToneRewriteButtonControl.Visibility = string.IsNullOrWhiteSpace(finalText)
                        ? Visibility.Collapsed
                        : Visibility.Visible;
                });
            });
    }

    private void StopToneRewriteButton_Click(object sender, RoutedEventArgs e) =>
        CancelActive();

    private async void ClarityRewriteButton_Click(object sender, RoutedEventArgs e)
    {
        var text = GetRewriteSourceText?.Invoke() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(text))
        {
            ShowValidationMessage(ClarityOutputControl);
            return;
        }

        await RunStreamingOperationAsync(
            ResourceHelper.GetString("SmartSidebarErrorFormat"),
            $"Rewrite the following text to improve clarity and readability:\n\n{text}",
            ClarityOutputControl,
            ClarityProgressControl,
            StopClarityRewriteButtonControl,
            finalText =>
            {
                _lastClarityRewrite = finalText;
                DispatcherQueue.TryEnqueue(() =>
                {
                    ApplyClarityRewriteButtonControl.Visibility = string.IsNullOrWhiteSpace(finalText)
                        ? Visibility.Collapsed
                        : Visibility.Visible;
                });
            });
    }

    private void StopClarityRewriteButton_Click(object sender, RoutedEventArgs e) =>
        CancelActive();

    private async void GrammarFixButton_Click(object sender, RoutedEventArgs e)
    {
        var text = GetRewriteSourceText?.Invoke() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(text))
        {
            ShowValidationMessage(GrammarFixOutputControl, ApplyGrammarFixButtonControl);
            return;
        }

        await RunStreamingOperationAsync(
            ResourceHelper.GetString("SmartSidebarErrorFormat"),
            BuildGrammarFixPrompt(text),
            GrammarFixOutputControl,
            GrammarFixProgressControl,
            StopGrammarFixButtonControl,
            finalText =>
            {
                _lastGrammarFix = finalText;
                DispatcherQueue.TryEnqueue(() =>
                {
                    ApplyGrammarFixButtonControl.Visibility = string.IsNullOrWhiteSpace(finalText)
                        ? Visibility.Collapsed
                        : Visibility.Visible;
                });
            });
    }

    private void StopGrammarFixButton_Click(object sender, RoutedEventArgs e) =>
        CancelActive();

    private async void ShortenButton_Click(object sender, RoutedEventArgs e)
    {
        var text = GetRewriteSourceText?.Invoke() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(text))
        {
            ShowValidationMessage(ShortenOutputControl, ApplyShortenButtonControl);
            return;
        }

        await RunStreamingOperationAsync(
            ResourceHelper.GetString("SmartSidebarErrorFormat"),
            BuildShortenPrompt(text),
            ShortenOutputControl,
            ShortenProgressControl,
            StopShortenButtonControl,
            finalText =>
            {
                _lastShortenedText = finalText;
                DispatcherQueue.TryEnqueue(() =>
                {
                    ApplyShortenButtonControl.Visibility = string.IsNullOrWhiteSpace(finalText)
                        ? Visibility.Collapsed
                        : Visibility.Visible;
                });
            });
    }

    private void StopShortenButton_Click(object sender, RoutedEventArgs e) =>
        CancelActive();

    private async void AutoCompleteButton_Click(object sender, RoutedEventArgs e)
    {
        var text = GetTextBeforeCaret?.Invoke() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(text))
        {
            ShowValidationMessage(AutoCompleteOutputControl, ApplyAutoCompleteButtonControl);
            return;
        }

        await RunStreamingOperationAsync(
            ResourceHelper.GetString("SmartSidebarErrorFormat"),
            BuildAutoCompletePrompt(text),
            AutoCompleteOutputControl,
            AutoCompleteProgressControl,
            StopAutoCompleteButtonControl,
            finalText =>
            {
                _lastAutoCompleteText = finalText;
                DispatcherQueue.TryEnqueue(() =>
                {
                    ApplyAutoCompleteButtonControl.Visibility = string.IsNullOrWhiteSpace(finalText)
                        ? Visibility.Collapsed
                        : Visibility.Visible;
                });
            });
    }

    private void StopAutoCompleteButton_Click(object sender, RoutedEventArgs e) =>
        CancelActive();

    private void ApplyToneRewriteButton_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_lastToneRewrite))
            return;

        ApplyToneRewrite?.Invoke(_lastToneRewrite);
    }

    private void ApplyClarityRewriteButton_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_lastClarityRewrite))
            return;

        ApplyClarityRewrite?.Invoke(_lastClarityRewrite);
    }

    private void ApplyGrammarFixButton_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_lastGrammarFix))
            return;

        ApplyGrammarFix?.Invoke(_lastGrammarFix);
    }

    private void ApplyShortenButton_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_lastShortenedText))
            return;

        ApplyShortenRewrite?.Invoke(_lastShortenedText);
    }

    private void ApplyAutoCompleteButton_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_lastAutoCompleteText))
            return;

        InsertGeneratedText?.Invoke(_lastAutoCompleteText);
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
            OcrProgressControl.IsActive = true;
            OcrProgressControl.Visibility = Visibility.Visible;

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
            OcrProgressControl.IsActive = false;
            OcrProgressControl.Visibility = Visibility.Collapsed;
        }
    }

    private void InsertOcrButton_Click(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(_lastOcrText))
        {
            InsertGeneratedText?.Invoke(_lastOcrText);
        }
    }

    // ── Badge ──

    private void HardwareBadge_Tapped(object sender, Microsoft.UI.Xaml.Input.TappedRoutedEventArgs e)
    {
        FlyoutBase.ShowAttachedFlyout(HardwareBadgeHostControl);
    }

    // ── Helpers ──

    private Button ClarityRewriteButtonControl => (Button)FindName("ClarityRewriteButton")!;

    private StackPanel AssistSectionControl => (StackPanel)FindName("AssistSection")!;

    private TextBlock GrammarSectionTitleTextControl => (TextBlock)FindName("GrammarSectionTitleText")!;

    private TextBlock ShortenSectionTitleTextControl => (TextBlock)FindName("ShortenSectionTitleText")!;

    private TextBlock AutoCompleteSectionTitleTextControl => (TextBlock)FindName("AutoCompleteSectionTitleText")!;

    private Button GrammarFixButtonControl => (Button)FindName("GrammarFixButton")!;

    private Button ShortenButtonControl => (Button)FindName("ShortenButton")!;

    private Button AutoCompleteButtonControl => (Button)FindName("AutoCompleteButton")!;

    private Button StopToneRewriteButtonControl => (Button)FindName("StopToneRewriteButton")!;

    private Button StopClarityRewriteButtonControl => (Button)FindName("StopClarityRewriteButton")!;

    private Button StopGrammarFixButtonControl => (Button)FindName("StopGrammarFixButton")!;

    private Button StopShortenButtonControl => (Button)FindName("StopShortenButton")!;

    private Button StopAutoCompleteButtonControl => (Button)FindName("StopAutoCompleteButton")!;

    private Button ApplyClarityRewriteButtonControl => (Button)FindName("ApplyClarityRewriteButton")!;

    private Button ApplyToneRewriteButtonControl => (Button)FindName("ApplyToneRewriteButton")!;

    private Button ApplyGrammarFixButtonControl => (Button)FindName("ApplyGrammarFixButton")!;

    private Button ApplyShortenButtonControl => (Button)FindName("ApplyShortenButton")!;

    private Button ApplyAutoCompleteButtonControl => (Button)FindName("ApplyAutoCompleteButton")!;

    private TextBlock ClarityOutputControl => (TextBlock)FindName("ClarityOutput")!;

    private TextBlock GrammarFixOutputControl => (TextBlock)FindName("GrammarFixOutput")!;

    private TextBlock ShortenOutputControl => (TextBlock)FindName("ShortenOutput")!;

    private TextBlock AutoCompleteOutputControl => (TextBlock)FindName("AutoCompleteOutput")!;

    private TextBlock ResponsibleAiNoticeTextControl => (TextBlock)FindName("ResponsibleAiNoticeText")!;

    private ProgressRing ClarityProgressControl => (ProgressRing)FindName("ClarityProgress")!;

    private ProgressRing GrammarFixProgressControl => (ProgressRing)FindName("GrammarFixProgress")!;

    private ProgressRing ShortenProgressControl => (ProgressRing)FindName("ShortenProgress")!;

    private ProgressRing AutoCompleteProgressControl => (ProgressRing)FindName("AutoCompleteProgress")!;

    private Grid OcrDropHostControl => (Grid)FindName("OcrDropHost")!;

    private TextBlock OcrDropPromptTextControl => (TextBlock)FindName("OcrDropPromptText")!;

    private TextBlock OcrDropHintTextControl => (TextBlock)FindName("OcrDropHintText")!;

    private ProgressRing OcrProgressControl => (ProgressRing)FindName("OcrProgress")!;

    private Grid HardwareBadgeHostControl => (Grid)FindName("HardwareBadgeHost")!;

    private void InitializeFlyouts()
    {
        _insertOcrButton.Click += InsertOcrButton_Click;

        var ocrPanel = new StackPanel { MinWidth = 260, MaxWidth = 320, Spacing = 8 };
        ocrPanel.Children.Add(_ocrResultTitleText);
        ocrPanel.Children.Add(_ocrResultTextBox);
        ocrPanel.Children.Add(_insertOcrButton);
        FlyoutBase.SetAttachedFlyout(OcrDropHostControl, new Flyout
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
        FlyoutBase.SetAttachedFlyout(HardwareBadgeHostControl, new Flyout
        {
            Placement = FlyoutPlacementMode.Top,
            Content = hardwarePanel,
        });
    }

    private void ApplyLocalizedStrings()
    {
        GrammarSectionTitleTextControl.Text = ResourceHelper.GetString("SmartSidebarGrammarSectionTitle");
        ShortenSectionTitleTextControl.Text = ResourceHelper.GetString("SmartSidebarShortenSectionTitle");
        AutoCompleteSectionTitleTextControl.Text = ResourceHelper.GetString("SmartSidebarAutoCompleteSectionTitle");
        GrammarFixButtonControl.Content = ResourceHelper.GetString("SmartSidebarGrammarFix");
        ShortenButtonControl.Content = ResourceHelper.GetString("SmartSidebarShorten");
        AutoCompleteButtonControl.Content = ResourceHelper.GetString("SmartSidebarAutoComplete");
        StopSummarizeButton.Content = ResourceHelper.GetString("SmartSidebarCancel");
        StopToneRewriteButtonControl.Content = ResourceHelper.GetString("SmartSidebarCancel");
        StopClarityRewriteButtonControl.Content = ResourceHelper.GetString("SmartSidebarCancel");
        StopGrammarFixButtonControl.Content = ResourceHelper.GetString("SmartSidebarCancel");
        StopShortenButtonControl.Content = ResourceHelper.GetString("SmartSidebarCancel");
        StopAutoCompleteButtonControl.Content = ResourceHelper.GetString("SmartSidebarCancel");
        ClarityRewriteButtonControl.Content = ResourceHelper.GetString("SmartSidebarRewriteForClarity");
        OcrDropPromptTextControl.Text = ResourceHelper.GetString("SmartSidebarOcrDropPrompt");
        OcrDropHintTextControl.Text = ResourceHelper.GetString("SmartSidebarOcrDropHint");
        _ocrResultTitleText.Text = ResourceHelper.GetString("SmartSidebarOcrInsert");
        _insertOcrButton.Content = ResourceHelper.GetString("SmartSidebarOcrInsert");
        _hardwareDetailsTitleText.Text = ResourceHelper.GetString("SmartSidebarExecutionDetailsTitle");
        _hardwareModelLabelText.Text = ResourceHelper.GetString("SmartSidebarExecutionModel");
        _hardwareTokensLabelText.Text = ResourceHelper.GetString("SmartSidebarExecutionTokensPerSecond");
        _hardwareTokensValueText.Text = ResourceHelper.GetString("SmartSidebarExecutionPending");
        ResponsibleAiNoticeTextControl.Text = ResourceHelper.GetString("SmartSidebarResponsibleAiNotice");
        ApplyToneRewriteButtonControl.Content = ResourceHelper.GetString("SmartSidebarApplyGeneratedText");
        ApplyClarityRewriteButtonControl.Content = ResourceHelper.GetString("SmartSidebarApplyGeneratedText");
        ApplyGrammarFixButtonControl.Content = ResourceHelper.GetString("SmartSidebarApplyGeneratedText");
        ApplyShortenButtonControl.Content = ResourceHelper.GetString("SmartSidebarApplyGeneratedText");
        ApplyAutoCompleteButtonControl.Content = ResourceHelper.GetString("SmartSidebarInsertGeneratedText");
    }

    private void ApplyDispatcherPendingState()
    {
        SetAiInteractionsEnabled(false);
        SetInitializationStatus(
            ResourceHelper.GetString("SmartSidebarInitializationPending"),
            isVisible: true);
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
            isVisible: true);
        HardwareBadge.Text = _dispatcher.ExecutionTargetDisplayName;
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(
            HardwareBadge,
            $"AI execution: {_dispatcher.ExecutionTargetDisplayName}");
        _hardwareModelValueText.Text = GetModelName();
        _hardwareTokensValueText.Text = GetPendingMetricsText();
        ToolTipService.SetToolTip(HardwareBadge, GetHardwareTooltip());
    }

    private void ApplyDispatcherUnavailableState(string? failureMessage)
    {
        SetAiInteractionsEnabled(false);

        var availabilityMessage = GetDispatcherUnavailableMessage(failureMessage);
        SetInitializationStatus(availabilityMessage, isVisible: true);
        HardwareBadge.Text = "⚠";
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(
            HardwareBadge,
            $"AI execution: {availabilityMessage}");
        _hardwareModelValueText.Text = availabilityMessage;
        _hardwareTokensValueText.Text = ResourceHelper.GetString("SmartSidebarExecutionPending");
        ToolTipService.SetToolTip(HardwareBadge, availabilityMessage);
    }

    private void SetAiInteractionsEnabled(bool isEnabled)
    {
        SetSectionInteractivity(SummarizeSection, isEnabled);
        SetSectionInteractivity(ToneSection, isEnabled);
        SetSectionInteractivity(AssistSectionControl, isEnabled);
    }

    private static void SetSectionInteractivity(UIElement section, bool isEnabled)
    {
        ArgumentNullException.ThrowIfNull(section);

        section.IsHitTestVisible = isEnabled;
        if (section is FrameworkElement element)
            element.Opacity = isEnabled ? 1d : 0.6d;
    }

    private string GetDispatcherUnavailableMessage(string? failureMessage)
    {
        if (!string.IsNullOrWhiteSpace(failureMessage))
            return ResourceHelper.GetFormatted("SmartSidebarErrorFormat", failureMessage);

        var availability = _dispatcher.Availability;
        return availability switch
        {
            { PhiSilica.Status: AIBackendAvailabilityStatus.RequiresPackageIdentity } =>
                ResourceHelper.GetString("SmartSidebarExecutionPackageIdentityRequired"),
            { PhiSilica.Status: AIBackendAvailabilityStatus.Unsupported, FoundryGpu.IsUsable: false } =>
                ResourceHelper.GetString("SmartSidebarExecutionUnsupported"),
            { FoundryGpu.Status: AIBackendAvailabilityStatus.Error } =>
                ResourceHelper.GetFormatted("SmartSidebarErrorFormat", availability.FoundryGpu.DiagnosticMessage ?? availability.FoundryGpu.DiagnosticCode ?? ResourceHelper.GetString("SmartSidebarExecutionUnavailable")),
            { PhiSilica.Status: AIBackendAvailabilityStatus.Error } =>
                ResourceHelper.GetFormatted("SmartSidebarErrorFormat", availability.PhiSilica.DiagnosticMessage ?? availability.PhiSilica.DiagnosticCode ?? ResourceHelper.GetString("SmartSidebarExecutionUnavailable")),
            { FoundryGpu.Status: AIBackendAvailabilityStatus.Unavailable } =>
                ResourceHelper.GetString("SmartSidebarExecutionUnavailable"),
            _ => ResourceHelper.GetString("SmartSidebarExecutionUnavailable")
        };
    }

    private void SetInitializationStatus(string text, bool isVisible)
    {
        ArgumentNullException.ThrowIfNull(text);

        InitializationStatusTextControl.Text = text;
        InitializationStatusTextControl.Visibility = isVisible ? Visibility.Visible : Visibility.Collapsed;
    }

    private async Task RunStreamingOperationAsync(
        string errorFormat,
        string prompt,
        TextBlock output,
        ProgressRing progress,
        UIElement? stopButton,
        Action<string>? onCompletedText)
    {
        ArgumentNullException.ThrowIfNull(errorFormat);
        ArgumentNullException.ThrowIfNull(prompt);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(progress);

        CancelActive();
        _activeCts = new CancellationTokenSource();
        var ct = _activeCts.Token;
        var builder = new StringBuilder();
        var tokenCount = 0;
        var stopwatch = Stopwatch.StartNew();

        output.Text = string.Empty;
        progress.IsActive = true;
        progress.Visibility = Visibility.Visible;
        if (stopButton is not null)
            stopButton.Visibility = Visibility.Visible;

        if (ReferenceEquals(output, ToneOutput))
        {
            _lastToneRewrite = string.Empty;
            ApplyToneRewriteButtonControl.Visibility = Visibility.Collapsed;
        }
        else if (ReferenceEquals(output, ClarityOutputControl))
        {
            _lastClarityRewrite = string.Empty;
            ApplyClarityRewriteButtonControl.Visibility = Visibility.Collapsed;
        }
        else if (ReferenceEquals(output, GrammarFixOutputControl))
        {
            _lastGrammarFix = string.Empty;
            ApplyGrammarFixButtonControl.Visibility = Visibility.Collapsed;
        }
        else if (ReferenceEquals(output, ShortenOutputControl))
        {
            _lastShortenedText = string.Empty;
            ApplyShortenButtonControl.Visibility = Visibility.Collapsed;
        }
        else if (ReferenceEquals(output, AutoCompleteOutputControl))
        {
            _lastAutoCompleteText = string.Empty;
            ApplyAutoCompleteButtonControl.Visibility = Visibility.Collapsed;
        }

        await _dispatcher.StreamResponseAsync(
            prompt,
            onToken: token =>
            {
                lock (builder)
                {
                    builder.Append(token);
                }

                tokenCount += EstimateTokenCount(token);
                DispatcherQueue.TryEnqueue(() => output.Text += token);
            },
            onComplete: () => DispatcherQueue.TryEnqueue(() =>
            {
                CloseProgressState(progress, stopButton);
                if (ct.IsCancellationRequested)
                {
                    if (ReferenceEquals(output, ToneOutput))
                        ApplyToneRewriteButtonControl.Visibility = Visibility.Collapsed;
                    else if (ReferenceEquals(output, ClarityOutputControl))
                        ApplyClarityRewriteButtonControl.Visibility = Visibility.Collapsed;
                    else if (ReferenceEquals(output, GrammarFixOutputControl))
                        ApplyGrammarFixButtonControl.Visibility = Visibility.Collapsed;
                    else if (ReferenceEquals(output, ShortenOutputControl))
                        ApplyShortenButtonControl.Visibility = Visibility.Collapsed;
                    else if (ReferenceEquals(output, AutoCompleteOutputControl))
                        ApplyAutoCompleteButtonControl.Visibility = Visibility.Collapsed;

                    return;
                }

                UpdateInferenceMetrics(tokenCount, stopwatch.Elapsed);
                onCompletedText?.Invoke(builder.ToString());
            }),
            onError: ex => DispatcherQueue.TryEnqueue(() =>
            {
                output.Text = string.Format(errorFormat, ex.Message);
                CloseProgressState(progress, stopButton);
                if (ReferenceEquals(output, ToneOutput))
                    ApplyToneRewriteButtonControl.Visibility = Visibility.Collapsed;
                else if (ReferenceEquals(output, ClarityOutputControl))
                    ApplyClarityRewriteButtonControl.Visibility = Visibility.Collapsed;
                else if (ReferenceEquals(output, GrammarFixOutputControl))
                    ApplyGrammarFixButtonControl.Visibility = Visibility.Collapsed;
                else if (ReferenceEquals(output, ShortenOutputControl))
                    ApplyShortenButtonControl.Visibility = Visibility.Collapsed;
                else if (ReferenceEquals(output, AutoCompleteOutputControl))
                    ApplyAutoCompleteButtonControl.Visibility = Visibility.Collapsed;
            }),
            ct: ct);
    }

    private static string BuildGrammarFixPrompt(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return $"Correct grammar, punctuation, and spelling in the following text without changing its meaning or tone. Return only the corrected text:\n\n{text}";
    }

    private static string BuildShortenPrompt(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return $"Shorten the following text while preserving its meaning and key details. Return only the revised text:\n\n{text}";
    }

    private static string BuildAutoCompletePrompt(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return $"Continue the current sentence based on the existing context. Return only the completion text, keep it concise, and do not start a new paragraph:\n\n{text}";
    }

    private void ShowValidationMessage(TextBlock output, Button? applyButton = null)
    {
        ArgumentNullException.ThrowIfNull(output);

        output.Text = ResourceHelper.GetString("SmartSidebarSelectionRequired");
        if (applyButton is not null)
            applyButton.Visibility = Visibility.Collapsed;
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
        ToolTipService.SetToolTip(HardwareBadge, GetHardwareTooltip());
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
        return _dispatcher.ExecutionTargetDisplayName switch
        {
            "⚡ NPU" => "Phi Silica",
            "🖥️ GPU" => "phi-3.5-mini-instruct",
            "🐢 CPU" => "phi-3.5-mini-instruct-generic-cpu",
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
        FlyoutBase.ShowAttachedFlyout(OcrDropHostControl);
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

    private sealed record SearchResultItem(int TabId, string TabName, string SearchText, string ChunkText);

    private static void CloseProgressState(ProgressRing progress, UIElement? stopButton)
    {
        progress.IsActive = false;
        progress.Visibility = Visibility.Collapsed;
        if (stopButton is not null)
            stopButton.Visibility = Visibility.Collapsed;
    }

    private void CancelActive()
    {
        _activeCts?.Cancel();
        _activeCts?.Dispose();
        _activeCts = null;
    }
}
