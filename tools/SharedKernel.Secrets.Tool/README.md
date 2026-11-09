# PandaBear.SharedKernel.Secrets.Tool

[![Repository-wide build status](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml)

Packages remain preview (prerelease); stable packages, tags, and releases require separate authorization.

Generates [age](https://age-encryption.org) identities, encrypts and decrypts [SOPS](https://getsops.io) files, and checks that
every encrypted file in a repository is encrypted to exactly the recipients of its creation rule. It is a thin command line over
`PandaBear.SharedKernel.Secrets`. **Secrets are never arguments**: plaintext travels on standard input and output only, and the
programs it runs get no environment except `LC_ALL=C` and, for `sops`, the path in `SOPS_AGE_KEY_FILE`. Every `sops` call passes `--config /dev/null` and starts in an empty directory, so a `.sops.yaml` in the repository cannot change how data is encrypted.

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
sharedkernel-secrets recipients check secrets/app.sops.yaml --prove-with-identity ~/.config/age/work.txt
sharedkernel-secrets recipients check secrets/app.sops.yaml --prove-with-identity ~/.config/age/work.txt --prove-absent-identity ~/.config/age/revoked.txt
sharedkernel-secrets verify
sharedkernel-secrets verify --base origin/main
sharedkernel-secrets verify --base origin/main --allow-changed-recipients
sharedkernel-secrets verify --allow-unverified-files
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
  key itself; a value that looks like a key is refused. `--output <path>` writes a new file instead and needs `--force`. The
  path is relative to the root (no `..`, no symbolic link), its directory must exist, and the file is created with a random name and
  mode 0600 next to its destination, exclusively, and renamed into place, so a failure leaves no partial plaintext. It never overwrites.
- `recipients list` prints the age recipients of a file, any other key holder (`kms`, `pgp`, ...), and concerns.
- `recipients check` compares a file's **labels** with its creation rule and prints `labels consistent` or the differences: a missing
  recipient, an unexpected recipient, a non-age key holder, or a **weakening**. A weakening is a non-default `unencrypted_regex`,
  `encrypted_regex`, `unencrypted_suffix`, `encrypted_suffix`, comment regex, or `mac_only_encrypted` setting (any YAML boolean
  spelling, any JSON letter case), an unknown property in the `sops` block, a comment outside it (comments are not covered by the
  checks), or a value that is not a complete `ENC[AES256_GCM,data:...,iv:...,tag:...,type:...]`. Keys that end with `_unencrypted`,
  `null`, empty strings, and empty lists and mappings are allowed, as sops leaves them in clear. Every run says once that
  it **proves labels and structure, not who can decrypt**.
- `recipients check --prove-with-identity <file>` adds the proof that labels cannot give: sops tries to decrypt the file with that
  identity file, in an empty directory with no configuration and with `HOME` pointing at that empty directory (so no key in the
  caller's `~/.ssh` or `~/.config/sops` can decrypt instead), and the plaintext is discarded unread. It reports `can decrypt` or
  `cannot decrypt` (exit 1). `--prove-absent-identity <file>` is the negative check for a revoked key: it must **not** decrypt
  (`CAN decrypt but must not` is exit 1). It only means something next to a `--prove-with-identity` that decrypts, so it is refused
  alone and skipped when no holder decrypted. Both can be repeated.
  - Every identity must first be a **readable regular file of age secret keys** (`AGE-SECRET-KEY-1...` lines and comments). A missing,
    empty, garbage, unreadable, or comment-only file, a directory, a FIFO or device (which would block; reads time out after five
    seconds), an SSH private key, or a plugin identity is exit 2 before any trial. SSH identities cannot be proven absent: an SSH key
    that is still a recipient would decrypt, so the tool refuses rather than guess.
  - Only two outcomes are verdicts about an identity: success, and sops saying the identity matches none of the file's recipients.
    Every other failure (a bad MAC, a parse error, a corrupted `enc`, a missing file, an unreadable identity, a timeout) is exit 2 and
    says `file could not be decrypted (not an identity verdict)`. A corrupted file therefore never reads as "this holder cannot decrypt".
- `verify` judges **only files named `*.sops.*`** (any letter case), and it rejects everything else in a rule's scope. This is a structural
  rule with no content inspection: inside the scope of any creation rule (the paths its expression matches), every regular file must be named
  `*.sops.<ext>`, or the run is exit 2 and lists the file as `not judged: name does not mark it as SOPS-encrypted (rename it to *.sops.<ext>, or
  move it out of the rule's scope)`. There is no content sniffing in scope and no ignore list, so nothing in scope is skipped because of what
  it contains (content tricks such as `!!binary c29wcw==:`, which go-yaml decodes to the key `sops`, cannot hide a file). A consequence is that
  `.gitkeep`, a README, or a non-secret file in a scoped directory fails the run: rename it, or move it out of the rule's scope. A file named
  `*.sops.*` is read (at most 16 MiB; a FIFO or device times out) and judged by the metadata reader, which fails closed (exit 2) on anything it
  does not fully understand: tags (`!`...) and `%` directives anywhere in YAML, complex keys, anchors, flow style, escaped keys, indentation, UTF-16,
  a second document, and more. It skips `.git`, the root `.sops.yaml` (the sops configuration), and nothing else by name: `bin`, `obj`, and
  `node_modules` are checked. A link, an unreadable directory, and a path with a control character, `:`, or `\` in scope are exit 2. It never passes on nothing: no
  covered file is exit 2. **Outside every rule the detection is best effort:** a file named `*.sops.*`, or a yaml, yml, json, env, or ini file that a
  heuristic recognises as SOPS, is listed as uncovered (exit 1, unless `--allow-unverified-files`, whose name says what you accept: nobody checks who can read those files). The heuristic
  cannot be complete, so keep SOPS files inside a rule's scope. A link where a rule applies, or whose children a rule could match, is exit 2; a link outside every rule that is named like a SOPS file is listed as uncovered.
- **The guarantee, precisely:** `verify` judges only files named `*.sops.*`; every other file in a rule's scope is rejected, so nothing in scope is skipped by content inspection. Content detection outside scope is best effort.
- `verify --base <rev>` is the strongest check that needs no key. For every judged file it reads the same file at that git revision
  (git by absolute path, arguments one by one, no shell, no user configuration; the revision cannot start with `-` or hold `:` or
  spaces) and compares each label's `enc` text. A label that is new, or whose `enc` differs, is `changed: needs review` (exit 1),
  because only someone holding the data key can legitimately produce a new `enc`, and a forged label has to. Paths are resolved from
  `--root` in both the working tree and the revision (`rev:./path`), so a root in a subdirectory of the repository compares each file with its own
  twin at that revision, never with a file of the same name at the top level; a file missing at the revision is new, and every label of it is changed. An unchanged label
  passes. `--allow-changed-recipients` accepts changes for a reviewed change, such as a deliberate rotation, which legitimately changes every `enc`.

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
| 0 | Success (for a check: labels consistent, and any requested proof or base comparison passed), or a preview that changed nothing. |
| 1 | A check ran and found a file whose recipients are not exactly its rule's recipients, a weakened file, a failed proof, or (for `verify`) a file that no rule covers or a label that changed since `--base`; or a command-line parse error. |
| 2 | The result cannot be known or the request was refused: invalid or missing configuration, no matching rule, a missing program, a failed program, `--output` without `--force` or outside the root, an existing output file, `verify` with nothing to verify, a file, link, or directory that cannot be read, understood, or judged, an unusable `--base`, or `--prove-absent-identity` without `--prove-with-identity`. |

## Limits

- Only YAML and JSON SOPS files have readable recipients. A reader that does not fully understand a file reports an error; it
  never treats the file as having no recipients. It refuses a YAML file with a second document, a byte order mark, a tab indent,
  a top-level anchor, alias, tag, or flow collection, or a top-level key that reads as `sops` without being spelled `sops:`.
- **Labels are not proof.** An X25519 recipient label cannot be bound to its stanza without a private key: encrypt to X, rewrite the
  label to A, and every static check still reports `labels consistent` while X decrypts. Only a trial decryption by each holder
  (`--prove-with-identity`, `--prove-absent-identity`) or a review of what changed since a trusted revision (`--base`) closes that gap.
  See the [ADR](../../docs/adr/20261109-secrets-engine.md#threat-model-and-limits).
- A non-age key holder (`kms`, `pgp`, `hc_vault`) makes a file differ from its rule, because its holder can read the file. The
  tool does not remove them.
- .NET cannot guarantee that no copy of decrypted data remains in memory. The
  [library README](../../src/SharedKernel/SharedKernel.Secrets/README.md#what-net-cannot-guarantee) says what is and is not cleared.
- Rotating keys and changing recipients are not offered: they rewrite files in place, and the tool has no command with a preview and an atomic write for them yet.
