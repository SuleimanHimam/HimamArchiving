using System.Security.Cryptography;
using Archiving.Application.Common.Interfaces;
using Archiving.Infrastructure.Licensing;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Archiving.Tests;

/// <summary>Covers the cases the licence exists to stop: a key copied to a second server, a key
/// edited by hand, a key signed by someone else, and a key that has run out.</summary>
public class LicensingTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "archiving-lic-" + Guid.NewGuid().ToString("N"));
    private readonly ECDsa  _vendorKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);

    public LicensingTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        _vendorKey.Dispose();
        try { Directory.Delete(_dir, recursive: true); } catch (IOException) { /* best effort */ }
        GC.SuppressFinalize(this);
    }

    private string PublicKey => Convert.ToBase64String(_vendorKey.ExportSubjectPublicKeyInfo());

    private string Sign(LicensePayload payload, ECDsa? key = null) =>
        LicenseToken.Create(payload, key ?? _vendorKey);

    private static LicensePayload Payload(string machineId, DateTime? expires = null, int graceDays = 0) => new()
    {
        Id        = "TEST",
        Customer  = "Test Customer",
        MachineId = machineId,
        Edition   = "Enterprise",
        IssuedAt  = DateTime.UtcNow.AddDays(-1),
        ExpiresAt = expires,
        GraceDays = graceDays,
    };

    private LicenseService Service(string licenseText, string? publicKey = null)
    {
        var path = Path.Combine(_dir, Guid.NewGuid().ToString("N") + ".lic");
        File.WriteAllText(path, licenseText);
        return ServiceForPath(path, publicKey);
    }

    private LicenseService ServiceForPath(string path, string? publicKey = null) => new(
        Options.Create(new LicenseOptions { Path = path, PublicKey = publicKey ?? PublicKey }),
        new FakeEnvironment(_dir),
        NullLogger<LicenseService>.Instance);

    [Fact]
    public void Licence_signed_for_this_machine_is_valid()
    {
        var status = Service(Sign(Payload(MachineFingerprint.Current, DateTime.UtcNow.AddDays(30)))).GetStatus();

        Assert.Equal(LicenseState.Valid, status.State);
        Assert.True(status.IsUsable);
        Assert.Equal("Test Customer", status.Customer);
    }

    [Fact]
    public void Licence_issued_for_another_machine_is_rejected()
    {
        // The whole point of the binding: this is the same file the customer legitimately owns,
        // copied onto a second server.
        var status = Service(Sign(Payload("AAAAA-BBBBB-CCCCC-DDDDD", DateTime.UtcNow.AddDays(30)))).GetStatus();

        Assert.Equal(LicenseState.MachineMismatch, status.State);
        Assert.False(status.IsUsable);
    }

    [Fact]
    public void Unbound_licence_runs_anywhere()
    {
        var status = Service(Sign(Payload(LicensePayload.AnyMachine, DateTime.UtcNow.AddDays(30)))).GetStatus();

        Assert.Equal(LicenseState.Valid, status.State);
    }

    [Fact]
    public void Editing_the_payload_breaks_the_signature()
    {
        // Re-point a valid licence at this machine by hand — the signature no longer covers it.
        var token = Sign(Payload("AAAAA-BBBBB-CCCCC-DDDDD", DateTime.UtcNow.AddDays(30)));
        var parts = token.Split('.');
        var forged = $"{parts[0]}.{Convert.ToBase64String(
            System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(
                Payload(MachineFingerprint.Current, DateTime.UtcNow.AddDays(30))))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_')}.{parts[2]}";

        Assert.Equal(LicenseState.Invalid, Service(forged).GetStatus().State);
    }

    [Fact]
    public void Licence_signed_by_a_different_key_is_rejected()
    {
        using var impostor = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var token = Sign(Payload(MachineFingerprint.Current, DateTime.UtcNow.AddDays(30)), impostor);

        Assert.Equal(LicenseState.Invalid, Service(token).GetStatus().State);
    }

    [Fact]
    public void Expired_licence_inside_grace_still_runs()
    {
        var status = Service(Sign(Payload(MachineFingerprint.Current, DateTime.UtcNow.AddDays(-2), graceDays: 14)))
            .GetStatus();

        Assert.Equal(LicenseState.Grace, status.State);
        Assert.True(status.IsUsable);
    }

    [Fact]
    public void Expired_licence_past_grace_stops()
    {
        var status = Service(Sign(Payload(MachineFingerprint.Current, DateTime.UtcNow.AddDays(-20), graceDays: 14)))
            .GetStatus();

        Assert.Equal(LicenseState.Expired, status.State);
        Assert.False(status.IsUsable);
    }

    [Fact]
    public void Perpetual_licence_never_expires()
    {
        var status = Service(Sign(Payload(MachineFingerprint.Current))).GetStatus();

        Assert.Equal(LicenseState.Valid, status.State);
        Assert.Null(status.ExpiresAt);
    }

    [Fact]
    public void Missing_file_and_missing_public_key_are_distinguishable()
    {
        var absent = ServiceForPath(Path.Combine(_dir, "nothing-here.lic")).GetStatus();
        Assert.Equal(LicenseState.Missing, absent.State);

        var unconfigured = Service(Sign(Payload(MachineFingerprint.Current)), publicKey: "").GetStatus();
        Assert.Equal(LicenseState.Invalid, unconfigured.State);
    }

    [Fact]
    public void Feature_list_gates_modules_and_an_empty_list_means_all()
    {
        var limited = Service(Sign(Payload(MachineFingerprint.Current) with { Features = ["workflow", "ocr"] }));
        Assert.True(limited.HasFeature("workflow"));
        Assert.True(limited.HasFeature("OCR"));               // case-insensitive
        Assert.False(limited.HasFeature("preservation"));

        var unrestricted = Service(Sign(Payload(MachineFingerprint.Current)));
        Assert.True(unrestricted.HasFeature("preservation"));

        var expired = Service(Sign(Payload(MachineFingerprint.Current, DateTime.UtcNow.AddDays(-20))));
        Assert.False(expired.HasFeature("workflow"));         // nothing is enabled without a licence
    }

    [Fact]
    public void Install_writes_a_good_licence_and_refuses_a_bad_one()
    {
        var path    = Path.Combine(_dir, "installed.lic");
        var service = ServiceForPath(path);

        var rejected = service.Install(Sign(Payload("AAAAA-BBBBB-CCCCC-DDDDD")));
        Assert.Equal(LicenseState.MachineMismatch, rejected.State);
        Assert.False(File.Exists(path));                      // nothing written on rejection

        var accepted = service.Install(Sign(Payload(MachineFingerprint.Current, DateTime.UtcNow.AddDays(30))));
        Assert.Equal(LicenseState.Valid, accepted.State);
        Assert.True(File.Exists(path));
        Assert.Equal(LicenseState.Valid, ServiceForPath(path).GetStatus().State);
    }

    [Fact]
    public void Whitespace_and_line_breaks_around_a_pasted_key_are_tolerated()
    {
        var token = Sign(Payload(MachineFingerprint.Current, DateTime.UtcNow.AddDays(30)));
        var pasted = "  " + token[..40] + "\r\n" + token[40..] + "\n\n";

        Assert.Equal(LicenseState.Valid, Service(pasted).GetStatus().State);
    }

    [Fact]
    public void Licensing_is_dormant_until_a_signing_key_is_configured()
    {
        // No public key: a development or pre-licensing install must run untouched.
        var dormant = Service(Sign(Payload(MachineFingerprint.Current)), publicKey: "");
        Assert.False(dormant.IsEnforced);
        Assert.True(dormant.HasFeature("anything"));

        Assert.True(Service(Sign(Payload(MachineFingerprint.Current))).IsEnforced);
    }

    [Fact]
    public void Fingerprint_is_stable_and_readable()
    {
        Assert.Equal(MachineFingerprint.Current, MachineFingerprint.Current);
        Assert.Matches("^[0-9A-HJKMNP-TV-Z]{5}(-[0-9A-HJKMNP-TV-Z]{5}){3}$", MachineFingerprint.Current);
    }

    private sealed class FakeEnvironment(string root) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Test";
        public string ApplicationName { get; set; } = "Archiving.Tests";
        public string ContentRootPath { get; set; } = root;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
