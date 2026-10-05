namespace UnlockedAI.Core.Models;

/// <summary>Attachment metadata as shown in the UI.</summary>
/// <param name="Kind">Short type label such as "text", "pdf" or "docx".</param>
/// <param name="Truncated">True when the extracted text was cut at the size limit.</param>
public sealed record Attachment(long Id, string FileName, string Kind, long SizeBytes, bool Truncated);

/// <summary>An attachment that has been read from disk but not yet saved with a message.</summary>
public sealed record NewAttachment(string FileName, string Kind, long SizeBytes, bool Truncated, string Text);

/// <summary>Extracted text for one attachment, loaded only when a request to the model is built.</summary>
public sealed record AttachmentText(long MessageId, string FileName, bool Truncated, string Text);
