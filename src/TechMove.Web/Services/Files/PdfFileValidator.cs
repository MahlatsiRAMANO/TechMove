namespace TechMove.Web.Services.Files;

public interface IFileValidator
{
    void Validate(string? fileName, long length, Stream? content);
    void Validate(IFormFile? file);
}

/// <summary>
/// Only real PDFs are accepted: extension must be .pdf, the file must not be empty or too big,
/// and the first bytes must be the PDF signature "%PDF" (so a renamed .exe is rejected too).
/// </summary>
public sealed class PdfFileValidator : IFileValidator
{
    public const long MaxFileSizeBytes = 10 * 1024 * 1024; // 10 MB
    public const string AllowedExtension = ".pdf";

    private static readonly byte[] PdfSignature = { 0x25, 0x50, 0x44, 0x46 }; // "%PDF"

    public void Validate(IFormFile? file)
    {
        if (file is null)
        {
            throw new InvalidFileException("Please choose a PDF file to upload.");
        }

        using var stream = file.OpenReadStream();
        Validate(file.FileName, file.Length, stream);
    }

    public void Validate(string? fileName, long length, Stream? content)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            throw new InvalidFileException("Please choose a PDF file to upload.");
        }

        var extension = Path.GetExtension(fileName.Trim());
        if (!string.Equals(extension, AllowedExtension, StringComparison.OrdinalIgnoreCase))
        {
            var shown = string.IsNullOrEmpty(extension) ? "(no extension)" : extension;
            throw new InvalidFileException($"Only .pdf files are allowed. '{shown}' files are not permitted.");
        }

        if (length <= 0)
        {
            throw new InvalidFileException("The uploaded file is empty.");
        }

        if (length > MaxFileSizeBytes)
        {
            throw new InvalidFileException($"The file is too large. The maximum size is {MaxFileSizeBytes / (1024 * 1024)} MB.");
        }

        if (content is not null && !HasPdfSignature(content))
        {
            throw new InvalidFileException("The file is not a valid PDF document.");
        }
    }

    private static bool HasPdfSignature(Stream content)
    {
        var start = content.CanSeek ? content.Position : 0;
        var header = new byte[PdfSignature.Length];
        var read = 0;
        while (read < header.Length)
        {
            var n = content.Read(header, read, header.Length - read);
            if (n == 0) break;
            read += n;
        }

        if (content.CanSeek)
        {
            content.Position = start;
        }

        return read == header.Length && header.AsSpan().SequenceEqual(PdfSignature);
    }
}
