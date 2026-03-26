namespace SmrtPad.AI;

using System.Runtime.InteropServices;
using System.Management;

/// <summary>Describes the AI execution backend selected by hardware probing.</summary>
public enum AIExecutionTarget
{
    /// <summary>On-device NPU via Phi Silica (Copilot+ PCs).</summary>
    PhiSilicaNpu,

    /// <summary>Local GPU via Foundry Local SDK.</summary>
    FoundryLocalGpu,

    /// <summary>Local CPU via Foundry Local SDK (fallback).</summary>
    FoundryLocalCpu,
}

/// <summary>Describes the availability state for an AI backend.</summary>
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

/// <summary>Captures the capability result for a single AI backend probe.</summary>
public sealed record AIBackendCapability(
    string BackendName,
    AIBackendAvailabilityStatus Status,
    string? DiagnosticCode = null,
    string? DiagnosticMessage = null,
    long GpuVramMb = 0,
    long AvailableSystemRamMb = 0)
{
    /// <summary>Whether the backend can still be selected for initialization.</summary>
    public bool IsUsable =>
        Status is AIBackendAvailabilityStatus.Available or AIBackendAvailabilityStatus.InstallRequired;
}

/// <summary>Captures the outcome of selecting the best available AI execution target.</summary>
public sealed record HardwareProbeResult(
    AIExecutionTarget SelectedTarget,
    AIBackendCapability PhiSilica,
    AIBackendCapability FoundryGpu)
{
    /// <summary>Default probe state before any detection has run.</summary>
    public static HardwareProbeResult Uninitialized { get; } = new(
        AIExecutionTarget.FoundryLocalCpu,
        new AIBackendCapability("Phi Silica", AIBackendAvailabilityStatus.Unknown),
        new AIBackendCapability("Foundry Local GPU", AIBackendAvailabilityStatus.Unknown));
}

/// <summary>Abstracts hardware capability queries for testability.</summary>
public interface IExecutionProviderCatalogAdapter
{
    /// <summary>Returns the capability result for the Phi Silica NPU path.</summary>
    Task<AIBackendCapability> ProbePhiSilicaAsync(CancellationToken ct);

    /// <summary>Returns the capability result for the Foundry Local GPU path.</summary>
    Task<AIBackendCapability> ProbeFoundryGpuAsync(CancellationToken ct);
}

/// <summary>
/// Probes local hardware to determine the best AI execution target.
/// Priority: NPU → GPU → CPU.
/// </summary>
public sealed class HardwareProbeService
{
    private readonly IExecutionProviderCatalogAdapter _catalog;

    public HardwareProbeService(IExecutionProviderCatalogAdapter catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        _catalog = catalog;
    }

    /// <summary>
    /// Detects the best available execution target and captures backend diagnostics.
    /// </summary>
    public async Task<HardwareProbeResult> DetectAsync(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        var phiSilica = await _catalog.ProbePhiSilicaAsync(ct).ConfigureAwait(false);
        if (phiSilica.IsUsable)
        {
            return new HardwareProbeResult(
                AIExecutionTarget.PhiSilicaNpu,
                phiSilica,
                new AIBackendCapability("Foundry Local GPU", AIBackendAvailabilityStatus.Unknown));
        }

        ct.ThrowIfCancellationRequested();

        var foundryGpu = await _catalog.ProbeFoundryGpuAsync(ct).ConfigureAwait(false);
        if (foundryGpu.IsUsable)
        {
            return new HardwareProbeResult(AIExecutionTarget.FoundryLocalGpu, phiSilica, foundryGpu);
        }

        return new HardwareProbeResult(AIExecutionTarget.FoundryLocalCpu, phiSilica, foundryGpu);
    }

    /// <summary>Queries dedicated GPU VRAM via DXGI. Returns 0 on failure.</summary>
    internal static long QueryDxgiVramMb()
    {
        try
        {
            var hr = NativeMethods.CreateDXGIFactory1(NativeMethods.IID_IDXGIFactory1, out nint factory);
            if (hr != 0 || factory == nint.Zero)
                return 0;

            try
            {
                long bestVram = 0;
                uint index = 0;
                while (true)
                {
                    int enumHr = NativeMethods.EnumAdapters1(factory, index++, out nint adapter);
                    // DXGI_ERROR_NOT_FOUND = 0x887A0002
                    if (enumHr == unchecked((int)0x887A0002) || adapter == nint.Zero)
                        break;

                    try
                    {
                        // DXGI_ADAPTER_DESC1: Description[128 chars = 256 bytes], VendorId(4), DeviceId(4),
                        // SubSysId(4), Revision(4), DedicatedVideoMemory(8) at offset 272
                        var desc = new NativeMethods.DXGI_ADAPTER_DESC1();
                        if (NativeMethods.GetDesc1(adapter, out desc) == 0)
                        {
                            long vram = (long)(desc.DedicatedVideoMemory / (1024 * 1024));
                            if (vram > bestVram)
                                bestVram = vram;
                        }
                    }
                    finally
                    {
                        NativeMethods.Release(adapter);
                    }
                }

                return bestVram;
            }
            finally
            {
                NativeMethods.Release(factory);
            }
        }
        catch
        {
            return 0;
        }
    }

