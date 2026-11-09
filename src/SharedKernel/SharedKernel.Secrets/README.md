# PandaBear.SharedKernel.Secrets

An engine for secrets that live in git as [age](https://age-encryption.org) and [SOPS](https://getsops.io) files:
validated public recipients, references to identity files (never keys), creation rules, process adapters for `age`,
`age-keygen`, and `sops` that keep secrets on standard input and output, and an exact recipient-set check. Targets .NET 10.
It has no NuGet dependency beyond the other SharedKernel libraries, and it does not implement any cryptography: the programs do.

```csharp
using PandaBear.Secrets;

var runner = new SystemToolRunner();
var age = new ProcessAgeTool(runner, "/usr/bin/age", "/usr/bin/age-keygen");

var identity = AgeIdentityRef.Create("/home/me/.config/age/work.txt").Value;
var recipient = (await age.GenerateIdentity(identity, ct)).Value;        // writes the 0600 identity file; returns the public recipient

var recipients = AgeRecipientSet.Create([recipient, AgeRecipient.Create("age1...").Value]).Value;
var rule = SopsCreationRule.Create(@"^secrets/.*\.sops\.ya?ml$", recipients, minimumRecipients: 2).Value;

var sops = new ProcessSopsTool(runner, "/usr/local/bin/sops");
var check = (await sops.VerifyRecipients("/repo/secrets/app.sops.yaml", rule, ct)).Value;
if (!check.IsExact) { /* check.Missing, check.Unexpected, check.OtherKeyTypes */ }

using var password = (await sops.DecryptValue("/repo", SecretRef.Create("secrets/app.sops.yaml#db.password").Value, identity, ct)).Value;
```

## Value objects

Each is built only through a `Create` that returns a `Result`; its validation error says what is wrong and never repeats
the input.

- `AgeRecipient`: a public recipient. It accepts Bech32 recipients whose human-readable part is on an allow-list (`age`,
  `age1pq`, `age1tag`, `age1tagpq`, and the plugin parts `age1yubikey`, `age1tpm`, `age1se`, `age1fido2-hmac`) and SSH
  recipient lines `ssh-ed25519 <key>` and `ssh-rsa <key>` with no comment, no white space in the key, canonical base64, and a blob that
  parses exactly (an ed25519 key is 32 bytes, an RSA key two minimal positive integers, nothing after it). A native `age1...` recipient must carry a 32-byte
  key. A Bech32 recipient is stored lowercase, so the uppercase spelling is equal. Private identities
  (`AGE-SECRET-KEY-1...`, `AGE-PLUGIN-...`) are rejected with their own message.
- `AgeRecipientSet`: distinct recipients with order-insensitive equality: a reordered set is equal, a subset or a
  superset is not.
- `AgeIdentityRef`: the absolute path of an identity file. Text that looks like key material is refused with "Pass the path
  of an identity file, never the key itself." A plugin identity is referenced the same way, as the file that holds its stub.
- `EncryptedFilePath`: a safe relative path (`/` separators, no empty, `.` or `..` segment, no `\`, `:`, or control
  character) whose file name matches `*.sops.*`. `ResolveUnder(root)` joins it to a root and refuses a symbolic link.
- `SecretRef`: `secrets/app.sops.yaml#db.password` or `...#servers[0].token`. It names a value; it never holds one. Key
  names are limited to ASCII letters, digits, `_`, and `-`.
- `SopsCreationRule`: a path expression, an exact recipient set, and the least number of recipients. The minimum is the
  caller's parameter (a rule with fewer recipients cannot be built), not a constant of the library. The expression is
  unanchored like SOPS's `path_regex` and compiles with `RegexOptions.NonBacktracking` and a one-second timeout, so
  look-around and back-references are refused. `SopsCreationRuleSet.FirstMatch` returns the first matching rule, as SOPS does.

Bech32 is the BIP-173 variant (not Bech32m) with no length limit, which is what the [age specification](https://github.com/C2SP/C2SP/blob/main/age.md)
requires. The checksum code is checked against the BIP-173 vectors and the Bech32m vectors are rejected.

## Ports and adapters

`IAgeTool` (`GenerateIdentity`, `Encrypt`, `Decrypt`) and `ISopsTool` (`Encrypt`, `Decrypt`, `DecryptValue`,
`ListRecipients`, `VerifyRecipients`) have process adapters, `ProcessAgeTool` and `ProcessSopsTool`,
over `IToolRunner`. `SystemToolRunner` is the real runner:

- the program path is absolute and arguments go one by one, without a shell;
- plaintext, ciphertext, and documents go on standard input and come back on standard output, never in an argument or an
  environment variable; a `ToolRequest` that carries `AGE-SECRET-KEY-` or `AGE-PLUGIN-` text in an argument or environment value is refused;
- the child inherits **no** environment: it gets `LC_ALL=C` and the variables of the request. `sops` gets only
  `SOPS_AGE_KEY_FILE`, set to the identity file's path; `age` and `age-keygen` get nothing;
- standard output is limited to 16 MiB and standard error to 64 KiB (more ends the process), and a run has a two-minute limit;
- nothing is logged. An adapter's failure message holds the tool name, the exit code, and the first error line, cleaned of
  control characters and cut at 200 characters.

`age-keygen` writes the identity file itself with mode 0600; the adapter refuses an existing path (it never overwrites a key),
prints only the public recipient, and fails if the file is readable by others. Every `sops` call passes `--config /dev/null` and
runs in a new empty working directory that is deleted afterwards, because sops reads `.sops.yaml` from its working directory and a
creation rule there (`unencrypted_regex: '.*'`) would otherwise write plaintext. There is no `rotate` or `updatekeys` in the public
API: they rewrite a file in place without a backup, and they return when a command with a preview and an atomic write exists.

## Reading and checking recipients

`SopsMetadataReader` reads the key holders from a file's `sops` block without decrypting and without a YAML library (a
purpose-built reader of the subset SOPS writes). `RecipientVerification.Compare` then checks them against a rule:

- equal sets are `IsExact`; a missing recipient, an unexpected recipient, a non-age key holder (`kms`, `pgp`, `hc_vault`, and
  so on: whoever holds one can read the file), or any `Concerns` entry is not. Concerns are a non-default `unencrypted_*`,
  `encrypted_*`, or `mac_only_encrypted` setting (property names compared without regard to letter case, every YAML boolean spelling), an unknown
  property in the `sops` block, a comment outside it, and values that are not a complete `ENC[AES256_GCM,data:...,iv:...,tag:...,type:...]`
  (keys ending in `_unencrypted`, `null`, empty strings, and empty collections are allowed). Comments inside the `sops` block are refused;
- a recipient listed twice is an error, and each entry's `enc` must be an age file with exactly one recipient stanza of the type its
  label requires (X25519 for `age1...`, and for an SSH recipient the stanza type and key tag derived from the key). This proves the
  labels and the structure, not who can decrypt: the X25519 stanza holds an ephemeral share, never the recipient, so only a trial
  decryption by each holder (or a comparison with a trusted revision of the file: `SopsFileRecipients.Envelopes`) proves a label honest;
- **it fails closed.** A file the reader does not fully understand is an error, never an empty list that would pass for "no
  recipients": a YAML document marker or directive, a byte order mark, a tab indent, a top-level anchor, alias, tag, flow
  collection, or sequence, a top-level key that reads as `sops` without being spelled `sops:` exactly, a repeated key, a second
  `sops:` block, an invalid or duplicated recipient, or an `enc` that is not one recipient stanza.
- Only YAML and JSON files are readable. dotenv, ini, and binary SOPS files are an error.

## What .NET cannot guarantee

`SecretBytes` does not print itself (`ToString` is fixed text), lives in a pinned array that the garbage collector does not
move, and overwrites it on `Dispose`. The runner overwrites its working buffers as it grows them. Beyond that:

- an immutable `string` made from the bytes (`RevealUtf8`) cannot be cleared; neither can anything you pass to a stream or file;
- the child process's memory, the pipes, swap, and core dumps are outside this library;
- a reference you keep after `Dispose` sees zeros, but a copy you made earlier is yours.

Keep decrypted values short-lived, dispose them, and write them only where you mean to.

## Limits

- The process adapters are for Unix-like systems (`sops encrypt` reads `/dev/stdin`).
- Plugin recipients validate here, but the program you run must support them; `sops` support for plugin recipients is its own.
- Reading the key groups of a file with Shamir thresholds lists every holder; it does not judge the threshold.
