using System.IO.Compression;
using System.Text;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;
using UnlockedAI.Core.Errors;
using UnlockedAI.Core.Files;

namespace UnlockedAI.Tests.Files;

public sealed class AttachmentServiceTests : IDisposable
{
    private readonly string _folder = Directory.CreateTempSubdirectory("unlockedai-files-").FullName;
    private readonly AttachmentService _service = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    [Fact]
    public async Task Text_file_is_read_whole()
    {
        var path = Write("notes.md", "# Title\n\nSome notes with accents: café.");

        var attachment = await _service.ReadAsync(path, Ct);

        Assert.Equal("notes.md", attachment.FileName);
        Assert.Equal("text", attachment.Kind);
        Assert.Equal("# Title\n\nSome notes with accents: café.", attachment.Text);
        Assert.False(attachment.Truncated);
        Assert.Equal(new FileInfo(path).Length, attachment.SizeBytes);
    }

    [Fact]
    public async Task Utf16_text_is_recognised_by_its_byte_order_mark()
    {
        var path = Path.Combine(_folder, "wide.txt");
        await File.WriteAllTextAsync(path, "wide text", Encoding.Unicode, Ct);

        Assert.Equal("wide text", (await _service.ReadAsync(path, Ct)).Text);
    }

    [Fact]
    public async Task File_without_an_extension_is_accepted_when_it_holds_text()
    {
        var path = Write("Makefile", "build:\n\tdotnet build\n");

        Assert.Equal("text", (await _service.ReadAsync(path, Ct)).Kind);
    }

    [Fact]
    public async Task Binary_file_is_unsupported()
    {
        var path = Path.Combine(_folder, "photo.png");
        await File.WriteAllBytesAsync(path, [0x89, 0x50, 0x4E, 0x47, 0x00, 0x00, 0x00, 0x0D], Ct);

        Assert.Equal(AppErrorKind.FileUnsupported, (await FailsAsync(path)).Kind);
    }

    [Fact]
    public async Task File_over_the_size_limit_is_rejected_without_being_read()
    {
        var path = Path.Combine(_folder, "huge.txt");
        await using (var stream = File.Create(path))
        {
            stream.SetLength(AttachmentService.MaxFileBytes + 1);
        }

        var error = await FailsAsync(path);

        Assert.Equal(AppErrorKind.FileTooLarge, error.Kind);
        Assert.Contains("10 MB", error.Message);
    }

    [Fact]
    public async Task Long_text_is_cut_at_the_limit_and_marked_truncated()
    {
        var path = Write("long.txt", new string('x', AttachmentService.MaxTextChars + 500));

        var attachment = await _service.ReadAsync(path, Ct);

        Assert.True(attachment.Truncated);
        Assert.Equal(AttachmentService.MaxTextChars, attachment.Text.Length);
    }

    [Fact]
    public async Task Text_exactly_at_the_limit_is_not_marked_truncated()
    {
        var path = Write("exact.txt", new string('x', AttachmentService.MaxTextChars));

        Assert.False((await _service.ReadAsync(path, Ct)).Truncated);
    }

    [Fact]
    public async Task Missing_file_is_reported_as_not_found()
    {
        Assert.Equal(AppErrorKind.FileNotFound, (await FailsAsync(Path.Combine(_folder, "gone.txt"))).Kind);
    }

    [Fact]
    public async Task File_with_no_text_says_so()
    {
        var error = await FailsAsync(Write("blank.txt", "   \n  "));

        Assert.Equal(AppErrorKind.FileUnreadable, error.Kind);
        Assert.Equal("No text found", error.Title);
    }

    [Fact]
    public async Task Word_document_gives_one_line_per_paragraph()
    {
        var path = WriteDocx(
            "letter.docx",
            """
            <w:p><w:r><w:t>Dear reader,</w:t></w:r></w:p>
            <w:p><w:r><w:t xml:space="preserve">First </w:t></w:r><w:r><w:t>second</w:t><w:tab/><w:t>third</w:t><w:br/><w:t>fourth</w:t></w:r></w:p>
            <w:p/>
            <w:p><w:r><w:t>Regards</w:t></w:r></w:p>
            """);

        var attachment = await _service.ReadAsync(path, Ct);

        Assert.Equal("docx", attachment.Kind);
        Assert.Equal("Dear reader,\nFirst second\tthird\nfourth\n\nRegards", attachment.Text);
    }

    [Fact]
    public async Task Zip_that_is_not_a_word_document_is_unsupported()
    {
        var path = Path.Combine(_folder, "fake.docx");
        await using (var archive = new ZipArchive(File.Create(path), ZipArchiveMode.Create))
        {
            archive.CreateEntry("readme.txt");
        }

        Assert.Equal(AppErrorKind.FileUnsupported, (await FailsAsync(path)).Kind);
    }

    [Fact]
    public async Task Damaged_word_document_is_unreadable()
    {
        var path = Write("broken.docx", "this is not a zip archive");

        Assert.Equal(AppErrorKind.FileUnreadable, (await FailsAsync(path)).Kind);
    }

    [Fact]
    public async Task Pdf_text_is_extracted_from_every_page()
    {
        var builder = new PdfDocumentBuilder();
        var font = builder.AddStandard14Font(Standard14Font.Helvetica);
        builder.AddPage(PageSize.A4).AddText("First page text", 12, new PdfPoint(50, 700), font);
        builder.AddPage(PageSize.A4).AddText("Second page text", 12, new PdfPoint(50, 700), font);
        var path = Path.Combine(_folder, "report.pdf");
        await File.WriteAllBytesAsync(path, builder.Build(), Ct);

        var attachment = await _service.ReadAsync(path, Ct);

        Assert.Equal("pdf", attachment.Kind);
        Assert.Contains("First page text", attachment.Text);
        Assert.Contains("Second page text", attachment.Text);
        Assert.True(
            attachment.Text.IndexOf("First", StringComparison.Ordinal) < attachment.Text.IndexOf("Second", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Damaged_pdf_is_unreadable()
    {
        var path = Write("broken.pdf", "%PDF-1.7 but nothing else");

        Assert.Equal(AppErrorKind.FileUnreadable, (await FailsAsync(path)).Kind);
    }

    [Theory]
    [InlineData(512, "512 bytes")]
    [InlineData(24 * 1024, "24 KB")]
    [InlineData(3 * 1024 * 1024, "3 MB")]
    public void Sizes_are_formatted_for_people(long bytes, string expected)
    {
        Assert.Equal(expected, FileSize.Format(bytes));
    }

    private async Task<AppError> FailsAsync(string path) =>
        (await Assert.ThrowsAsync<AppException>(() => _service.ReadAsync(path, Ct))).Error;

    private string Write(string name, string content)
    {
        var path = Path.Combine(_folder, name);
        File.WriteAllText(path, content, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        return path;
    }

    private string WriteDocx(string name, string bodyXml)
    {
        var path = Path.Combine(_folder, name);
        using var archive = new ZipArchive(File.Create(path), ZipArchiveMode.Create);
        using var writer = new StreamWriter(archive.CreateEntry("word/document.xml").Open(), Encoding.UTF8);
        writer.Write(
            $"""
            <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
            <w:document xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main"><w:body>{bodyXml}</w:body></w:document>
            """);
        return path;
    }
}
