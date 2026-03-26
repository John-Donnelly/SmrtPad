using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Windows.UI.Input.Inking;
using Windows.UI.Input.Inking.Analysis;
using SmrtPad.Services.Licensing;

namespace SmrtPad.Services;

public interface IInkService
{
    Task<string> RecognizeAsync(IReadOnlyList<InkStroke> strokes, CancellationToken ct = default);
}

internal sealed class InkService : IInkService
{
    public async Task<string> RecognizeAsync(IReadOnlyList<InkStroke> strokes, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(strokes);
        ct.ThrowIfCancellationRequested();

        if (strokes.Count == 0)
        {
            return string.Empty;
        }

        if (FeatureFlags.IsEnabled(SmrtPadFeature.InkAnalytics))
        {
            string analyticsResult = await RecognizeWithAnalyzerAsync(strokes, ct).ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(analyticsResult))
            {
                return analyticsResult;
            }
        }

        return await RecognizeWithRecognizerAsync(strokes, ct).ConfigureAwait(false);
    }

    private static async Task<string> RecognizeWithAnalyzerAsync(IReadOnlyList<InkStroke> strokes, CancellationToken ct)
    {
        var analyzer = new InkAnalyzer();
        analyzer.AddDataForStrokes(strokes);

        try
        {
            InkAnalysisResult analysisResult = await analyzer.AnalyzeAsync().AsTask(ct).ConfigureAwait(false);
            if (analysisResult.Status != InkAnalysisStatus.Updated)
            {
                return string.Empty;
            }

            var lineNodes = analyzer.AnalysisRoot.FindNodes(InkAnalysisNodeKind.Line)
                .OfType<InkAnalysisLine>()
                .OrderBy(line => line.BoundingRect.Top)
                .ToList();

            if (lineNodes.Count == 0)
            {
                return string.Empty;
            }

            var drawingStrokeIds = analyzer.AnalysisRoot.FindNodes(InkAnalysisNodeKind.InkDrawing)
                .OfType<InkAnalysisInkDrawing>()
                .SelectMany(node => node.GetStrokeIds())
                .ToHashSet();

            List<string> lines = [];
            foreach (InkAnalysisLine line in lineNodes)
            {
                ct.ThrowIfCancellationRequested();

                if (line.GetStrokeIds().All(id => !drawingStrokeIds.Contains(id))
                    && !string.IsNullOrWhiteSpace(line.RecognizedText))
                {
                    lines.Add(line.RecognizedText.Trim());
                }
            }

            return string.Join(Environment.NewLine, lines);
        }
        finally
        {
            analyzer.ClearDataForAllStrokes();
        }
    }

    private static async Task<string> RecognizeWithRecognizerAsync(IReadOnlyList<InkStroke> strokes, CancellationToken ct)
    {
        var container = new InkStrokeContainer();
        foreach (InkStroke stroke in strokes)
        {
            ct.ThrowIfCancellationRequested();
            container.AddStroke(stroke.Clone());
        }

        var recognizer = new InkRecognizerContainer();
        IReadOnlyList<InkRecognitionResult> results = await recognizer
            .RecognizeAsync(container, InkRecognitionTarget.All)
            .AsTask(ct)
            .ConfigureAwait(false);

        List<string> lines = [];
        foreach (InkRecognitionResult result in results)
        {
            ct.ThrowIfCancellationRequested();
            string? candidate = result.GetTextCandidates().FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(candidate))
            {
                lines.Add(candidate.Trim());
            }
        }

        return string.Join(Environment.NewLine, lines);
    }
}
