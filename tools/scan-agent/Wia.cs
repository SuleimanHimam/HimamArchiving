using System.Reflection;
using System.Runtime.Versioning;

/// <summary>Windows Image Acquisition (WIA) access via late-bound COM — no interop assembly required.
/// WIA is the standard, driver-agnostic scanning API on Windows.</summary>
[SupportedOSPlatform("windows")]
internal static class Wia
{
    private const string WiaFormatJpeg = "{B96B3CAE-0728-11D3-9D7B-0000F81EF32E}";
    private const int WiaDeviceTypeScanner = 1;

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

    public static byte[] ScanJpeg(string? preferredName)
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

        dynamic device = chosen.Connect();
        if ((int)device.Items.Count < 1)
            throw new InvalidOperationException("الماسح متصل لكنه لم يُرجع أي صورة (تحقق من وجود ورقة في الماسح)");

        dynamic item = device.Items[1];
        dynamic image = item.Transfer(WiaFormatJpeg);

        // Retrieve the bytes by letting WIA write the image to a temp file, then reading it back.
        // This avoids fragile SAFEARRAY/BinaryData marshaling that varies across scanner drivers.
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