    /// <summary>
    /// Queries VRAM via WMI Win32_VideoController. Used as fallback when DXGI returns 0.
    /// Returns 0 on failure or timeout.
    /// </summary>
    internal static long QueryWmiVramMb()
    {
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));
            using var searcher = new ManagementObjectSearcher("SELECT AdapterRAM FROM Win32_VideoController");
            long best = 0;
            foreach (ManagementObject obj in searcher.Get())
            {
                if (cts.Token.IsCancellationRequested)
                    break;
                var raw = obj["AdapterRAM"];
                if (raw is uint u && u > 0)
                {
                    long mb = u / (1024 * 1024);
                    if (mb > best) best = mb;
                }
            }
            return best;
        }
        catch
        {
            return 0;
        }
    }

    /// <summary>Queries available system RAM via GlobalMemoryStatusEx. Falls back to GC info.</summary>
    internal static long QueryAvailableRamMb()
    {
        try
        {
            var status = new NativeMethods.MEMORYSTATUSEX { dwLength = (uint)Marshal.SizeOf<NativeMethods.MEMORYSTATUSEX>() };
            if (NativeMethods.GlobalMemoryStatusEx(ref status))
                return (long)(status.ullAvailPhys / (1024 * 1024));
        }
        catch { /* fall through */ }

        // GC fallback
        var gcInfo = GC.GetGCMemoryInfo();
        return gcInfo.TotalAvailableMemoryBytes / (1024 * 1024);
    }
}

file static class NativeMethods
{
    internal static readonly Guid IID_IDXGIFactory1 = new("770aae78-f26f-4dba-a829-253c83d1b387");

    [DllImport("dxgi.dll", PreserveSig = true)]
    internal static extern int CreateDXGIFactory1(in Guid riid, out nint ppFactory);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);

    // IDXGIFactory1::EnumAdapters1 at vtable slot 12 (index 7 of IDXGIFactory1, 0-based)
    internal static int EnumAdapters1(nint factory, uint index, out nint ppAdapter)
    {
        // vtable: IUnknown(3) + IDXGIObject(4) + IDXGIFactory(3) + IDXGIFactory1::EnumAdapters1 = slot 10
        unsafe
        {
            var vtbl = *(nint**)factory;
            var fn = (delegate* unmanaged<nint, uint, nint*, int>)vtbl[12];
            nint adapter = nint.Zero;
            int hr = fn(factory, index, &adapter);
            ppAdapter = adapter;
            return hr;
        }
    }

    internal static int GetDesc1(nint adapter, out DXGI_ADAPTER_DESC1 desc)
    {
        // IDXGIAdapter1::GetDesc1 is at vtable slot 8
        // IUnknown(3) + IDXGIObject(4) + IDXGIAdapter(0 extra before GetDesc1 at slot 7) + IDXGIAdapter1 slot 8
        unsafe
        {
            var d = new DXGI_ADAPTER_DESC1();
            var vtbl = *(nint**)adapter;
            var fn = (delegate* unmanaged<nint, DXGI_ADAPTER_DESC1*, int>)vtbl[8];
            int hr = fn(adapter, &d);
            desc = d;
            return hr;
        }
    }

    internal static void Release(nint punk)
    {
        // IUnknown::Release at vtable slot 2
        unsafe
        {
            var vtbl = *(nint**)punk;
            var fn = (delegate* unmanaged<nint, uint>)vtbl[2];
            fn(punk);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct MEMORYSTATUSEX
    {
        internal uint dwLength;
        internal uint dwMemoryLoad;
        internal ulong ullTotalPhys;
        internal ulong ullAvailPhys;
        internal ulong ullTotalPageFile;
        internal ulong ullAvailPageFile;
        internal ulong ullTotalVirtual;
        internal ulong ullAvailVirtual;
        internal ulong ullAvailExtendedVirtual;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    internal unsafe struct DXGI_ADAPTER_DESC1
    {
        internal fixed char Description[128];
        internal uint VendorId;
        internal uint DeviceId;
        internal uint SubSysId;
        internal uint Revision;
        internal nuint DedicatedVideoMemory;
        internal nuint DedicatedSystemMemory;
        internal nuint SharedSystemMemory;
        // LUID: LowPart + HighPart
        internal uint LuidLowPart;
        internal int LuidHighPart;
        internal uint Flags;
    }
}
