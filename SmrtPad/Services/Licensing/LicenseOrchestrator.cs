namespace SmrtPad.Services.Licensing;

/// <summary>Abstracts Windows Store licence queries for testability.</summary>
public interface IStoreContextAdapter
{
    /// <summary>Checks whether the user owns the Pro add-on SKU.</summary>
    Task<bool> HasProLicenseAsync(CancellationToken ct);

    /// <summary>Raised when offline licence state changes (e.g., purchase completes while offline).</summary>
    event EventHandler OfflineLicensesChanged;
}

/// <summary>
/// Orchestrates licence validation via Store and offline Ed25519 key probes.
/// Either probe returning <see langword="true"/> enables Pro features.
/// </summary>
public sealed class LicenseOrchestrator
{
    private readonly IStoreContextAdapter _storeAdapter;
    private readonly LocalKeyValidator _keyValidator;
    private readonly SemaphoreSlim _initLock = new(1, 1);
    private bool _initialized;

    /// <summary>Returns <see langword="true"/> when Pro features are active.</summary>
    public bool IsPro { get; private set; }

    /// <summary>Raised when the Pro licence state changes.</summary>
    public event EventHandler<bool>? ProLicenseChanged;

    public LicenseOrchestrator(IStoreContextAdapter storeAdapter, LocalKeyValidator keyValidator)
    {
        _storeAdapter = storeAdapter ?? throw new ArgumentNullException(nameof(storeAdapter));
        _keyValidator = keyValidator ?? throw new ArgumentNullException(nameof(keyValidator));

        _storeAdapter.OfflineLicensesChanged += OnOfflineLicensesChanged;
    }

    /// <summary>Initialises the orchestrator. Idempotent — second call is a no-op.</summary>
    public async Task InitializeAsync(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        await _initLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (_initialized)
            {
                return;
            }

            var isPro = await ProbeAsync(ct).ConfigureAwait(false);
            ApplyProState(isPro);
            _initialized = true;
        }
        finally
        {
            _initLock.Release();
        }
    }

    private async Task<bool> ProbeAsync(CancellationToken ct)
    {
        // Probe A — Store
        try
        {
            if (await _storeAdapter.HasProLicenseAsync(ct).ConfigureAwait(false))
            {
                return true;
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            // Store probe failed — fall through to local key
        }

        // Probe B — Local Ed25519 key
        try
        {
            return await _keyValidator.ValidateAsync(ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return false;
        }
    }

    private void ApplyProState(bool isPro)
    {
        var previousState = IsPro;
        IsPro = isPro;

        if (isPro)
        {
            FeatureFlags.SetProFlags();
        }
        else
        {
            FeatureFlags.ClearProFlags();
        }

        if (previousState != isPro)
        {
            ProLicenseChanged?.Invoke(this, isPro);
        }
    }

    private async void OnOfflineLicensesChanged(object? sender, EventArgs e)
    {
        try
        {
            var isPro = await ProbeAsync(CancellationToken.None).ConfigureAwait(false);
            ApplyProState(isPro);
        }
        catch
        {
            // Best-effort — do not crash on background re-evaluation
        }
    }
}
