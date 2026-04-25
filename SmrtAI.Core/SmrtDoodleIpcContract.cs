namespace SmrtAI.Core.Ipc;

public static class SmrtDoodleIpc
{
    public const string ProtocolScheme = "smrtdoodle";
    public const string StoreSearchUri = "ms-windows-store://search/?query=SmrtDoodle";
    public const string PipeQueryKey = "pipe";
    public const string SchemaQueryKey = "v";
    public const int CurrentSchemaVersion = 1;
    public const string PipeNamePrefix = "SmrtSuite.ImageBridge.";
    public const string CommandEditImage = "edit-image";
    public const string CommandImageReady = "image-ready";
    public const string CommandCancelled = "cancelled";
    public static string BuildLaunchUri(string pipeName)
        => $"{ProtocolScheme}://edit?{PipeQueryKey}={System.Uri.EscapeDataString(pipeName)}&{SchemaQueryKey}={CurrentSchemaVersion}";
    public static string NewPipeName() => PipeNamePrefix + System.Guid.NewGuid().ToString("N");
}

public sealed record SmrtDoodleImageMessage(
    string Command,
    int SchemaVersion,
    string? ImagePngBase64 = null,
    string? Message = null);
