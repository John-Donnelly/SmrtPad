namespace SmrtPad.Services;

/// <summary>Describes the availability state for a specific AI backend.</summary>
public enum AIBackendAvailabilityStatus
{
    /// <summary>The backend has not yet been evaluated.</summary>
    Unknown,

    /// <summary>The backend is ready to initialize.</summary>
    Available,

    /// <summary>The backend is supported but still requires model preparation.</summary>
    InstallRequired,

    /// <summary>The backend requires package identity or registration before it can be used.</summary>
    RequiresPackageIdentity,

    /// <summary>The backend is not supported on the current system.</summary>
    Unsupported,

    /// <summary>The backend was evaluated but no compatible device was found.</summary>
    Unavailable,

    /// <summary>The backend probe failed unexpectedly.</summary>
    Error,
}

/// <summary>Captures the availability result for a specific AI backend.</summary>
public sealed record AIBackendAvailability(
    string BackendName,
    AIBackendAvailabilityStatus Status,
    string? DiagnosticCode,
    string? DiagnosticMessage,
    long GpuVramMb = 0,
    long AvailableSystemRamMb = 0)
{
    /// <summary>Whether the backend can still be selected for initialization.</summary>
    public bool IsUsable =>
        Status is AIBackendAvailabilityStatus.Available or AIBackendAvailabilityStatus.InstallRequired;
}

/// <summary>Captures the latest backend availability snapshot reported by the AI dispatcher.</summary>
public sealed record AIDispatcherAvailability(
    string SelectedTarget,
    AIBackendAvailability PhiSilica,
    AIBackendAvailability FoundryGpu)
{
    /// <summary>Default state before AI capabilities have been evaluated.</summary>
    public static AIDispatcherAvailability Uninitialized { get; } = new(
        SelectedTarget: "FoundryLocalCpu",
        PhiSilica: new AIBackendAvailability("Phi Silica", AIBackendAvailabilityStatus.Unknown, null, null),
        FoundryGpu: new AIBackendAvailability("Foundry Local GPU", AIBackendAvailabilityStatus.Unknown, null, null));
}
