using System.Security.Cryptography;
using System.Text.Json;

namespace Archiving.LicenseManager;

/// <summary>
/// Where the signing key and the ledger live, and how this program remembers that between runs.
///
/// The key is never copied here and never stored by this program — only its path is remembered. That
/// is deliberate: the documented rule is that <c>license-private.pem</c> stays off the repo and off
/// customer machines, and a tool that quietly cached a copy in %APPDATA% would break that rule on
/// behalf of a user who thought they were following it. Keep the key on removable media or in a
/// password vault; this program will ask for it when it is not there.
/// </summary>
internal sealed class Vault
{
    public string? KeyPath { get; set; }
    public string? LedgerPath { get; set; }

    private static string Dir => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ArchivingLicenses");

    private static string SettingsPath => Path.Combine(Dir, "settings.json");

    /// <summary>Where the ledger goes when the operator has not chosen: beside the key, so that
    /// backing up the key backs up the record of what was signed with it.</summary>
    public string DefaultLedgerPath => KeyPath is null
        ? Path.Combine(Dir, "licenses.json")
        : Path.Combine(Path.GetDirectoryName(KeyPath)!, "licenses.json");

    public static Vault Load()
    {
        try
        {
            return File.Exists(SettingsPath)
                ? JsonSerializer.Deserialize<Vault>(File.ReadAllText(SettingsPath)) ?? new Vault()
                : new Vault();
        }
        catch
        {
            return new Vault();
        }
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Dir);
            File.WriteAllText(SettingsPath, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { /* a read-only profile costs the operator one file-picker per run, nothing worse */ }
    }

    public bool KeyAvailable => KeyPath is not null && File.Exists(KeyPath);

    /// <summary>
    /// Loads the private key for one signing operation. Returned to a <c>using</c> so the key
    /// material is disposed rather than left in memory for the life of the window.
    /// </summary>
    public ECDsa OpenKey()
    {
        if (!KeyAvailable)
            throw new InvalidOperationException(
                "لم يُحدَّد مفتاح التوقيع، أو أن الملف غير موجود في مساره.\n\n" +
                "اختر ملف license-private.pem من زر «مفتاح التوقيع».");

        var key = ECDsa.Create();
        try
        {
            key.ImportFromPem(File.ReadAllText(KeyPath!));
        }
        catch (Exception ex)
        {
            key.Dispose();
            throw new InvalidOperationException(
                $"الملف المحدَّد ليس مفتاح توقيع صالحًا:\n{ex.Message}", ex);
        }
        return key;
    }

    /// <summary>The public half, in the exact form that goes into the product's appsettings.json.
    /// Offered from the same screen because the two are always needed together and copying it out of
    /// a text file by hand is where a stray newline gets introduced.</summary>
    public string PublicKeyBase64()
    {
        using var key = OpenKey();
        return Convert.ToBase64String(key.ExportSubjectPublicKeyInfo());
    }

    /// <summary>Creates a signing key. Refuses to overwrite one, because every licence ever issued
    /// under the old key would stop verifying the moment it was replaced.</summary>
    public static string CreateKey(string directory)
    {
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "license-private.pem");
        if (File.Exists(path))
            throw new InvalidOperationException(
                $"يوجد مفتاح بالفعل في {path}.\n\n" +
                "لن يُستبدل: كل ترخيص صدر بهذا المفتاح سيتوقّف عن العمل فورًا لو استُبدل.");

        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        File.WriteAllText(path, key.ExportPkcs8PrivateKeyPem());
        File.WriteAllText(Path.Combine(directory, "license-public.txt"),
            Convert.ToBase64String(key.ExportSubjectPublicKeyInfo()));
        return path;
    }
}
