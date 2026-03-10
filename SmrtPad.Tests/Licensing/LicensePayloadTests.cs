using System.Text;
using SmrtPad.Services.Licensing;

namespace SmrtPad.Tests.Licensing;

public class LicensePayloadTests
{
    private static LicensePayload CreateSample(
        string sku = "SmrtPadPro",
        DateTimeOffset? expiry = null,
        byte[]? signature = null,
        byte[]? signedBytes = null) => new()
    {
        Sku = sku,
        Expiry = expiry ?? new DateTimeOffset(2030, 1, 1, 0, 0, 0, TimeSpan.Zero),
        Signature = signature ?? [1, 2, 3, 4],
        SignedBytes = signedBytes ?? Encoding.UTF8.GetBytes("""{"sku":"SmrtPadPro","expiry":"2030-01-01T00:00:00+00:00"}"""),
    };

    [Fact]
    public void Serialize_ThenDeserialize_RoundTrips_Sku()
    {
        var original = CreateSample();
        var roundTripped = LicensePayload.Deserialize(original.Serialize());
        Assert.Equal(original.Sku, roundTripped.Sku);
    }

    [Fact]
    public void Serialize_ThenDeserialize_RoundTrips_Expiry()
    {
        var original = CreateSample();
        var roundTripped = LicensePayload.Deserialize(original.Serialize());
        Assert.Equal(original.Expiry, roundTripped.Expiry);
    }

    [Fact]
    public void Serialize_ThenDeserialize_RoundTrips_Signature()
    {
        var original = CreateSample();
        var roundTripped = LicensePayload.Deserialize(original.Serialize());
        Assert.Equal(original.Signature, roundTripped.Signature);
    }

    [Fact]
    public void Serialize_ThenDeserialize_RoundTrips_SignedBytes()
    {
        var original = CreateSample();
        var roundTripped = LicensePayload.Deserialize(original.Serialize());
        Assert.Equal(original.SignedBytes, roundTripped.SignedBytes);
    }

    [Fact]
    public void Serialize_ProducesNonEmptyByteArray()
    {
        var payload = CreateSample();
        var bytes = payload.Serialize();
        Assert.NotEmpty(bytes);
    }

    [Fact]
    public void Deserialize_NullBytes_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => LicensePayload.Deserialize(null!));
    }

    [Fact]
    public void Deserialize_EmptyBytes_ThrowsFormatException()
    {
        Assert.Throws<FormatException>(() => LicensePayload.Deserialize([]));
    }

    [Fact]
    public void Deserialize_MalformedJson_ThrowsFormatException()
    {
        var bytes = Encoding.UTF8.GetBytes("not json at all {{{{");
        Assert.Throws<FormatException>(() => LicensePayload.Deserialize(bytes));
    }

    [Fact]
    public void Deserialize_MissingSkuField_ThrowsFormatException()
    {
        var json = """{"expiry":"2030-01-01T00:00:00+00:00","signature":"AQID","signedBytes":""}""";
        var bytes = Encoding.UTF8.GetBytes(json);
        Assert.Throws<FormatException>(() => LicensePayload.Deserialize(bytes));
    }

    [Fact]
    public void Deserialize_MissingExpiryField_ThrowsFormatException()
    {
        var json = """{"sku":"SmrtPadPro","signature":"AQID","signedBytes":""}""";
        var bytes = Encoding.UTF8.GetBytes(json);
        Assert.Throws<FormatException>(() => LicensePayload.Deserialize(bytes));
    }

    [Fact]
    public void Deserialize_MissingSignatureField_ThrowsFormatException()
    {
        var json = """{"sku":"SmrtPadPro","expiry":"2030-01-01T00:00:00+00:00","signedBytes":""}""";
        var bytes = Encoding.UTF8.GetBytes(json);
        Assert.Throws<FormatException>(() => LicensePayload.Deserialize(bytes));
    }

    [Fact]
    public void Deserialize_ZeroLengthSignatureBytes_Deserializes()
    {
        var payload = CreateSample(signature: []);
        var bytes = payload.Serialize();
        var result = LicensePayload.Deserialize(bytes);
        Assert.Empty(result.Signature);
    }

    [Fact]
    public void Deserialize_MaxDateTimeOffset_RoundTrips()
    {
        var payload = CreateSample(expiry: DateTimeOffset.MaxValue);
        var bytes = payload.Serialize();
        var result = LicensePayload.Deserialize(bytes);
        Assert.Equal(DateTimeOffset.MaxValue, result.Expiry);
    }

    [Fact]
    public void Deserialize_MinDateTimeOffset_RoundTrips()
    {
        var payload = CreateSample(expiry: DateTimeOffset.MinValue);
        var bytes = payload.Serialize();
        var result = LicensePayload.Deserialize(bytes);
        Assert.Equal(DateTimeOffset.MinValue, result.Expiry);
    }

    [Fact]
    public void Serialize_EmptySignature_RoundTrips()
    {
        var payload = CreateSample(signature: []);
        var result = LicensePayload.Deserialize(payload.Serialize());
        Assert.Empty(result.Signature);
    }

    [Fact]
    public void Serialize_LargeSignatureBytes_RoundTrips()
    {
        var largeSignature = new byte[1024];
        Random.Shared.NextBytes(largeSignature);
        var payload = CreateSample(signature: largeSignature);
        var result = LicensePayload.Deserialize(payload.Serialize());
        Assert.Equal(largeSignature, result.Signature);
    }
}
