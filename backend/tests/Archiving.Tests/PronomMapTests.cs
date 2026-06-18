using Archiving.Infrastructure.Services;
using Xunit;

namespace Archiving.Tests;

public class PronomMapTests
{
    [Theory]
    [InlineData("pdf", "fmt/276")]
    [InlineData("jpg", "fmt/44")]
    [InlineData("jpeg", "fmt/44")]
    [InlineData("png", "fmt/13")]
    [InlineData("bmp", "fmt/116")]
    [InlineData("tiff", "fmt/353")]
    public void Maps_extension_to_pronom_puid(string ext, string expected)
    {
        var (puid, _, _) = PronomMap.For(ext, null, "application/octet-stream");
        Assert.Equal(expected, puid);
    }

    [Fact]
    public void PdfA2b_maps_to_fmt477_and_keeps_format_name()
    {
        var (puid, format, note) = PronomMap.For("pdf", "PDF/A-2B", "application/pdf");
        Assert.Equal("fmt/477", puid);
        Assert.Equal("PDF/A-2B", format);
        Assert.False(string.IsNullOrEmpty(note));
    }

    [Fact]
    public void Unknown_extension_has_no_puid()
    {
        var (puid, _, _) = PronomMap.For("xyz", null, "application/octet-stream");
        Assert.Null(puid);
    }
}
