using System.Text;

namespace UnlockedAI.Core.Files;

/// <summary>
/// Reads text and source code. It accepts any extension and decides by content instead: a file with
/// no NUL bytes near its start is treated as text. That covers Makefile, Dockerfile and every language
/// without keeping a list of extensions.
/// </summary>
public sealed class TextFileExtractor : IFileExtractor
{
    private const int SniffBytes = 8192;

    public string Kind => "text";

    public bool CanRead(string extension) => true;

    public ExtractedText Extract(Stream stream, int maxChars)
    {
        if (LooksBinary(stream))
        {
            throw new NotThisKindOfFileException();
        }

        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);

        var buffer = new char[Math.Min(maxChars, 16 * 1024)];
        var text = new StringBuilder();
        while (text.Length < maxChars)
        {
            var read = reader.Read(buffer, 0, Math.Min(buffer.Length, maxChars - text.Length));
            if (read == 0)
            {
                return new ExtractedText(text.ToString(), Truncated: false);
            }

            text.Append(buffer, 0, read);
        }

        return new ExtractedText(text.ToString(), Truncated: reader.Peek() >= 0);
    }

    private static bool LooksBinary(Stream stream)
    {
        Span<byte> head = stackalloc byte[SniffBytes];
        var read = stream.ReadAtLeast(head, head.Length, throwOnEndOfStream: false);
        stream.Position = 0;

        head = head[..read];

        // UTF-16 text is full of NUL bytes but announces itself with a byte order mark.
        var isUtf16 = head.Length >= 2 && ((head[0] == 0xFF && head[1] == 0xFE) || (head[0] == 0xFE && head[1] == 0xFF));
        return !isUtf16 && head.Contains((byte)0);
    }
}
