# PandaBear.SharedKernel.Secrets.Tool

[![Repository-wide build status](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml)

Packages remain preview (prerelease); stable packages, tags, and releases require separate authorization.

Generates [age](https://age-encryption.org) identities, encrypts and decrypts [SOPS](https://getsops.io) files, and checks that
every encrypted file in a repository is encrypted to exactly the recipients of its creation rule. It is a thin command line over
`PandaBear.SharedKernel.Secrets`. **Secrets are never arguments**: plaintext travels on standard input and output only, and the
programs it runs get no environment except `LC_ALL=C` and, for `sops`, the path in `SOPS_AGE_KEY_FILE`.

## Prerequisites

`age`, `age-keygen`, and `sops` on `PATH` (only absolute `PATH` directories are searched, so a checkout cannot supply them). The
tool installs nothing and adds no dependency; verify and pin those programs where you install them. `verify`, `recipients list`,
and `recipients check` read the files' metadata and start no program.

## Installation

Install a validated candidate as a local tool from a trusted feed. Add the feed to `NuGet.Config`, map
`PandaBear.SharedKernel.*` to it, and map every other package, for example `*`, to the existing source.

```text
dotnet tool install PandaBear.SharedKernel.Secrets.Tool --version <validated-candidate-version>
```

A local tool runs as `dotnet tool run sharedkernel-secrets -- verify`.

## Usage

Changes are previewed unless `--force` is given. `--root <path>` is the repository root and defaults to the current directory;
file arguments are relative to it.

```text
sharedkernel-secrets keygen --output ~/.config/age/work.txt
sharedkernel-secrets keygen --output ~/.config/age/work.txt --force
sharedkernel-secrets encrypt secrets/app.sops.yaml
sharedkernel-secrets encrypt secrets/app.sops.yaml --force
sharedkernel-secrets decrypt secrets/app.sops.yaml --identity ~/.config/age/work.txt
sharedkernel-secrets decrypt secrets/app.sops.yaml#db.password --identity ~/.config/age/work.txt
sharedkernel-secrets decrypt secrets/app.sops.yaml --identity ~/.config/age/work.txt --output plain.yaml --force
sharedkernel-secrets recipients list secrets/app.sops.yaml
sharedkernel-secrets recipients check secrets/app.sops.yaml
sharedkernel-secrets verify
sharedkernel-secrets --help
```

- `keygen` runs `age-keygen`, which writes the identity file with mode 0600, and prints only the public recipient. It never
  overwrites a file. Back the file up; the tool cannot recover a lost key.
- `encrypt` finds the first creation rule that matches the new file, reads the plaintext from standard input (up to 16 MiB),
  encrypts it with `sops` to exactly that rule's recipients, creates the file, and reads it back to check the recipients. It never
  overwrites a file; without `--force` it reads nothing and writes nothing. For example
  `printf 'db:\n  password: ...\n' | sharedkernel-secrets encrypt secrets/app.sops.yaml --force`, with the value coming from a
  file or a password manager rather than your shell history.
- `decrypt` writes the plaintext to standard output exactly as it is, unescaped, with no added newline. `file#key.path` decrypts
  one value (`servers[0].token` indexes a list). The identity is `--identity <path>` or the path in `SOPS_AGE_KEY_FILE`, never the
  key itself; a value that looks like a key is refused. `--output <path>` writes a new file with mode 0600 instead and needs
  `--force`; it never overwrites.
- `recipients list` prints the age recipients of a file and any other key holder (`kms`, `pgp`, ...). `recipients check` compares
  them with the file's creation rule: a missing recipient, an unexpected recipient, and any non-age key holder are differences.
- `verify` checks every `*.sops.*` file under the root that a rule matches. It skips `.git`, `node_modules`, `bin`, `obj`, and
  symbolic links, counts the files that no rule covers, and does not judge them.

Output escapes control characters in file names and tool messages. The plaintext of `decrypt` is data and is not escaped, so
pipe it, do not print it to a shared terminal or a log.

## Configuration

`.config/sharedkernel-secrets.json` is strict JSON with `"version": 1` (comments, trailing commas, and unknown or repeated
properties are errors):

```json
{
  "version": 1,
  "creationRules": [
    {
      "pathRegex": "^secrets/prod/",
      "recipients": ["age1...admin1", "age1...admin2", "age1...ci"],
      "minimumRecipients": 3
    },
    {
      "pathRegex": "^secrets/.*\\.sops\\.ya?ml$",
      "recipients": ["age1...admin1", "age1...admin2"],
      "minimumRecipients": 2
    }
  ]
}
```

- Rules are tried in order and the first match applies, as in SOPS. `pathRegex` is unanchored and compiled with the
  non-backtracking engine and a one-second timeout, so look-around and back-references are refused.
- `recipients` are public age recipients or `ssh-ed25519` / `ssh-rsa` lines without a comment. A private identity is rejected.
- `minimumRecipients` (1 to 64) is yours to set: a rule that lists fewer recipients is an error. The tool hard-codes no policy.
- Errors name the file and the JSON path and never repeat a value.

## Exit codes

| Code | Meaning |
| --- | --- |
| 0 | Success, or a preview that changed nothing. |
| 1 | A check ran and found a file whose recipients are not exactly its rule's recipients, or a command-line parse error. |
| 2 | The result cannot be known or the request was refused: invalid or missing configuration, no matching rule, a missing program, a failed program, `--output` without `--force`, an existing output file, or a covered file that cannot be read. |

## Limits

- Only YAML and JSON SOPS files have readable recipients. A reader that does not fully understand a file reports an error; it
  never treats the file as having no recipients.
- A non-age key holder (`kms`, `pgp`, `hc_vault`) makes a file differ from its rule, because its holder can read the file. The
  tool does not remove them.
- .NET cannot guarantee that no copy of decrypted data remains in memory. The
  [library README](../../src/SharedKernel/SharedKernel.Secrets/README.md#what-net-cannot-guarantee) says what is and is not cleared.
- `rotate` and recipient updates are available in the library (`ISopsTool`) and not in this command line yet.
