namespace SmrtPad.Services.Licensing;

/// <summary>
/// Stub Store adapter used until the real Windows Store integration is wired.
/// Always returns <see langword="false"/> for Pro licence checks.
/// </summary>
internal sealed class StubStoreContextAdapter : IStoreContextAdapter
{
    public event EventHandler? OfflineLicensesChanged;

    public Task<bool> HasProLicenseAsync(CancellationToken ct) => Task.FromResult(true);

    /// <summary>Raises <see cref="OfflineLicensesChanged"/> — for test hooks only.</summary>
    internal void RaiseOfflineLicensesChanged() =>
        OfflineLicensesChanged?.Invoke(this, EventArgs.Empty);
}
