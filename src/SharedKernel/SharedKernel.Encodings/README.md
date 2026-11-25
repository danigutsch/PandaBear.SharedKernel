# PandaBear.SharedKernel.Encodings

[![Repository-wide build status](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml)

Packages remain preview (prerelease); stable packages, tags, and releases require separate authorization.

Text encodings that .NET does not provide. It has no dependency.

## Bech32

[Bech32](https://github.com/bitcoin/bips/blob/master/bip-0173.mediawiki) is a human-readable prefix, the separator `1`, data
in a 32-character alphabet, and a six-character checksum that catches a mistyped or truncated string. [age](https://age-encryption.org)
writes its public recipients (`age1...`) and private identities (`AGE-SECRET-KEY-1...`) in it, and lifts BIP-173's limit of 90
characters, so the limit is a parameter.

```csharp
using SharedKernel.Encodings;

if (Bech32.TryDecode(text, maxLength: null, out var prefix, out var fiveBit)
    && prefix == "age"
    && Bech32.TryConvertToBytes(fiveBit, out var key)
    && key.Length == 32)
{
    // text is an X25519 age recipient
}
```

- `TryDecode` accepts all lowercase or all uppercase, never mixed case, checks the checksum, and returns the prefix in
  lowercase and the data as 5-bit values. It is BIP-173 Bech32, not Bech32m: it passes the BIP-173 test vectors and refuses the
  Bech32m ones.
- `TryConvertToBytes` regroups the 5-bit values into bytes and refuses padding of five bits or more, or padding that is not zero.
- `Bip173MaxLength` is 90, for callers that want BIP-173's limit.

## Base32

`Base32` is [RFC 4648](https://www.rfc-editor.org/rfc/rfc4648#section-6) Base32, the encoding of TOTP secrets. `Encode` writes
bytes without padding, and `Normalize` uppercases text and drops everything outside the alphabet, such as the spaces and dashes
that authenticator setup screens put into a secret.
