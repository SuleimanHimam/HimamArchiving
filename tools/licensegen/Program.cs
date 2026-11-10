using System.Globalization;
using System.Security.Cryptography;
using Archiving.Infrastructure.Licensing;

// Vendor-side licence generator for the Archiving system. Runs on our machine, never at a customer's:
// it holds the private half of the signing key.
//
//   licensegen keygen  [--out <dir>]
//   licensegen issue   --key <private.pem> --customer <name> --machine <fingerprint> [options]
//   licensegen inspect <file|token> [--public-key <base64>]
//   licensegen machineid

return Run(args);

static int Run(string[] args)
{
    if (args.Length == 0 || args[0] is "-h" or "--help" or "help")
    {
        Usage();
        return args.Length == 0 ? 1 : 0;
    }

    var opts = ParseOptions(args.Skip(1).ToArray());
    try
    {
        return args[0] switch
        {
            "keygen"  => KeyGen(opts),
            "issue"   => Issue(opts),
            "inspect" => Inspect(args.Skip(1).FirstOrDefault(), opts),
            "machineid" => MachineId(),
            _         => Fail($"Unknown command '{args[0]}'."),
        };
    }
    catch (Exception e) when (e is IOException or CryptographicException or FormatException or ArgumentException)
    {
        return Fail(e.Message);
    }
}

// ---- commands ---------------------------------------------------------------------------------

static int KeyGen(Dictionary<string, string> o)
{
    var dir = o.GetValueOrDefault("out", ".");
    Directory.CreateDirectory(dir);

    var privatePath = Path.Combine(dir, "license-private.pem");
    if (File.Exists(privatePath))
        return Fail($"{privatePath} already exists. Refusing to overwrite a signing key — " +
                    "every licence ever issued with it would stop verifying.");

    using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
    File.WriteAllText(privatePath, key.ExportPkcs8PrivateKeyPem());
    var publicKey = Convert.ToBase64String(key.ExportSubjectPublicKeyInfo());
    File.WriteAllText(Path.Combine(dir, "license-public.txt"), publicKey);

    Console.WriteLine($"Private key : {privatePath}");
    Console.WriteLine("              KEEP THIS SECRET AND BACK IT UP. It cannot be recovered, and");
    Console.WriteLine("              losing it means re-issuing every customer licence.");
    Console.WriteLine();
    Console.WriteLine("Public key (put this in appsettings.json as License:PublicKey):");
    Console.WriteLine();
    Console.WriteLine(publicKey);
    return 0;
}

/// <summary>Prints the fingerprint of the machine this tool is running on — handy when we install
/// for a customer ourselves, or when someone cannot reach the admin screen to read it.</summary>
static int MachineId()
{
    Console.WriteLine(MachineFingerprint.Current);
    return 0;
}

static int Issue(Dictionary<string, string> o)
{
    var keyPath  = Require(o, "key");
    var customer = Require(o, "customer");
    var machine  = o.GetValueOrDefault("machine", "").Trim().ToUpperInvariant();

    if (machine.Length == 0)
        return Fail("--machine is required: the fingerprint of the customer's server, from " +
                    "Settings > Licence in the app (or GET /api/license/machine-id). " +
                    "Pass --machine * only for our own demo builds.");

    DateTime? expires = null;
    if (o.TryGetValue("expires", out var raw))
    {
        if (!DateTime.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal, out var parsed))
            return Fail($"--expires '{raw}' is not a date (use YYYY-MM-DD).");
        // End of the stated day, so a licence "to 2027-01-01" works all of that day.
        expires = DateTime.SpecifyKind(parsed.Date.AddDays(1).AddSeconds(-1), DateTimeKind.Utc);
    }
    else if (o.TryGetValue("days", out var days))
    {
        expires = DateTime.UtcNow.Date.AddDays(int.Parse(days, CultureInfo.InvariantCulture)).AddSeconds(-1);
    }
    else if (!o.ContainsKey("perpetual"))
    {
        return Fail("Give the term: --days <n>, --expires <YYYY-MM-DD>, or --perpetual.");
    }

    var payload = new LicensePayload
    {
        Id        = Guid.NewGuid().ToString("N")[..12].ToUpperInvariant(),
        Customer  = customer,
        MachineId = machine,
        Edition   = o.GetValueOrDefault("edition", "Standard"),
        IssuedAt  = DateTime.UtcNow,
        ExpiresAt = expires,
        GraceDays = int.Parse(o.GetValueOrDefault("grace", "14"), CultureInfo.InvariantCulture),
        MaxUsers  = o.TryGetValue("max-users", out var mu) ? int.Parse(mu, CultureInfo.InvariantCulture) : null,
        Features  = o.TryGetValue("features", out var f)
            ? f.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            : [],
        Notes     = o.GetValueOrDefault("notes"),
    };

    using var key = ECDsa.Create();
    key.ImportFromPem(File.ReadAllText(keyPath));
    var token = LicenseToken.Create(payload, key);

    var outPath = o.GetValueOrDefault("out");
    if (outPath is not null)
    {
        File.WriteAllText(outPath, token + Environment.NewLine);
        Console.WriteLine($"Wrote {outPath}");
    }

    Describe(payload);
    Console.WriteLine();
    Console.WriteLine(token);
    return 0;
}

