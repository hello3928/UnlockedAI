using UnlockedAI.Core.Errors;
using UnlockedAI.Core.Models;

namespace UnlockedAI.Core.Files;

/// <summary>
/// Turns a file on disk into an attachment by extracting its text. Only the text is kept;
/// the original file is never copied or stored.
/// </summary>
public sealed class AttachmentService
{
    public const long MaxFileBytes = 10 * 1024 * 1024;
    public const int MaxTextChars = 100_000;

    // Tried in order. The text extractor accepts anything, so it goes last.
    private readonly IFileExtractor[] _extractors = [new PdfExtractor(), new DocxExtractor(), new TextFileExtractor()];

    /// <exception cref="AppException">The file is missing, too large, not a supported type, or unreadable.</exception>
    public Task<NewAttachment> ReadAsync(string path, CancellationToken cancellationToken = default) =>
        Task.Run(() => Read(path), cancellationToken);

    private NewAttachment Read(string path)
    {
        var fileName = Path.GetFileName(path);
        var extension = Path.GetExtension(path).ToLowerInvariant();

        try
        {
            var info = new FileInfo(path);
            if (!info.Exists)
            {
                throw new AppException(AppError.FileNotFound(path));
            }

            if (info.Length > MaxFileBytes)
            {
                throw new AppException(AppError.FileTooLarge(fileName, MaxFileBytes));
            }

            var extractor = _extractors.First(candidate => candidate.CanRead(extension));

            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            var extracted = extractor.Extract(stream, MaxTextChars);

            if (string.IsNullOrWhiteSpace(extracted.Text))
            {
                throw new AppException(new AppError(
                    AppErrorKind.FileUnreadable,
                    "No text found",
                    $"\"{fileName}\" has no text that can be read. If it is a scan, the pages are images."));
            }

            return new NewAttachment(fileName, extractor.Kind, info.Length, extracted.Truncated, extracted.Text);
        }
        catch (AppException)
        {
            throw;
        }
        catch (NotThisKindOfFileException exception)
        {
            throw new AppException(AppError.FileUnsupported(fileName), exception);
        }
        catch (UnauthorizedAccessException exception)
        {
            throw new AppException(AppError.AccessDenied(exception.Message), exception);
        }
        catch (Exception exception)
        {
            // Parsers for PDF and zip throw many different types for damaged or protected files.
            throw new AppException(AppError.FileUnreadable(fileName, exception.Message), exception);
        }
    }
}
