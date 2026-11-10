/// <summary>How a scan should be acquired. Defaults reproduce the agent's original behaviour —
/// one flatbed page, colour, 200 dpi — so a caller that sends nothing gets what it always got.</summary>
internal sealed record ScanOptions
{
    /// <summary>Preferred device name; null takes the first scanner WIA reports.</summary>
    public string? Scanner { get; init; }

    /// <summary>Dots per inch. Clamped to what the device advertises at scan time.</summary>
    public int Dpi { get; init; } = 200;

    public ColorMode Color { get; init; } = ColorMode.Color;

    /// <summary>Page size the scan area is set to. Unknown/None leaves the driver's own extent.</summary>
    public PaperSize Paper { get; init; } = PaperSize.A4;

    /// <summary>Flatbed = one page from the glass. Feeder = pull from the ADF until it empties.</summary>
    public PaperSource Source { get; init; } = PaperSource.Flatbed;

    /// <summary>Scan both sides. Ignored unless the feeder is used and the device reports duplex.</summary>
    public bool Duplex { get; init; }

    /// <summary>Safety stop for a feeder that never reports empty. 0 = the agent's own cap.</summary>
    public int MaxPages { get; init; }

    /// <summary>Show the agent's preview window and wait for the user to approve/re-scan pages
    /// before the HTTP response is written.</summary>
    public bool Preview { get; init; }
}

internal enum ColorMode
{
    /// <summary>1-bit black and white — smallest files, best for plain text forms.</summary>
    BlackWhite,
    Grayscale,
    Color,
}

internal enum PaperSource
{
    Flatbed,
    Feeder,
}

/// <summary>Page sizes the agent can set a scan extent for, in hundredths of an inch
/// (WIA's unit for WIA_IPS_XEXTENT / WIA_IPS_YEXTENT is pixels, converted at the chosen dpi).</summary>
internal enum PaperSize
{
    /// <summary>Leave whatever extent the driver defaults to.</summary>
    None,
    A4,
    A5,
    Letter,
    Legal,
}

internal static class PaperSizes
{
    /// <summary>Width/height in inches. WIA wants pixels, which depend on dpi, so the conversion
    /// happens where the resolution is known rather than being baked in here.</summary>
    public static (double W, double H)? Inches(this PaperSize size) => size switch
    {
        PaperSize.A4 => (8.27, 11.69),
        PaperSize.A5 => (5.83, 8.27),
        PaperSize.Letter => (8.5, 11.0),
        PaperSize.Legal => (8.5, 14.0),
        _ => null,
    };
}
