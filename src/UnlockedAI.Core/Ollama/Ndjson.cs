using System.Buffers;
using System.IO.Pipelines;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace UnlockedAI.Core.Ollama;

/// <summary>
/// Reads newline-delimited JSON from a stream, yielding each value as soon as its line arrives.
/// Parses straight from the receive buffer, so no string is allocated per line.
/// </summary>
internal static class Ndjson
{
    private const byte NewLine = (byte)'\n';

    public static async IAsyncEnumerable<T> ReadAsync<T>(
        Stream stream,
        JsonTypeInfo<T> typeInfo,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var reader = PipeReader.Create(stream);
        try
        {
            while (true)
            {
                var result = await reader.ReadAsync(cancellationToken).ConfigureAwait(false);
                var buffer = result.Buffer;

                while (TryTakeLine(ref buffer, out var line))
                {
                    if (TryParse(line, typeInfo, out var value))
                    {
                        yield return value;
                    }
                }

                if (result.IsCompleted)
                {
                    // The last value may not end with a newline.
                    if (TryParse(buffer, typeInfo, out var last))
                    {
                        yield return last;
                    }

                    break;
                }

                // Everything before buffer.Start is consumed; the partial line after it is kept for the next read.
                reader.AdvanceTo(buffer.Start, buffer.End);
            }
        }
        finally
        {
            await reader.CompleteAsync().ConfigureAwait(false);
        }
    }

    private static bool TryTakeLine(ref ReadOnlySequence<byte> buffer, out ReadOnlySequence<byte> line)
    {
        var position = buffer.PositionOf(NewLine);
        if (position is null)
        {
            line = default;
            return false;
        }

        line = buffer.Slice(0, position.Value);
        buffer = buffer.Slice(buffer.GetPosition(1, position.Value));
        return true;
    }

    private static bool TryParse<T>(ReadOnlySequence<byte> line, JsonTypeInfo<T> typeInfo, out T value)
    {
        value = default!;
        if (IsBlank(line))
        {
            return false;
        }

        var json = new Utf8JsonReader(line);
        var parsed = JsonSerializer.Deserialize(ref json, typeInfo);
        if (parsed is null)
        {
            return false;
        }

        value = parsed;
        return true;
    }

    private static bool IsBlank(ReadOnlySequence<byte> line)
    {
        foreach (var segment in line)
        {
            foreach (var b in segment.Span)
            {
                if (b is not ((byte)' ' or (byte)'\r' or (byte)'\t' or NewLine))
                {
                    return false;
                }
            }
        }

        return true;
    }
}
