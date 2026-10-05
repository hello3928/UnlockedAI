using System.IO.Compression;
using System.Text;
using System.Xml;

namespace UnlockedAI.Core.Files;

/// <summary>
/// Reads the body text of a Word document. A .docx is a zip holding XML, so this needs no library:
/// it streams word/document.xml and collects the text runs, one line per paragraph.
/// </summary>
public sealed class DocxExtractor : IFileExtractor
{
    private const string BodyPart = "word/document.xml";
    private const string WordNamespace = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";

    public string Kind => "docx";

    public bool CanRead(string extension) => extension == ".docx";

    public ExtractedText Extract(Stream stream, int maxChars)
    {
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true);
        var body = archive.GetEntry(BodyPart) ?? throw new NotThisKindOfFileException();

        using var bodyStream = body.Open();
        using var xml = XmlReader.Create(
            bodyStream,
            new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null });

        var text = new StringBuilder();
        var insideTextRun = false;

        // Reading stops at the limit, so a document that unpacks to something huge is never fully expanded.
        while (xml.Read())
        {
            switch (xml.NodeType)
            {
                case XmlNodeType.Element when xml.NamespaceURI == WordNamespace:
                    switch (xml.LocalName)
                    {
                        case "t":
                            insideTextRun = !xml.IsEmptyElement;
                            break;
                        case "tab":
                            text.Append('\t');
                            break;
                        case "br" or "cr":
                            text.Append('\n');
                            break;
                        case "p" when xml.IsEmptyElement:
                            text.Append('\n');
                            break;
                    }

                    break;

                case XmlNodeType.EndElement when xml.NamespaceURI == WordNamespace:
                    if (xml.LocalName == "t")
                    {
                        insideTextRun = false;
                    }
                    else if (xml.LocalName == "p")
                    {
                        text.Append('\n');
                    }

                    break;

                case XmlNodeType.Text or XmlNodeType.Whitespace or XmlNodeType.SignificantWhitespace when insideTextRun:
                    text.Append(xml.Value);
                    break;
            }

            if (text.Length >= maxChars)
            {
                return new ExtractedText(text.ToString(0, maxChars), Truncated: true);
            }
        }

        return new ExtractedText(text.ToString().TrimEnd(), Truncated: false);
    }
}