static int Inspect(string? target, Dictionary<string, string> o)
{
    if (string.IsNullOrWhiteSpace(target) || target.StartsWith("--"))
        return Fail("Usage: licensegen inspect <file|token> [--public-key <base64>]");

    var token   = File.Exists(target) ? File.ReadAllText(target) : target;
    var payload = LicenseToken.PeekUnverified(token);
    if (payload is null) return Fail("Not a readable licence key.");

    Describe(payload);

    if (o.TryGetValue("public-key", out var pub))
    {
        using var key = ECDsa.Create();
        key.ImportSubjectPublicKeyInfo(Convert.FromBase64String(pub), out _);
        var ok = LicenseToken.TryRead(token, key, out _, out var error);
        Console.WriteLine($"Signature   : {(ok ? "valid" : "INVALID — " + error)}");
        if (!ok) return 1;
    }
    else
    {
        Console.WriteLine("Signature   : not checked (pass --public-key to verify)");
    }
    return 0;
}

// ---- helpers ----------------------------------------------------------------------------------

static void Describe(LicensePayload p)
{
    Console.WriteLine($"Licence id  : {p.Id}");
    Console.WriteLine($"Customer    : {p.Customer}");
    Console.WriteLine($"Machine     : {(p.MachineId == LicensePayload.AnyMachine ? "* (unbound — any machine)" : p.MachineId)}");
    Console.WriteLine($"Edition     : {p.Edition}");
    Console.WriteLine($"Issued      : {p.IssuedAt:yyyy-MM-dd}");
    Console.WriteLine($"Expires     : {(p.ExpiresAt is { } e ? $"{e:yyyy-MM-dd} (+{p.GraceDays}d grace)" : "never (perpetual)")}");
    Console.WriteLine($"Max users   : {(p.MaxUsers is { } m ? m.ToString() : "unlimited")}");
    Console.WriteLine($"Features    : {(p.Features.Length == 0 ? "all" : string.Join(", ", p.Features))}");
    if (!string.IsNullOrWhiteSpace(p.Notes)) Console.WriteLine($"Notes       : {p.Notes}");
}

/// <summary>Flags only: <c>--name value</c>, or <c>--flag</c> for a switch.</summary>
static Dictionary<string, string> ParseOptions(string[] args)
{
    var o = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    for (var i = 0; i < args.Length; i++)
    {
        if (!args[i].StartsWith("--")) continue;
        var name = args[i][2..];
        var hasValue = i + 1 < args.Length && !args[i + 1].StartsWith("--");
        o[name] = hasValue ? args[++i] : "true";
    }
    return o;
}

static string Require(Dictionary<string, string> o, string name) =>
    o.TryGetValue(name, out var v) && v.Length > 0 ? v : throw new ArgumentException($"--{name} is required.");

static int Fail(string message)
{
    Console.Error.WriteLine("error: " + message);
    return 1;
}

static void Usage() => Console.WriteLine("""
    Archiving licence generator

      licensegen keygen [--out <dir>]
          Creates the vendor signing key pair. Run once, ever. Writes license-private.pem
          (secret) and license-public.txt, whose contents go in the product's appsettings.json.

      licensegen issue --key <private.pem> --customer <name> --machine <fingerprint>
                       (--days <n> | --expires <YYYY-MM-DD> | --perpetual)
                       [--edition <name>] [--max-users <n>] [--features a,b,c]
                       [--grace <days>] [--notes <text>] [--out <file.lic>]
          Signs one licence for one machine. The customer reads their fingerprint from
          Settings > Licence in the app, or from GET /api/license/machine-id.

      licensegen machineid
          Prints this machine's fingerprint.

      licensegen inspect <file|token> [--public-key <base64>]
          Prints what a licence contains, and verifies it when given the public key.

    Example:
      licensegen issue --key license-private.pem --customer "Ministry of Health"
          --machine K3M8Q-1ZR4T-9WYH2-B7NDX --days 365 --max-users 50
          --edition Enterprise --out moh.lic
    """);

