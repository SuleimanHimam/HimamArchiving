using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

/// <summary>Windows Image Acquisition (WIA) access via late-bound COM — no interop assembly required.
/// WIA is the standard, driver-agnostic scanning API on Windows.
///
/// Driver support for individual properties varies enormously, so every property write here is
/// best-effort: a scanner that rejects, say, 600 dpi or a duplex flag should still produce a scan at
/// whatever it does support rather than failing the whole request. Only the transfer itself throws.</summary>
[SupportedOSPlatform("windows")]
internal static class Wia
{
    private const string WiaFormatJpeg = "{B96B3CAE-0728-11D3-9D7B-0000F81EF32E}";
    private const int WiaDeviceTypeScanner = 1;

    // ── Device properties (WIA_DPS_*) ──
    private const int DpsDocumentHandlingCapabilities = 3086;
    private const int DpsDocumentHandlingStatus = 3087;
    private const int DpsDocumentHandlingSelect = 3088;

    // DOCUMENT_HANDLING_SELECT / CAPABILITIES bits
    private const int FeederFlag = 0x001;
    private const int FlatbedFlag = 0x002;
    private const int DuplexFlag = 0x004;
    // DOCUMENT_HANDLING_STATUS bits
    private const int FeedReady = 0x001;

    // ── Item properties (WIA_IPS_* / WIA_IPA_*) ──
    private const int IpaDataType = 4103;
    private const int IpsXRes = 6147;
    private const int IpsYRes = 6148;
    private const int IpsXPos = 6149;
    private const int IpsYPos = 6150;
    private const int IpsXExtent = 6151;
    private const int IpsYExtent = 6152;

    // WIA_IPA_DATATYPE values
    private const int DataTypeThreshold = 0;   // 1-bit black & white
    private const int DataTypeGrayscale = 2;
    private const int DataTypeColor = 3;

    /// <summary>The feeder is empty — the normal way an ADF batch ends, not a failure.</summary>
    private const int WiaErrorPaperEmpty = unchecked((int)0x80210003);

    /// <summary>Hard ceiling so a misreporting feeder cannot spool forever.</summary>
    public const int PageCap = 200;

    public static string[] ListScanners()
    {
        dynamic manager = CreateManager();
        dynamic infos = manager.DeviceInfos;
        var names = new List<string>();
        for (int i = 1; i <= infos.Count; i++)
        {
            dynamic info = infos[i];
            if ((int)info.Type == WiaDeviceTypeScanner)
                names.Add(DeviceName(info, i));
        }
        return names.ToArray();
    }

    /// <summary>What the chosen (or first) scanner can actually do, so the UI and the web app offer
    /// only real choices instead of a fixed menu that half the devices reject.</summary>
    public static ScannerCapabilities Capabilities(string? preferredName)
    {
        try
        {
            dynamic device = Connect(preferredName);
            int handling = ReadInt(device.Properties, DpsDocumentHandlingCapabilities) ?? FlatbedFlag;
            return new ScannerCapabilities
            {
                Feeder = (handling & FeederFlag) != 0,
                Flatbed = (handling & FlatbedFlag) != 0 || handling == 0,
                Duplex = (handling & DuplexFlag) != 0,
            };
        }
        catch
        {
            // No scanner, or a driver that will not describe itself — assume the safe minimum.
            return new ScannerCapabilities { Flatbed = true };
        }
    }

    /// <summary>Acquires one or more pages as JPEGs. Flatbed yields exactly one; the feeder keeps
    /// pulling until the paper runs out, the cap is hit, or the driver says the feed is not ready.</summary>
    public static List<byte[]> ScanPages(ScanOptions opts)
    {
        dynamic device = Connect(opts.Scanner);

        var useFeeder = opts.Source == PaperSource.Feeder;
        TrySetInt(device.Properties, DpsDocumentHandlingSelect,
            useFeeder ? FeederFlag | (opts.Duplex ? DuplexFlag : 0) : FlatbedFlag);

        if ((int)device.Items.Count < 1)
            throw new InvalidOperationException("الماسح متصل لكنه لم يُرجع أي صورة (تحقق من وجود ورقة في الماسح)");

        dynamic item = device.Items[1];
        ApplyItemSettings(item, opts);

        var max = opts.MaxPages > 0 ? Math.Min(opts.MaxPages, PageCap) : PageCap;
        var pages = new List<byte[]>();

        while (pages.Count < max)
        {
            byte[] page;
            try
            {
                dynamic image = item.Transfer(WiaFormatJpeg);
                page = ReadImageBytes(image);
            }
            catch (COMException ex) when (ex.HResult == WiaErrorPaperEmpty)
            {
                break;                       // feeder ran out — the expected end of a batch
            }

            pages.Add(page);

            if (!useFeeder) break;           // flatbed is always a single sheet
            if (!FeedReadyNow(device)) break;
        }

        if (pages.Count == 0)
            throw new InvalidOperationException(useFeeder
                ? "لم يتم العثور على ورق في وحدة التغذية"
                : "تم المسح لكن الملف الناتج فارغ");

        return pages;
    }

    /// <summary>Back-compat single page (the original entry point).</summary>
    public static byte[] ScanJpeg(string? preferredName) =>
        ScanPages(new ScanOptions { Scanner = preferredName })[0];

