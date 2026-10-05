namespace UnlockedAI.Core.Files;

/// <summary>The text pulled out of a file, and whether it was cut at the size limit.</summary>
public readonly record struct ExtractedText(string Text, bool Truncated);

/// <summary>Reads one kind of file as plain text. Supporting a new file type means adding one of these.</summary>
public interface IFileExtractor
{
    /// <summary>Short label stored with the attachment, such as "pdf".</summary>
    string Kind { get; }

    /// <param name="extension">Lower-case, with the dot, for example ".pdf". Empty when the file has none.</param>
    bool CanRead(string extension);

    /// <summary>Reads at most <paramref name="maxChars"/> characters. Stops reading the file once it has them.</summary>
    ExtractedText Extract(Stream stream, int maxChars);
}

/// <summary>Thrown by an extractor when the file turns out not to be the kind it reads.</summary>
public sealed class NotThisKindOfFileException() : Exception("The file is not in the expected format.");
