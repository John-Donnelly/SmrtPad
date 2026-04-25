using System;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace SmrtAI.Core.Ipc;

/// <summary>
/// Reads and writes <see cref="SmrtDoodleImageMessage"/> frames over a <see cref="Stream"/>.
/// Frame format: little-endian <c>int32</c> byte count, then UTF-8 JSON.
/// </summary>
public static class SmrtDoodleFrame
{
    private static readonly JsonSerializerOptions s_options = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>Writes one frame to <paramref name="stream"/>.</summary>
    public static async Task WriteAsync(Stream stream, SmrtDoodleImageMessage message, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(message);

        var json = JsonSerializer.SerializeToUtf8Bytes(message, s_options);
        var lengthHeader = BitConverter.GetBytes(json.Length);
        await stream.WriteAsync(lengthHeader, ct).ConfigureAwait(false);
        await stream.WriteAsync(json, ct).ConfigureAwait(false);
        await stream.FlushAsync(ct).ConfigureAwait(false);
    }

    /// <summary>Reads one frame from <paramref name="stream"/>, or returns <c>null</c> on EOF.</summary>
    public static async Task<SmrtDoodleImageMessage?> ReadAsync(Stream stream, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(stream);

        var header = new byte[4];
        if (!await ReadExactlyAsync(stream, header, ct).ConfigureAwait(false))
            return null;

        var length = BitConverter.ToInt32(header, 0);
        if (length <= 0 || length > 64 * 1024 * 1024)
            throw new InvalidDataException($"Invalid SmrtDoodle frame length: {length}");

        var body = new byte[length];
        if (!await ReadExactlyAsync(stream, body, ct).ConfigureAwait(false))
            return null;

        return JsonSerializer.Deserialize<SmrtDoodleImageMessage>(body, s_options);
    }

    private static async Task<bool> ReadExactlyAsync(Stream stream, byte[] buffer, CancellationToken ct)
    {
        var offset = 0;
        while (offset < buffer.Length)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(offset), ct).ConfigureAwait(false);
            if (read == 0) return offset == buffer.Length;
            offset += read;
        }
        return true;
    }

    /// <summary>Encodes PNG bytes for transport.</summary>
    public static string Encode(byte[] png) => Convert.ToBase64String(png);

    /// <summary>Decodes PNG bytes from transport.</summary>
    public static byte[]? Decode(string? base64)
        => string.IsNullOrEmpty(base64) ? null : Convert.FromBase64String(base64);
}
