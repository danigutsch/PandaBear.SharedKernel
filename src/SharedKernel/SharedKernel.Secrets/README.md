# PandaBear.SharedKernel.Secrets

An engine for secrets that live in git as [age](https://age-encryption.org) and [SOPS](https://getsops.io) files:
validated public recipients, references to identity files (never keys), creation rules, a SOPS metadata reader that fails
closed, repository verification, and `SecretsRepository`, which runs the operations of `dotnet secrets` over two seams,
`IAgeTool` and `ISopsTool`. The providers that run the local programs are separate packages:
[PandaBear.SharedKernel.Secrets.Age](https://github.com/danigutsch/PandaBear.SharedKernel/blob/main/src/SharedKernel/SharedKernel.Secrets.Age/README.md)
and [PandaBear.SharedKernel.Secrets.Sops](https://github.com/danigutsch/PandaBear.SharedKernel/blob/main/src/SharedKernel/SharedKernel.Secrets.Sops/README.md).
It has no NuGet dependency beyond other SharedKernel libraries, makes no network request, and implements no cryptography:
the programs do.

```csharp
using SharedKernel.Processes;
using SharedKernel.Secrets;
using SharedKernel.Secrets.Age;
using SharedKernel.Secrets.Sops;

var runner = new SystemToolRunner();
var repository = new SecretsRepository("/repo", new SecretsPrograms
{
    Runner = runner,
    ExecutableResolver = PathExecutableResolver.Default,
    Age = () => ProcessAgeTool.Find(runner, PathExecutableResolver.Default),
    Sops = () => ProcessSopsTool.Find(runner, PathExecutableResolver.Default),
});

// 0 when every SOPS file matches its rule, 1 for a difference, 2 when the result cannot be known.
var exitCode = await repository.Verify(new VerifySettings { BaseRevision = "origin/main" }, Console.Out, Console.Error, ct);

// The value objects and the reader work on their own, without a program.
var rules = await SecretsConfigurationFile.Read("/repo", ct);
var path = EncryptedFilePath.Create("secrets/app.sops.yaml").Value;
var holders = (await SopsFileReader.ReadRecipients("/repo/secrets/app.sops.yaml", ct)).Value;
var check = RecipientVerification.Compare(holders, rules.FirstMatch(path)!);
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
requires. It comes from [PandaBear.SharedKernel.Encodings](https://github.com/danigutsch/PandaBear.SharedKernel/blob/main/src/SharedKernel/SharedKernel.Encodings/README.md),
whose tests check it against the BIP-173 vectors and reject the Bech32m vectors.

## Repository operations

`SecretsRepository` takes the root and the programs, and each operation takes a settings record, writes what it reports with
control characters escaped, writes a refusal or failure to the error writer, and returns `SecretsExitCodes.Success` (0),
`Differences` (1), or `Failure` (2). Plaintext travels only through the streams the settings open.

- `CreateIdentity`, `Encrypt`, and `Decrypt` with `OutputPath` preview without `Force`, and never overwrite a file.
- `ListRecipients` and `CheckRecipients` read metadata; `CheckRecipients` proves identities by a trial decryption.
- `UpdateRecipients` (keep the data key) and `Rotate` (a new data key) make a file's recipients exactly its rule's, and preview
  without `Force`. `Edit` lets a person edit the document through `sops edit` in a new owner-only directory under
  `EditSettings.TemporaryRoot`, which must be on a tmpfs or ramfs, and removes it afterwards. All three refuse a weakened
  file, check that the new version carries exactly the rule's recipients with nothing weakened, and replace the file only when
  it still holds the bytes it was read with: on Linux by swapping names with a flushed temporary file in one step and undoing
  the swap if another writer got there first, elsewhere by a comparison just before a rename. The directory is flushed to disk.
- `Verify` judges every file named `*.sops.*` against its rule, rejects every other file in a rule's scope, and with a base
  revision reports each label whose `enc` changed.

`SecretsConfigurationFile` reads `.config/sharedkernel-secrets.json` with `SharedKernel.ConfigurationFiles.JsonConfigurationFile`.
The [tool README](https://github.com/danigutsch/PandaBear.SharedKernel/blob/main/tools/SharedKernel.Secrets.Tool/README.md) describes the
format and each command's exact behavior.

## Seams and the runner

`IAgeTool` (`GenerateIdentity`, `Encrypt`, `Decrypt`) and `ISopsTool` (`Encrypt`, `Decrypt`, `DecryptValue`, `UpdateKeys`,
`Rotate`, `Edit`, `TrialDecrypt`, `ListRecipients`, `VerifyRecipients`) are what the providers implement over `IToolRunner`.
`UpdateKeys`, `Rotate`, and `Edit` take the encrypted document the caller read, work on a private copy, and return the new
encrypted document; they never read or write the file. `ToolRuns.RunToSuccess` turns a run into a result whose error is one
line without the program's output, and `ToolRuns.CheckFileArgument` checks a path before it becomes an argument. `SystemToolRunner` is the real
runner:

- the program path is absolute and arguments go one by one, without a shell;
- plaintext, ciphertext, and documents go on standard input and come back on standard output, never in an argument or an
  environment variable; a `ToolRequest` that carries `AGE-SECRET-KEY-` or `AGE-PLUGIN-` text in an argument or environment value is refused;
- the child inherits **no** environment: it gets `LC_ALL=C` and the variables of the request, except a forge token (`GH_TOKEN`,
  `GITHUB_TOKEN`, `FORGEJO_TOKEN`, and the GitHub Enterprise tokens), which `ProcessEnvironment.RemoveForgeTokens` removes even when a
  request names it;
- standard output is limited to 16 MiB and standard error to 64 KiB (more ends the process), a run has a two-minute limit, and
  `ProcessTree` ends the whole process tree on a failure or cancellation;
- `RunAttached` runs a child on the caller's terminal with no time limit and without forcing `LC_ALL`, for the editor;
- nothing is logged. `ToolResult.Describe` is the failure line the providers use: the tool name, the exit code, and the first
  error line, cleaned of control characters and cut at 200 characters.

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

- The sops provider is for Unix-like systems (`sops encrypt` reads `/dev/stdin`), and `Edit` needs a tmpfs or ramfs.
- Plugin recipients validate here, but the program you run must support them; `sops` support for plugin recipients is its own.
- Reading the key groups of a file with Shamir thresholds lists every holder; it does not judge the threshold.
