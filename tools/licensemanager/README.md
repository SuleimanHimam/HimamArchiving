# مراسيل — Licence Manager

Vendor-side program for issuing licences **and keeping the record of what was issued**. Runs on your
machine only: it holds the private half of the signing key.

```
bصمة الجهاز من العميل  ──►  [ تراخيص مراسيل ]  ──►  license.lic  ──►  خادم العميل
                                    │
                              licenses.json   ← ما بيع، لمن، حتى متى
```

## Why this exists alongside `licensegen`

The CLI in [`tools/licensegen`](../licensegen) still works, uses the same format, and is unchanged.
What it could not do is **remember**. It would mint a licence and forget it the same second, which
left three questions with no answer:

- What did we sell this customer?
- Whose licence expires next month?
- Have we already issued one for this machine?

This program answers all three, because every licence it signs is written to a ledger before it
leaves the screen. Signing and recording happen together or not at all.

## What it does

| | |
|---|---|
| **Issue** | Customer, machine fingerprint, edition, term, grace period, user cap, module allow-list, notes |
| **Renew** | New signature carrying an existing licence's terms; the old one is marked superseded |
| **Save / copy** | Write `license.lic` for the customer, or copy the raw token |
| **Inspect** | Read any licence file — ours or one a customer mailed back — and verify its signature |
| **Revoke** | A vendor-side note so a replaced licence is never re-sent by mistake |
| **Key management** | Generate a signing key (once), or point at an existing one; copy the public key for `appsettings.json` |

## The fingerprint check

A licence is signed **over** the machine fingerprint, so one wrong character produces a licence that
is perfectly valid, says the right customer name, and is rejected by the customer's server — usually
discovered by them, on a Sunday.

So the fingerprint is validated before signing, and repaired where repair is unambiguous. The
fingerprint alphabet omits `I`, `L`, `O` and `U` by design, which means `O → 0`, `I/L → 1` and
`U → V` cannot destroy a legitimate character — they can only recover a mis-transcribed one. Repairs
are shown, never applied silently.

It also warns when the machine already has a live licence, so you renew rather than quietly create a
second licence for one server.

## The signing key

**This program never stores your key — only its path.** The documented rule is that
`license-private.pem` stays off the repo and off customer machines, and a tool that cached a copy in
`%APPDATA%` would break that rule for a user who thought they were following it. Keep it on removable
media or in a password vault, and point the program at it.

Two rules the program enforces for you:

- **It refuses to overwrite an existing key.** Every licence ever issued under the old key would stop
  verifying the moment it was replaced.
- **It verifies every licence it signs, against its own public key, before recording it.** Signing
  and verifying are different code paths; the bad moment to learn they disagree is after the customer
  has the file.

Back the key up somewhere you will still have in five years. It cannot be recovered, and losing it
means re-issuing every customer licence under a new key.

## The ledger

A flat JSON file, `licenses.json`, kept beside the key by default — so backing up the key backs up
the record of what was signed with it. Readable without this program, and it outlives it.

Written to a temp file and moved into place, because a crash mid-write would otherwise truncate the
only record that a licence exists. A ledger that fails to parse stops the program rather than
starting empty — starting empty would overwrite the whole history on the next issue.

## Revocation is a note, not a kill switch

There is no call home, by design — an on-premises archive on a closed network cannot depend on one.
So revoking marks the licence in **your** ledger; the customer's copy keeps working until it expires.
The value is that nobody on your side re-sends a licence that was replaced or refunded.

## Build and run

```bash
dotnet build tools/licensemanager          # develop
dotnet publish tools/licensemanager -c Release
```

Publishes a single self-contained `archiving-licenses.exe` — no .NET runtime needed on the machine
that runs it.

## First-time setup

1. **Create a signing key** — «إنشاء مفتاح…». Once, ever, for the life of the product.
2. **Back it up.** Now, not later.
3. **Copy the public key** — «نسخ المفتاح العام» — into the product's `appsettings.json`:

```json
"License": {
  "Path": "license.lic",
  "PublicKey": "MFkwEwYHKoZIzj0CAQYIKoZIzj0DAQcDQgAE…",
  "Enforcement": "ReadOnly",
  "RecheckInterval": "00:05:00"
}
```

Then per customer: they read their fingerprint from **الإعدادات ← الترخيص** (or `GET
/api/license/machine-id`), you paste it into «ترخيص جديد», and send them the `license.lic` to drop
beside the server executable.
