using System.Text;
using Microsoft.AspNetCore.Http;
using TechMove.Api.Services;

namespace TechMove.Tests;

internal static class TestFiles
{
    public static readonly byte[] PdfBytes = Encoding.ASCII.GetBytes("%PDF-1.7\n1 0 obj\n<<>>\nendobj\ntrailer\n<<>>\n%%EOF");
    public static readonly byte[] ExeBytes = { 0x4D, 0x5A, 0x90, 0x00, 0x03, 0x00, 0x00, 0x00 }; // "MZ" header

    public static IFormFile FormFile(string fileName, byte[] content, string contentType = "application/pdf")
    {
        var stream = new MemoryStream(content);
        return new FormFile(stream, 0, content.Length, "signedAgreement", fileName)
        {
            Headers = new HeaderDictionary(),
            ContentType = contentType
        };
    }
}

internal sealed class FixedClock : IDateTimeProvider
{
    public FixedClock(DateTime today) => Today = today.Date;
    public DateTime Today { get; }
    public DateTime UtcNow => Today.AddHours(10);
}
