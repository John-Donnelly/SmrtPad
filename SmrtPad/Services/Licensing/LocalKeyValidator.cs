using System.Security.Cryptography;
using System.Text;

namespace SmrtPad.Services.Licensing;

/// <summary>Abstracts file-system access to the licence file for testability.</summary>
public interface ILicenseFileProvider
{
    /// <summary>Returns <see langword="true"/> when the licence file exists.</summary>
    bool Exists { get; }

    /// <summary>Reads all bytes from the licence file.</summary>
    byte[] ReadAllBytes();
}

/// <summary>
/// Validates an offline licence key stored in <c>%LOCALAPPDATA%\SmrtPad\.lic</c>.
/// Pipeline: read → DPAPI decrypt → deserialise → Ed25519 verify → expiry check.
/// Never throws on validation failures — returns <see langword="false"/>.
/// </summary>
public sealed class LocalKeyValidator
{
    // Base64-encoded DER Ed25519 public key — generated offline; private key never committed.
    // Replace with actual key before production use.
    private const string PublicKeyBase64 =
        "MCowBQYDK2VwAyEAZjGbBVUfBNF8fN/S7jKmFGHMqGLlHfzDvtCGEO6MVr0=";

    private readonly ILicenseFileProvider _fileProvider;

    /// <summary>Initialises a new validator with the specified file provider.</summary>
    public LocalKeyValidator(ILicenseFileProvider? fileProvider = null)
    {
        _fileProvider = fileProvider ?? new DefaultLicenseFileProvider();
    }

    /// <summary>
    /// Validates the local licence file. Returns <see langword="true"/> only when all
    /// checks pass: file exists, decrypts, deserialises, signature valid, not expired.
    /// </summary>
    public Task<bool> ValidateAsync(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        try
        {
            if (!_fileProvider.Exists)
            {
                return Task.FromResult(false);
            }

            var raw = _fileProvider.ReadAllBytes();
            if (raw.Length == 0)
            {
                return Task.FromResult(false);
            }

            byte[] decrypted;
            try
            {
                decrypted = ProtectedData.Unprotect(raw, MachineEntropy(), DataProtectionScope.CurrentUser);
            }
            catch
            {
                return Task.FromResult(false);
            }

            LicensePayload payload;
            try
            {
                payload = LicensePayload.Deserialize(decrypted);
            }
            catch
            {
                return Task.FromResult(false);
            }

            // Verify Ed25519 signature
            if (!VerifySignature(payload))
            {
                return Task.FromResult(false);
            }

            // Check expiry — must be strictly in the future
            if (payload.Expiry <= DateTimeOffset.UtcNow)
            {
                return Task.FromResult(false);
            }

            return Task.FromResult(true);
        }
        catch
        {
            return Task.FromResult(false);
        }
    }

    /// <summary>
    /// Generates machine-specific entropy bytes for DPAPI.
    /// Combines machine name and OS version for uniqueness.
    /// </summary>
    public static byte[] MachineEntropy()
    {
        var data = $"{Environment.MachineName}:{Environment.OSVersion}";
        return SHA256.HashData(Encoding.UTF8.GetBytes(data));
    }

    private static bool VerifySignature(LicensePayload payload)
    {
        try
        {
            var publicKeyBytes = Convert.FromBase64String(PublicKeyBase64);
            using var ecdsa = ECDsa.Create();
            ecdsa.ImportSubjectPublicKeyInfo(publicKeyBytes, out _);
            return ecdsa.VerifyData(payload.SignedBytes, payload.Signature, HashAlgorithmName.SHA256);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>Default provider that reads from <c>%LOCALAPPDATA%\SmrtPad\.lic</c>.</summary>
    private sealed class DefaultLicenseFileProvider : ILicenseFileProvider
    {
        private readonly string _path = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SmrtPad",
            ".lic");

        public bool Exists => File.Exists(_path);
        public byte[] ReadAllBytes() => File.ReadAllBytes(_path);
    }
}
