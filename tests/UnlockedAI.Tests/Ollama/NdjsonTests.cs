using System.Text;
using UnlockedAI.Core.Ollama;

namespace UnlockedAI.Tests.Ollama;

public class NdjsonTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Values_split_across_reads_are_reassembled()
    {
        // The second line is cut in the middle, as happens on a real socket.
        using var stream = new PiecewiseStream(
            "{\"error\":\"one\"}\n{\"err",
            "or\":\"two\"}\n");

        var errors = await ReadErrorsAsync(stream);

        Assert.Equal(["one", "two"], errors);
    }

    [Fact]
    public async Task Last_value_without_a_trailing_newline_is_still_read()
    {
        using var stream = new PiecewiseStream("{\"error\":\"one\"}\n{\"error\":\"last\"}");

        Assert.Equal(["one", "last"], await ReadErrorsAsync(stream));
    }

    [Fact]
    public async Task Blank_lines_are_skipped()
    {
        using var stream = new PiecewiseStream("\n{\"error\":\"one\"}\r\n\r\n", "{\"error\":\"two\"}\n\n");

        Assert.Equal(["one", "two"], await ReadErrorsAsync(stream));
    }

    private static async Task<List<string?>> ReadErrorsAsync(Stream stream)
    {
        var errors = new List<string?>();
        await foreach (var value in Ndjson.ReadAsync(stream, OllamaJsonContext.Default.ErrorDto, Ct))
        {
            errors.Add(value.Error);
        }

        return errors;
    }

    /// <summary>Hands out one prepared piece per read, like a network stream delivering packets.</summary>
    private sealed class PiecewiseStream(params string[] pieces) : Stream
    {
        private readonly Queue<byte[]> _pieces = new(pieces.Select(Encoding.UTF8.GetBytes));

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (!_pieces.TryDequeue(out var piece))
            {
                return 0;
            }

            piece.CopyTo(buffer, offset);
            return piece.Length;
        }

        public override void Flush()
        {
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
