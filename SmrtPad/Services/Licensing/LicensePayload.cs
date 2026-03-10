using System.Text.Json;

namespace SmrtPad.Services.Licensing;

/// <summary>
/// Represents a serialisable licence payload containing SKU, expiry, and an Ed25519 signature.
/// </summary>
public sealed class LicensePayload
{
    public required string Sku { get; init; }
    public required DateTimeOffset Expiry { get; init; }
    public required byte[] Signature { get; init; }
    public required byte[] SignedBytes { get; init; }

    /// <summary>Serialises this payload to a byte array.</summary>
    public byte[] Serialize()
    {
        var wrapper = new Dictionary<string, object>
        {
            ["sku"] = Sku,
            ["expiry"] = Expiry,
            ["signature"] = Convert.ToBase64String(Signature),
            ["signedBytes"] = Convert.ToBase64String(SignedBytes),
        };
        return JsonSerializer.SerializeToUtf8Bytes(wrapper);
    }

    /// <summary>Deserialises a byte array into a <see cref="LicensePayload"/>.</summary>
    /// <exception cref="ArgumentNullException"><paramref name="rawBytes"/> is <see langword="null"/>.</exception>
    /// <exception cref="FormatException">The bytes are empty, malformed, or missing required fields.</exception>
    public static LicensePayload Deserialize(byte[] rawBytes)
    {
        ArgumentNullException.ThrowIfNull(rawBytes);

        if (rawBytes.Length == 0)
        {
            throw new FormatException("License payload bytes are empty.");
        }

        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(rawBytes);
        }
        catch (JsonException ex)
        {
            throw new FormatException("License payload contains malformed JSON.", ex);
        }

        using (doc)
        {
            var root = doc.RootElement;

            if (!root.TryGetProperty("sku", out var skuElement) || skuElement.ValueKind == JsonValueKind.Null)
            {
                throw new FormatException("License payload is missing the 'sku' field.");
            }

            if (!root.TryGetProperty("expiry", out var expiryElement))
            {
                throw new FormatException("License payload is missing the 'expiry' field.");
            }

            if (!root.TryGetProperty("signature", out var signatureElement) || signatureElement.ValueKind == JsonValueKind.Null)
            {
                throw new FormatException("License payload is missing the 'signature' field.");
            }

            var sku = skuElement.GetString()!;

            DateTimeOffset expiry;
            try
            {
                expiry = expiryElement.GetDateTimeOffset();
            }
            catch (FormatException)
            {
                throw new FormatException("License payload 'expiry' field is not a valid DateTimeOffset.");
            }

            byte[] signature;
            try
            {
                signature = signatureElement.ValueKind == JsonValueKind.String
                    ? Convert.FromBase64String(signatureElement.GetString()!)
                    : signatureElement.EnumerateArray().Select(e => (byte)e.GetInt32()).ToArray();
            }
            catch
            {
                throw new FormatException("License payload 'signature' field is not valid Base64.");
            }

            byte[] signedBytes = [];
            if (root.TryGetProperty("signedBytes", out var signedBytesElement) &&
                signedBytesElement.ValueKind != JsonValueKind.Null)
            {
                try
                {
                    signedBytes = signedBytesElement.ValueKind == JsonValueKind.String
                        ? Convert.FromBase64String(signedBytesElement.GetString()!)
                        : signedBytesElement.EnumerateArray().Select(e => (byte)e.GetInt32()).ToArray();
                }
                catch
                {
                    signedBytes = [];
                }
            }

            return new LicensePayload
            {
                Sku = sku,
                Expiry = expiry,
                Signature = signature,
                SignedBytes = signedBytes,
            };
        }
    }
}
