using TechMove.Web.Services;
using TechMove.Web.Services.Files;

namespace TechMove.Tests;

public class PdfFileValidatorTests
{
    private readonly PdfFileValidator _sut = new();

    [Fact]
    public void Validate_ValidPdf_DoesNotThrow()
    {
        var file = TestFiles.FormFile("agreement.pdf", TestFiles.PdfBytes);

        var ex = Record.Exception(() => _sut.Validate(file));

        Assert.Null(ex);
    }

    [Fact]
    public void Validate_UppercaseExtension_IsAccepted()
    {
        var file = TestFiles.FormFile("AGREEMENT.PDF", TestFiles.PdfBytes);

        Assert.Null(Record.Exception(() => _sut.Validate(file)));
    }

    [Fact]
    public void Validate_ExeFile_ThrowsInvalidFileException()
    {
        var file = TestFiles.FormFile("virus.exe", TestFiles.ExeBytes, "application/octet-stream");

        var ex = Assert.Throws<InvalidFileException>(() => _sut.Validate(file));
        Assert.Contains(".exe", ex.Message);
    }

    [Theory]
    [InlineData("agreement.pdf.exe")] // double extension trick
    [InlineData("agreement.docx")]
    [InlineData("agreement.jpg")]
    [InlineData("agreement")]          // no extension
    public void Validate_NonPdfExtensions_Throw(string fileName)
    {
        var file = TestFiles.FormFile(fileName, TestFiles.PdfBytes);

        Assert.Throws<InvalidFileException>(() => _sut.Validate(file));
    }

    [Fact]
    public void Validate_ExeRenamedToPdf_ThrowsBecauseSignatureIsWrong()
    {
        var file = TestFiles.FormFile("renamed.pdf", TestFiles.ExeBytes);

        var ex = Assert.Throws<InvalidFileException>(() => _sut.Validate(file));
        Assert.Contains("not a valid PDF", ex.Message);
    }

    [Fact]
    public void Validate_NullFile_Throws()
    {
        Assert.Throws<InvalidFileException>(() => _sut.Validate((Microsoft.AspNetCore.Http.IFormFile?)null));
    }

    [Fact]
    public void Validate_EmptyFile_Throws()
    {
        var file = TestFiles.FormFile("empty.pdf", Array.Empty<byte>());

        var ex = Assert.Throws<InvalidFileException>(() => _sut.Validate(file));
        Assert.Contains("empty", ex.Message);
    }

    [Fact]
    public void Validate_FileOverLimit_Throws()
    {
        var ex = Assert.Throws<InvalidFileException>(() =>
            _sut.Validate("big.pdf", PdfFileValidator.MaxFileSizeBytes + 1, null));

        Assert.Contains("too large", ex.Message);
    }

    [Fact]
    public void Validate_FileExactlyAtLimit_IsAccepted()
    {
        Assert.Null(Record.Exception(() =>
            _sut.Validate("max.pdf", PdfFileValidator.MaxFileSizeBytes, null)));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_MissingFileName_Throws(string? name)
    {
        Assert.Throws<InvalidFileException>(() => _sut.Validate(name, 100, null));
    }

    [Fact]
    public void Validate_ResetsStreamPosition_SoFileCanStillBeSaved()
    {
        using var stream = new MemoryStream(TestFiles.PdfBytes);

        _sut.Validate("a.pdf", stream.Length, stream);

        Assert.Equal(0, stream.Position);
    }
}
