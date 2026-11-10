using System.Globalization;
using System.Text;

/// <summary>Wraps scanned JPEGs into a PDF (each image embedded as a DCTDecode XObject).
/// Avoids any PDF dependency so the agent stays a single self-contained executable.
///
/// Object layout for N pages:
///   1        catalog
///   2        page tree
///   3..      per page: page object, image XObject, content stream (three objects each)
/// </summary>
internal static class Pdf
{
    public static byte[] FromJpeg(byte[] jpeg) => FromJpegs([jpeg]);

    public static byte[] FromJpegs(IReadOnlyList<byte[]> jpegs)
    {
        if (jpegs.Count == 0) throw new ArgumentException("لا توجد صفحات لبناء ملف PDF", nameof(jpegs));

        using var ms = new MemoryStream();
        // Object numbers are 1-based; index 0 is the free head, so `offsets[n]` is the byte offset
        // of object n — exactly what the xref table needs.
        var offsets = new List<long> { 0 };

        void BeginObj() => offsets.Add(ms.Position);
        void Obj(string body) { BeginObj(); WriteAscii(ms, body); }

        WriteAscii(ms, "%PDF-1.4\n%âãÏÓ\n");

        // Page objects start at 3 and consume three object numbers each.
        int PageObj(int i) => 3 + i * 3;
        var kids = string.Join(" ", Enumerable.Range(0, jpegs.Count).Select(i => $"{PageObj(i)} 0 R"));

        Obj("1 0 obj\n<< /Type /Catalog /Pages 2 0 R >>\nendobj\n");
        Obj($"2 0 obj\n<< /Type /Pages /Kids [{kids}] /Count {jpegs.Count} >>\nendobj\n");

        for (int i = 0; i < jpegs.Count; i++)
        {
            var jpeg = jpegs[i];
            var (w, h, components) = JpegInfo(jpeg);
            // Match the PDF image colour space to the JPEG's actual channel count, otherwise
            // scanners' common grayscale output renders blank under a hard-coded DeviceRGB.
            var colorSpace = components switch { 1 => "/DeviceGray", 4 => "/DeviceCMYK", _ => "/DeviceRGB" };
            // Page is sized to the image at 72 dpi-equivalent points; the image fills the page.
            int pw = w, ph = h;

            int page = PageObj(i), img = page + 1, content = page + 2;

            Obj($"{page} 0 obj\n<< /Type /Page /Parent 2 0 R /MediaBox [0 0 {pw} {ph}] " +
                $"/Resources << /XObject << /Im0 {img} 0 R >> >> /Contents {content} 0 R >>\nendobj\n");

            // Image XObject: dictionary + binary JPEG stream.
            BeginObj();
            WriteAscii(ms, $"{img} 0 obj\n<< /Type /XObject /Subtype /Image /Width {w} /Height {h} " +
                $"/ColorSpace {colorSpace} /BitsPerComponent 8 /Filter /DCTDecode /Length {jpeg.Length} >>\nstream\n");
            ms.Write(jpeg, 0, jpeg.Length);
            WriteAscii(ms, "\nendstream\nendobj\n");

            var stream = $"q {pw} 0 0 {ph} 0 0 cm /Im0 Do Q\n";
            Obj($"{content} 0 obj\n<< /Length {Encoding.ASCII.GetByteCount(stream)} >>\nstream\n{stream}endstream\nendobj\n");
        }

        int objectCount = offsets.Count;          // includes the free head at index 0
        long xrefPos = ms.Position;
        var sb = new StringBuilder();
        sb.Append(CultureInfo.InvariantCulture, $"xref\n0 {objectCount}\n");
        sb.Append("0000000000 65535 f \n");
        for (int i = 1; i < objectCount; i++)
            sb.Append(offsets[i].ToString("D10", CultureInfo.InvariantCulture)).Append(" 00000 n \n");
        sb.Append(CultureInfo.InvariantCulture, $"trailer\n<< /Size {objectCount} /Root 1 0 R >>\n");
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