    private static void ApplyItemSettings(dynamic item, ScanOptions opts)
    {
        object props = item.Properties;

        // Data type before resolution: some drivers reset the resolution range when the colour
        // mode changes, so setting dpi first would silently be undone.
        TrySetInt(props, IpaDataType, opts.Color switch
        {
            ColorMode.BlackWhite => DataTypeThreshold,
            ColorMode.Grayscale => DataTypeGrayscale,
            _ => DataTypeColor,
        });

        TrySetInt(props, IpsXRes, opts.Dpi);
        TrySetInt(props, IpsYRes, opts.Dpi);

        // Extents are in pixels at the current resolution, so they must follow the dpi write.
        if (opts.Paper.Inches() is { } size)
        {
            TrySetInt(props, IpsXPos, 0);
            TrySetInt(props, IpsYPos, 0);
            TrySetInt(props, IpsXExtent, (int)Math.Round(size.W * opts.Dpi));
            TrySetInt(props, IpsYExtent, (int)Math.Round(size.H * opts.Dpi));
        }
    }

    /// <summary>True when the ADF still reports paper. Drivers that do not implement the status
    /// property return null, in which case we keep going and let PAPER_EMPTY end the loop.</summary>
    private static bool FeedReadyNow(dynamic device)
    {
        var status = ReadInt(device.Properties, DpsDocumentHandlingStatus);
        return status is null || (status.Value & FeedReady) != 0;
    }

    private static dynamic Connect(string? preferredName)
    {
        dynamic manager = CreateManager();
        dynamic infos = manager.DeviceInfos;

        dynamic? chosen = null;
        for (int i = 1; i <= infos.Count; i++)
        {
            dynamic info = infos[i];
            if ((int)info.Type != WiaDeviceTypeScanner) continue;
            chosen = info;
            if (preferredName is null) break;
            if (DeviceName(info, i) == preferredName) break;
        }
        if (chosen is null) throw new InvalidOperationException("لا يوجد ماسح ضوئي متصل بالجهاز");
        return chosen.Connect();
    }

    /// <summary>Retrieves the bytes by letting WIA write the image to a temp file, then reading it
    /// back. This avoids fragile SAFEARRAY/BinaryData marshaling that varies across drivers.</summary>
    private static byte[] ReadImageBytes(dynamic image)
    {
        var ext = SafeExtension(image);
        var tmp = Path.Combine(Path.GetTempPath(), $"archiving-scan-{Guid.NewGuid():N}{ext}");
        try
        {
            image.SaveFile(tmp);
            var bytes = File.ReadAllBytes(tmp);
            if (bytes.Length == 0)
                throw new InvalidOperationException("تم المسح لكن الملف الناتج فارغ");
            return bytes;
        }
        finally
        {
            try { if (File.Exists(tmp)) File.Delete(tmp); } catch { /* best effort */ }
        }
    }

    // WIA device-name property id (WIA_DIP_DEV_NAME).
    private const int WiaDipDevName = 7;

    private static string DeviceName(object info, int index)
    {
        // WIA Property.Value is a *parameterized* member, so the C# `dynamic` binder can't read it
        // (same reason get_BinaryData failed). Go through IDispatch via reflection, which handles it.
        try
        {
            object props = GetProp(info, "Properties")!;
            int count = Convert.ToInt32(GetProp(props, "Count"));
            for (int i = 1; i <= count; i++)
            {
                object prop = GetProp(props, "Item", i)!;       // 1-based ordinal
                var pid = Convert.ToInt32(GetProp(prop, "PropertyID"));
                var pname = GetProp(prop, "Name") as string;
                if (pid == WiaDipDevName || pname == "Name")
                {
                    if (GetProp(prop, "Value") is string v && !string.IsNullOrWhiteSpace(v))
                        return v.Trim();
                }
            }
        }
        catch { /* fall through to a generic label */ }
        return $"Scanner {index}";
    }

    /// <summary>Finds a property by WIA id in a Properties collection, or null if absent.</summary>
    private static object? FindProperty(object props, int propertyId)
    {
        int count = Convert.ToInt32(GetProp(props, "Count"));
        for (int i = 1; i <= count; i++)
        {
            object prop = GetProp(props, "Item", i)!;
            if (Convert.ToInt32(GetProp(prop, "PropertyID")) == propertyId) return prop;
        }
        return null;
    }

    private static int? ReadInt(object props, int propertyId)
    {
        try
        {
            var prop = FindProperty(props, propertyId);
            if (prop is null) return null;
            var value = GetProp(prop, "Value");
            return value is null ? null : Convert.ToInt32(value);
        }
        catch { return null; }
    }

    /// <summary>Best-effort property write — see the class remarks. Returns whether it stuck.</summary>
    private static bool TrySetInt(object props, int propertyId, int value)
    {
        try
        {
            var prop = FindProperty(props, propertyId);
            if (prop is null) return false;
            prop.GetType().InvokeMember("Value", BindingFlags.SetProperty, null, prop, [value]);
            return true;
        }
        catch { return false; }   // unsupported value or read-only on this driver
    }

    /// <summary>Reads a COM member through IDispatch (handles WIA's parameterized properties that
    /// late-bound <c>dynamic</c> cannot).</summary>
    private static object? GetProp(object comObj, string member, params object[] args) =>
        comObj.GetType().InvokeMember(member, BindingFlags.GetProperty, null, comObj, args.Length == 0 ? null : args);

    private static string SafeExtension(dynamic image)
    {
        try { return "." + (string)image.FileExtension; } catch { return ".jpg"; }
    }

    private static dynamic CreateManager()
    {
        var type = Type.GetTypeFromProgID("WIA.DeviceManager")
            ?? throw new InvalidOperationException("WIA غير متوفر على هذا الجهاز (تأكد من تثبيت تعريف الماسح)");
        return Activator.CreateInstance(type)
            ?? throw new InvalidOperationException("تعذّر بدء خدمة WIA");
    }
}

/// <summary>What a device supports, as far as its driver will admit.</summary>
internal sealed record ScannerCapabilities
{
    public bool Flatbed { get; init; }
    public bool Feeder { get; init; }
    public bool Duplex { get; init; }
}
