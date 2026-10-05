using System.Text;
using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;

namespace UnlockedAI.Core.Files;

/// <summary>Reads the text layer of a PDF, a page at a time. Scanned pages without a text layer yield nothing.</summary>
public sealed class PdfExtractor : IFileExtractor
{
    public string Kind => "pdf";

    public bool CanRead(string extension) => extension == ".pdf";

    public ExtractedText Extract(Stream stream, int maxChars)
    {
        using var document = PdfDocument.Open(stream);

        var text = new StringBuilder();
        foreach (var page in document.GetPages())
        {
            var pageText = ContentOrderTextExtractor.GetText(page);
            if (string.IsNullOrWhiteSpace(pageText))
            {
                continue;
            }

            if (text.Length > 0)
            {
                text.Append("\n\n");
            }

            if (text.Length + pageText.Length > maxChars)
            {
                text.Append(pageText, 0, Math.Max(0, maxChars - text.Length));
                return new ExtractedText(text.ToString(), Truncated: true);
            }

            text.Append(pageText);
        }

        return new ExtractedText(text.ToString(), Truncated: false);
    }
}
