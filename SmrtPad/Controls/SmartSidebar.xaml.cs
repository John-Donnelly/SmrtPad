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
    private string _lastOcrText = string.Empty;
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

    /// <summary>Raised when the user clicks the close button.</summary>
    public event EventHandler? CloseRequested;

    /// <summary>Delegate the sidebar calls to get the currently selected text from the active editor.</summary>
    public Func<string>? GetSelectedText { get; set; }

    /// <summary>Delegate the sidebar calls to get the current rewrite source from the active editor.</summary>
    public Func<string>? GetRewriteSourceText { get; set; }

    /// <summary>Delegate invoked when a tone rewrite should replace text in the active editor.</summary>
    public Action<string>? ApplyToneRewrite { get; set; }

    /// <summary>Delegate invoked when a clarity rewrite should replace text in the active editor.</summary>
    public Action<string>? ApplyClarityRewrite { get; set; }

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
        InitializeFlyouts();
        ApplyLocalizedStrings();
        SemanticSection.Visibility = FeatureFlags.IsEnabled(SmrtPadFeature.SemanticSearch)
            ? Visibility.Visible
            : Visibility.Collapsed;
        _ = InitializeDispatcherAsync();
    }

    private async Task InitializeDispatcherAsync()
    {
        try
        {
            if (!_dispatcher.IsInitialized)
                await _dispatcher.InitializeAsync();

            DispatcherQueue.TryEnqueue(() =>
            {
                HardwareBadge.Text = _dispatcher.ExecutionTargetDisplayName;
                Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(
                    HardwareBadge, $"AI execution: {_dispatcher.ExecutionTargetDisplayName}");
                ToolTipService.SetToolTip(HardwareBadge, GetHardwareTooltip());
                _hardwareModelValueText.Text = GetModelName();
                _hardwareTokensValueText.Text = GetPendingMetricsText();
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"AI init failed: {ex.Message}");
        }
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
            return;

        var tone = ToneToggle.IsOn ? "professional" : "casual";
        await RunStreamingOperationAsync(
            ResourceHelper.GetString("SmartSidebarErrorFormat"),
            $"Rewrite the following text in a {tone} tone:\n\n{text}",
            ToneOutput,
            ToneProgress,
            null,
            finalText => ApplyToneRewrite?.Invoke(finalText));
    }

    private async void ClarityRewriteButton_Click(object sender, RoutedEventArgs e)
    {
        var text = GetRewriteSourceText?.Invoke() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(text))
            return;

        await RunStreamingOperationAsync(
            ResourceHelper.GetString("SmartSidebarErrorFormat"),
            $"Rewrite the following text to improve clarity and readability:\n\n{text}",
            ClarityOutputControl,
            ClarityProgressControl,
            null,
            finalText => ApplyClarityRewrite?.Invoke(finalText));
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

    private TextBlock ClarityOutputControl => (TextBlock)FindName("ClarityOutput")!;

    private ProgressRing ClarityProgressControl => (ProgressRing)FindName("ClarityProgress")!;

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
        ClarityRewriteButtonControl.Content = ResourceHelper.GetString("SmartSidebarRewriteForClarity");
        OcrDropPromptTextControl.Text = ResourceHelper.GetString("SmartSidebarOcrDropPrompt");
        OcrDropHintTextControl.Text = ResourceHelper.GetString("SmartSidebarOcrDropHint");
        _ocrResultTitleText.Text = ResourceHelper.GetString("SmartSidebarOcrInsert");
        _insertOcrButton.Content = ResourceHelper.GetString("SmartSidebarOcrInsert");
        _hardwareDetailsTitleText.Text = ResourceHelper.GetString("SmartSidebarExecutionDetailsTitle");
        _hardwareModelLabelText.Text = ResourceHelper.GetString("SmartSidebarExecutionModel");
        _hardwareTokensLabelText.Text = ResourceHelper.GetString("SmartSidebarExecutionTokensPerSecond");
        _hardwareTokensValueText.Text = ResourceHelper.GetString("SmartSidebarExecutionPending");
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
                UpdateInferenceMetrics(tokenCount, stopwatch.Elapsed);
                onCompletedText?.Invoke(builder.ToString());
            }),
            onError: ex => DispatcherQueue.TryEnqueue(() =>
            {
                output.Text = string.Format(errorFormat, ex.Message);
                CloseProgressState(progress, stopButton);
            }),
            ct: ct);
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
