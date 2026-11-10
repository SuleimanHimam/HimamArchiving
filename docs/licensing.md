# Licensing

The product is licensed with a **signed licence file that is bound to one server**. There is no
activation server and no call home: the licence is verified locally, which is what an on-premises
archive on a closed network needs.

The binding is the important part. A licence carries the fingerprint of the machine it was issued
for. Copy `license.lic` to a second server and that server refuses it — the fingerprint no longer
matches, and the file cannot be edited to say otherwise because any edit breaks the signature.

## How it fits together

| Piece | Where | Role |
| --- | --- | --- |
| `licensegen` | `tools/licensegen` | Vendor-side CLI. Holds the private key, issues licences. |
| `LicenseToken` | `backend/src/Archiving.Infrastructure/Licensing/LicenseToken.cs` | The file format. Shared by the tool and the server *by link*, so they cannot drift. |
| `MachineFingerprint` | same folder | Computes the server's identity. Also shared by link. |
| `LicenseService` | same folder | Verifies signature → machine → expiry, and caches the result. |
| `LicenseEnforcementMiddleware` | `backend/src/Archiving.Api/Common` | Applies the outcome to every API call. |
| Settings ▸ الترخيص | `frontend/src/components/LicenseSettings.tsx` | Shows the fingerprint and status, takes a new key. |

Signing is ECDSA P-256 / SHA-256. The licence looks like:

```
ARCHLIC-1.<base64url payload>.<base64url signature>
```

The payload is readable JSON on purpose — a customer can see exactly what they bought — but it is
signed, so nothing in it can be changed.

## One-time setup

```bash
dotnet run --project tools/licensegen -- keygen --out ../keys
```

This writes `license-private.pem` and `license-public.txt`.

- **`license-private.pem` is the entire security of the scheme.** Keep it off this repo, off customer
  machines, and backed up somewhere you will still have in five years. If it leaks, anyone can mint
  licences. If it is lost, every licence must be re-issued under a new key.
- Put the contents of `license-public.txt` into the product's `appsettings.json`:

```json
"License": {
  "Path": "license.lic",
  "PublicKey": "MFkwEwYHKoZIzj0CAQYIKoZIzj0DAQcDQgAE...",
  "Enforcement": "ReadOnly",
  "RecheckInterval": "00:05:00"
}
```

Rotating the key later is a config change plus a re-issue, not a rebuild.

**Licensing stays dormant until a public key is configured.** With no key, nothing is verified and
nothing is blocked, so development machines and any install made before licensing was switched on
keep running exactly as they do today. For a release build, paste the key into
`LicenseOptions.EmbeddedPublicKey` as well: `appsettings.json` can be edited on a customer's server,
a compiled-in constant cannot.

## Issuing a licence

1. The customer opens **الإعدادات ▸ الترخيص** and copies their server fingerprint (e.g.
   `P3FYY-5VR8D-QDKKX-7JZS5`). If they cannot get into the app, run `licensegen machineid` on that
   server — it prints the same value.
2. Issue it:

```bash
dotnet run --project tools/licensegen -- issue \
  --key ../keys/license-private.pem \
  --customer "Ministry of Health" \
  --machine P3FYY-5VR8D-QDKKX-7JZS5 \
  --days 365 --max-users 50 --edition Enterprise \
  --out moh.lic
```

   Term is one of `--days <n>`, `--expires <YYYY-MM-DD>` or `--perpetual`. Optional:
   `--grace <days>` (default 14), `--max-users <n>`, `--features a,b,c`, `--notes "PO 1234"`.
   `--machine "*"` issues an unbound licence — only for our own demo boxes.
3. Send the customer the key. They paste it into **الإعدادات ▸ الترخيص** and press تفعيل, or drop the
   file next to the API binary as `license.lic`. Both routes verify before anything is written.

Check what a key contains at any time:

```bash
dotnet run --project tools/licensegen -- inspect moh.lic --public-key "<base64 public key>"
```

## What the server does with it

`LicenseService` re-reads the file when it changes on disk or when `RecheckInterval` elapses, so an
expiry takes effect and a renewal is picked up without restarting the service. The outcome is one of:

| State | Meaning |
| --- | --- |
| `Valid` | Signed, bound to this machine, in date. |
| `Grace` | Expired but inside the grace window — still runs, warns. |
| `Expired` | Past the grace window. |
| `MachineMismatch` | Issued for a different server. |
| `Invalid` | Bad signature, damaged file, or no public key configured. |
| `Missing` | No licence file. |

Every API response carries `X-License-State` (and `X-License-Days-Remaining` while in grace).

### Enforcement

`License:Enforcement` decides what an unlicensed instance may still do:

- **`ReadOnly` (default)** — reads keep working, writes are refused with `403 license_required`.
  Nobody loses access to their own archive because a licence lapsed; they just cannot add to it.
- **`Block`** — everything but signing in and the licence screen is refused.
- **`WarnOnly`** — nothing is blocked, the state is only reported. For pilots.

`/api/auth`, `/api/license`, `/api/branding` and `/health` are always reachable, whatever the state —
otherwise an expired server could not be renewed from its own admin screen.

## The fingerprint

Derived from one stable OS-level identity per platform (on Windows, the serial of the volume Windows
is installed on; on Linux, `/etc/machine-id`), hashed and rendered in Crockford base32 so it can be
read down a phone line. It discloses nothing about the machine.

It survives reboots, upgrades, and hardware changes. It changes when the OS is reinstalled or the
system volume is reformatted — which is the point at which a re-issue is genuinely warranted.

## Limits carried in a licence

`maxUsers` and `features` ride in the payload and are exposed on `ILicenseService`
(`GetStatus().MaxUsers`, `HasFeature("workflow")`). A licence with an empty feature list means every
module is enabled, which is the normal full-product case. Wire `HasFeature` into a module when you
actually want to sell that module separately.

## Tests

`backend/tests/Archiving.Tests/LicensingTests.cs` covers the cases the scheme exists to stop: a key
copied to another machine, a hand-edited payload, a key signed by someone else's key, and expiry
either side of the grace window.
