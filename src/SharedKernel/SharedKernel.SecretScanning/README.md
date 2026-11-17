# PandaBear.SharedKernel.SecretScanning

[![Repository-wide build status](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml)

Packages remain preview (prerelease); stable packages, tags, and releases require separate authorization.

Finds secrets in text and reports where they are, never what they are. It has no package dependency beyond other
SharedKernel libraries and makes no network request. The vault manager uses it to find secrets in password manager
exports; any tool that scans notes, files, or configuration can use it the same way.

```csharp
using PandaBear.SecretScanning;

IReadOnlyList<DetectedSecret> findings = SecretScanner.Default.Scan(text, "notes", SecretScanContext.None);
foreach (DetectedSecret finding in findings)
{
    Console.WriteLine(finding); // such as "jwt@notes:14 Jwt High length 119: JWT", never the value
}
```

## What it finds

- **Catalog rules** for credentials, keys, and tokens of common services: cloud keys (AWS, GCP, Azure SAS), source host and
  package registry tokens, chat webhooks, JWTs, database URIs and connection strings, PEM, PGP, and PuTTY private keys, age
  identities, and more. Each rule runs only on a bounded window around one of its keywords, and validates what it matches
  where it can: checksums of GitHub and npm tokens, Bech32 of age identities, JWT structure, the Luhn check of card numbers.
- **Structural detectors** for labeled lines (`password:`, `PIN:`, `token:`, `recovery code:`, a user name), security
  questions and answers, blocks of recovery codes, card numbers, IBANs and BICs, BIP-39 and Electrum seed phrases, TOTP secrets
  and `otpauth://` URIs, Steam Desktop Authenticator `.maFile` secrets, and long high-entropy strings.
- **Time limits.** Every pattern has a one-second match timeout. A match that times out runs once more; when it times out
  again, the region becomes a finding with `TimedOut` set that needs review, so text the patterns cannot handle is flagged,
  never silently skipped. After three such windows the rest of a rule's windows become one finding.

## Results without values

`DetectedSecret` holds the rule, the kind, the confidence, the location, an optional label location, and a message. It never
holds the value, and its `ToString` never prints one. A caller that needs the value to act on it uses the projection overload,
which hands each value over once, as it is found, and keeps nothing:

```csharp
var placed = SecretScanner.Default.Scan(text, "notes", context, (finding, value) => Decide(finding, value));
```

The value is `null` for a finding that spans text that is not one value: a seed phrase, a block of recovery codes, or a region
the patterns timed out on.

`SecretScanContext` says what is known about the text: the file it came from (`FileName`, which recognises `.maFile` and
`MEGA-RECOVERYKEY.txt` files and file names that announce recovery codes), the account's `Host`, whether the account still
`ExpectsUsername`, whether it `IsBitwardenAccount` (whose recovery code it may hold), and `CatalogRulesOnly` to skip the
structural detectors.

## Keys and TOTP

- `SshKeyConverter.Inspect` tells a key's format, algorithm, and whether it is encrypted, and `Convert` turns an OpenSSH,
  PKCS#1, SEC1, or PKCS#8 private key into a `SshKeyPair`: the private key (OpenSSH or PKCS#8), the public key line, and the
  `SHA256:` fingerprint as `ssh-keygen -lf` prints it. Encrypted, PuTTY, DSA, and security-key keys are reported, not converted.
- `PemBlock.TryParse` reads a PEM block's label, RFC 1421 headers, and body.
- `TotpUri.TryNormalize` turns a base32 seed, an `otpauth://totp` URI, or a `steam://` secret into normalized text, and
  `DecodeMigration` decodes a Google Authenticator `otpauth-migration://` export.
- `ConnectionParts` splits a database URI or connection string into user name, password, host, and an address without
  credentials. `CardNumberValidator.TryGetBrand` names the brand of a card number that passes the Luhn check.

Records that hold secret material (`ConnectionParts`, `MigratedOtp`, `PemBlock`, `SshKeyPair`) print only their non-secret
members.

## Limits

- Detection is heuristic. A secret in a format the catalog does not know, or one that looks like ordinary text, is missed;
  a random-looking non-secret can be reported with low confidence.
- It reads text only; binary files must be extracted first.
