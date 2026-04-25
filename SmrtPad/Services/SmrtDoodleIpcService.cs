using System;
using System.IO.Pipes;
using System.Threading;
using System.Threading.Tasks;
using Windows.System;

namespace SmrtPad.Services;

/// <summary>
/// SmrtPad-side of the SmrtPad↔SmrtDoodle "Insert Drawing" workflow.
///
/// Flow:
/// <list type="number">
///   <item>SmrtPad creates a per-session named-pipe server.</item>
///   <item>SmrtPad launches SmrtDoodle via <c>smrtdoodle://edit?pipe={name}&amp;v=1</c>.</item>
///   <item>SmrtDoodle connects to the pipe and reads the optional source PNG frame.</item>
///   <item>When the user inserts/cancels, SmrtDoodle writes back one frame and closes the pipe.</item>
/// </list>
/// Named pipes are used because Windows App SDK / WinUI 3 cannot host an
/// <c>AppServiceConnection</c> (no <c>OnBackgroundActivated</c>).
/// </summary>
internal sealed class SmrtDoodleIpcService : IAsyncDisposable
{
    private NamedPipeServerStream? _server;

    /// <summary>Whether SmrtDoodle's protocol handler is registered on this machine.</summary>
    public static async Task<bool> IsSmrtDoodleAvailableAsync()
    {
        try
        {
            var status = await Launcher.QueryUriSupportAsync(
                new Uri($"{SmrtDoodleIpc.ProtocolScheme}://probe"),
                LaunchQuerySupportType.Uri);
            return status == LaunchQuerySupportStatus.Available;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Launches SmrtDoodle (handing off <paramref name="sourceImagePng"/> if provided) and
    /// waits until it sends back the edited PNG or a cancellation frame.
    /// </summary>
    /// <returns>The PNG bytes returned by SmrtDoodle, or <c>null</c> when cancelled.</returns>
    public async Task<byte[]?> EditImageAsync(byte[]? sourceImagePng, CancellationToken ct = default)
    {
        var pipeName = SmrtDoodleIpc.NewPipeName();

        // Direction: in/out so SmrtPad can both push the source image and pull the result.
        _server = new NamedPipeServerStream(
            pipeName,
            PipeDirection.InOut,
            maxNumberOfServerInstances: 1,
            PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous);

        var launchUri = new Uri(SmrtDoodleIpc.BuildLaunchUri(pipeName));
        if (!await Launcher.LaunchUriAsync(launchUri))
            return null;

        // SmrtDoodle has up to 90s to connect after the protocol activation. If the user
        // dismisses the activation prompt or the launch fails silently, the wait completes
        // when the operation is cancelled by the caller.
        using (var connectCts = CancellationTokenSource.CreateLinkedTokenSource(ct))
        {
            connectCts.CancelAfter(TimeSpan.FromSeconds(90));
            try
            {
                await _server.WaitForConnectionAsync(connectCts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return null;
            }
        }

        // Send the (optional) source image so SmrtDoodle can preload it into the canvas.
        var openingFrame = new SmrtDoodleImageMessage(
            Command: SmrtDoodleIpc.CommandEditImage,
            SchemaVersion: SmrtDoodleIpc.CurrentSchemaVersion,
            ImagePngBase64: sourceImagePng is null ? null : SmrtDoodleFrame.Encode(sourceImagePng));
        await SmrtDoodleFrame.WriteAsync(_server, openingFrame, ct).ConfigureAwait(false);

        // Wait for the reply frame from SmrtDoodle.
        var reply = await SmrtDoodleFrame.ReadAsync(_server, ct).ConfigureAwait(false);
        if (reply is null) return null;

        return reply.Command == SmrtDoodleIpc.CommandImageReady
            ? SmrtDoodleFrame.Decode(reply.ImagePngBase64)
            : null;
    }

    public ValueTask DisposeAsync()
    {
        if (_server is not null)
        {
            try { _server.Dispose(); } catch { /* best-effort */ }
            _server = null;
        }
        return ValueTask.CompletedTask;
    }
}
