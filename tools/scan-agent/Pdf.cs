using System.Globalization;
using System.Text;

/// <summary>Wraps a JPEG into a minimal single-page PDF (image embedded as a DCTDecode XObject).
/// Avoids any PDF dependency so the agent stays a single self-contained executable.</summary>
internal static class Pdf
{
    public static byte[] FromJpeg(byte[] jpeg)
    {
        var (w, h, components) = JpegInfo(jpeg);
        // Match the PDF image colour space to the JPEG's actual channel count, otherwise scanners'
        // common grayscale output renders as a blank page under a hard-coded DeviceRGB.
        var colorSpace = components switch { 1 => "/DeviceGray", 4 => "/DeviceCMYK", _ => "/DeviceRGB" };
        // Page is sized to the image at 72 dpi-equivalent points; the image fills the page.
        int pw = w, ph = h;

        using var ms = new MemoryStream();
        var offsets = new List<long> { 0 }; // object 0 is the free head
        void Obj(string body)
        {
            offsets.Add(ms.Position);
            WriteAscii(ms, body);
        }

        WriteAscii(ms, "%PDF-1.4\n%âãÏÓ\n");

        Obj("1 0 obj\n<< /Type /Catalog /Pages 2 0 R >>\nendobj\n");
        Obj("2 0 obj\n<< /Type /Pages /Kids [3 0 R] /Count 1 >>\nendobj\n");
        Obj($"3 0 obj\n<< /Type /Page /Parent 2 0 R /MediaBox [0 0 {pw} {ph}] " +
            "/Resources << /XObject << /Im0 4 0 R >> >> /Contents 5 0 R >>\nendobj\n");

        // Image XObject (object 4): dictionary + binary JPEG stream.
        offsets.Add(ms.Position);
        WriteAscii(ms, $"4 0 obj\n<< /Type /XObject /Subtype /Image /Width {w} /Height {h} " +
            $"/ColorSpace {colorSpace} /BitsPerComponent 8 /Filter /DCTDecode /Length {jpeg.Length} >>\nstream\n");
        ms.Write(jpeg, 0, jpeg.Length);
        WriteAscii(ms, "\nendstream\nendobj\n");

        var content = $"q {pw} 0 0 {ph} 0 0 cm /Im0 Do Q\n";
        var contentBytes = Encoding.ASCII.GetByteCount(content);
        Obj($"5 0 obj\n<< /Length {contentBytes} >>\nstream\n{content}endstream\nendobj\n");

        long xrefPos = ms.Position;
        var sb = new StringBuilder();
        sb.Append("xref\n0 6\n");
        sb.Append("0000000000 65535 f \n");
        for (int i = 1; i <= 5; i++)
            sb.Append(offsets[i].ToString("D10", CultureInfo.InvariantCulture)).Append(" 00000 n \n");
        sb.Append("trailer\n<< /Size 6 /Root 1 0 R >>\n");
        sb.Append("startxref\n").Append(xrefPos.ToString(CultureInfo.InvariantCulture)).Append("\n%%EOF\n");
        WriteAscii(ms, sb.ToString());

        return ms.ToArray();
    }

    private static void WriteAscii(Stream s, string text)
    {
        var bytes = Encoding.ASCII.GetBytes(text);
        s.Write(bytes, 0, bytes.Length);
    }

    /// <summary>Reads width/height and channel count from the JPEG's SOF marker.</summary>
    private static (int Width, int Height, int Components) JpegInfo(byte[] j)
    {
        int i = 2; // skip SOI (FFD8)
        while (i + 9 < j.Length)
        {
            if (j[i] != 0xFF) { i++; continue; }
            byte marker = j[i + 1];
            int len = (j[i + 2] << 8) | j[i + 3];
            // SOF0..SOF15 carry frame dimensions (excluding DHT/DAC/RST/SOS markers).
            bool isSof = marker is >= 0xC0 and <= 0xCF && marker != 0xC4 && marker != 0xC8 && marker != 0xCC;
            if (isSof)
            {
                int height = (j[i + 5] << 8) | j[i + 6];
                int width = (j[i + 7] << 8) | j[i + 8];
                int components = j[i + 9];
                if (width > 0 && height > 0) return (width, height, components == 0 ? 3 : components);
            }
            i += 2 + len;
        }
        return (1240, 1754, 3); // sensible A4-ish RGB fallback if the header can't be read
    }
}
