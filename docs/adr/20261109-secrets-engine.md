# Secrets engine and tool

Date: 2026-11-09, amended 2026-11-17. Status: accepted.

Repositories that keep secrets in git encrypt them with [age](https://age-encryption.org) and [SOPS](https://getsops.io).
Three questions recur and are easy to get wrong: is this recipient a public key or a private identity, does this file
open for exactly the people the rule names, and did a secret ever reach an argument, an environment variable, or a log.
`SharedKernel.Secrets` answers them as types, a metadata reader, and the operations of a repository's secrets;
`SharedKernel.Secrets.Age` and `SharedKernel.Secrets.Sops` run the programs; and `sharedkernel-secrets` is a thin command line
over them, as the [tool engines ADR](20261115-tool-engines-and-thin-wrappers.md) lays out. The infrastructure inventory checks (issues 265 and 266) validate recipient sets of their own; they build on the
primitives here (`AgeRecipientSet`, `RecipientVerification`) instead of repeating them.

## Decision

- **A core and two providers.** The core holds what needs no program: the value objects, the SOPS metadata reader, the
  configuration file, repository verification, the byte runner, and `SecretsRepository`, whose operations take settings
  records and return exit codes. It defines the `IAgeTool` and `ISopsTool` seams. `ProcessAgeTool` and `ProcessSopsTool` are
  the providers, one library each, so a consumer that only verifies references neither. They share no internals with the core;
  `ToolResult.Describe` is the public failure line both use.

- **No cryptography and no new dependency.** The engine runs the `age`, `age-keygen`, and `sops` programs. It adds no NuGet
  package, direct or transitive, beyond the SharedKernel libraries it already references. The programs themselves are a
  supply-chain concern of the machine, not of the package: pin and verify them where you install them.
- **Value objects with Result factories**, errors that never echo the input. `AgeIdentityRef` is the path of an identity
  file and refuses text that looks like `AGE-SECRET-KEY-1...` or `AGE-PLUGIN-...` with a message that says to pass a file.
  `ToolRequest` independently refuses the same text in an argument or an environment value, so a mistake in an adapter
  cannot put key material on a command line.
- **Bech32, not Bech32m, with no length limit.** The age specification (C2SP) says recipients and identities are Bech32 as BIP-173
  defines it "but without length limits on the data part". The implementation is checked against the BIP-173 vectors,
  rejects the Bech32m vectors, and accepts the 91-character vector that BIP-173 itself rejects for length.
- **Allow-listed recipient kinds.** `age`, `age1pq`, `age1tag`, `age1tagpq`, and the plugin parts `age1yubikey`, `age1tpm`,
  `age1se`, `age1fido2-hmac`. Another valid Bech32 string is refused, because an unknown part is more likely a typo or a
  private identity of another tool than a recipient. SSH recipient lines `ssh-ed25519` and `ssh-rsa` are accepted (age and sops
  encrypt to them) but without a comment, so that equal keys compare equal.
- **Rule minimums are parameters.** A `SopsCreationRule` carries `minimumRecipients` (1 to 64) and cannot be built with fewer
  recipients. Two admins are a policy of the consuming repository, not of the engine. Rule expressions use the non-backtracking
  engine with a timeout, as the other tools' configuration does.
- **Stdin and stdout only.** Plaintext, ciphertext, and documents travel on standard input and output. The only
  environment variable a child gets is `SOPS_AGE_KEY_FILE`, a path; it inherits nothing else (not even `PATH`, because paths
  are absolute), plus `LC_ALL=C`. Output is bounded (16 MiB, 64 KiB for errors), runs are time-limited, and nothing is logged.
  Failure messages keep one cleaned error line.
- **A byte runner on the shared process library.** `SharedKernel.Processes` runs a child to completion and returns its
  output as strings, which cannot be cleared. Secrets need bytes in and out, standard input, no inherited environment, and
  overwritten buffers, so `SystemToolRunner` stays, but it starts and ends children with `ProcessTree` and removes every forge
  token with `ProcessEnvironment.RemoveForgeTokens`, even one a request names. `RunAttached` runs a child on the caller's
  terminal for the editor, with the same environment rules and no forced locale.
- **Reading recipients without a YAML library.** `sops.age[].recipient` is the only metadata the checks need. A YAML package
  would add a dependency and its transitive packages to a security tool, and would parse far more than we trust. A purpose-built
  reader handles the subset SOPS writes (block mappings and sequences, plain and quoted scalars, block scalars that it skips,
  empty flow collections), converts it to JSON, and reuses the JSON reader. Everything else is an error. The reader fails
  closed: an unreadable file is never an empty recipient list, a repeated key is an error, and a non-age key holder (`kms`,
  `pgp`, `hc_vault`, or any other non-empty holder list) makes a file inexact, because its holder can read the file.
- **New versions are checked and replaced atomically.** `ISopsTool.UpdateKeys`, `Rotate`, and `Edit` return the new encrypted
  document and never write the file. `SecretsRepository` refuses a weakened file (sops would keep the weakening), checks that the
  new version carries exactly the rule's recipients with nothing weakened, and replaces the file through a temporary file and a
  rename only when it still holds the bytes it was read with. `updatekeys` takes recipients only from a configuration file and
  rewrites the file it is given, so it runs on a private copy with a configuration that lists exactly the rule's recipients.
  `rotate` adds and removes only the difference with `--add-age` and `--rm-age` and writes to standard output.
- **Changing recipients means changing the rule.** There is no command that edits the configuration. A consumer changes a
  rule, then `recipients update` re-wraps the data key for it. Because a removed holder may already know the data key,
  `recipients update` suggests `rotate`, which replaces the data key.
- **Edit only in memory.** `sops edit` keeps the data key, so labels do not change and a review against a base shows nothing to
  review, and it decrypts to a file under `TMPDIR` that the editor opens. `edit` makes that a new owner-only directory under
  `XDG_RUNTIME_DIR`, or `/dev/shm`, and refuses unless it is on a tmpfs or ramfs, so plaintext never reaches a disk except
  through swap. The directory is also the child's `HOME`, so an editor's swap, backup, and history files land in it, and it is
  removed afterwards. The editor comes from `VISUAL` or `EDITOR`, resolved like every other program, and gets only terminal,
  locale, display, and absolute `PATH` variables.
- **sops runs with no ambient configuration.** sops reads `.sops.yaml` from its working directory. Every call passes `--config
  /dev/null` and runs in a new empty directory, so a repository file cannot turn on `unencrypted_regex` and make `encrypt` write plaintext.
- **Honest memory handling.** `SecretBytes` is a pinned array, overwritten on dispose, with a fixed `ToString`. .NET cannot
  guarantee that no copy exists (strings, streams, swap, core dumps); the README says so.

## Command line

`sharedkernel-secrets` reads `.config/sharedkernel-secrets.json` (`creationRules`), finds `age`, `age-keygen`, and `sops` on
`PATH` with the shared resolver (absolute `PATH` directories only), and is preview-by-default: `keygen`, `encrypt`,
`decrypt --output`, `recipients update`, and `rotate` change nothing without `--force`; `keygen`, `encrypt`, and `decrypt --output`
never overwrite a file; and `recipients update`, `rotate`, and `edit` replace a file only through the checked, atomic replacement
above. `edit` is interactive, so it needs a terminal instead of `--force`. `decrypt --output` stays inside the root, refuses
symbolic links, and renames a 0600 temporary file into place. `verify` never passes on nothing: no covered file, a path it cannot judge, or a
file no rule covers is not success (`--allow-unverified-files` names the risk for the last). `decrypt` writes to standard output unless `--output` and `--force`
are both given. Secrets are never arguments. Exit codes: 0 success or a preview, 1 a check found differences, 2 the result
cannot be known or the request was refused.

## Threat model and limits

The checks read file metadata. They are meant to catch a repository whose encrypted files are readable by more people, or by different
people, than the creation rule says, and a file that was never really encrypted. The attacker modelled can edit files in the repository
(a pull request) but is not a holder of an allowed key.

What the checks establish for a YAML or JSON file: there is exactly one `sops` block and the reader saw all of it; each listed recipient
is valid and listed once; each entry's `enc` is an age file with exactly one recipient stanza of the type the label requires, and for SSH
recipients with the tag derived from that key; no other key type (`kms`, `pgp`, `hc_vault`, ...) holds a key; no non-default
`unencrypted_*`, `encrypted_*`, or `mac_only_encrypted` setting is present; and every value is `ENC[...]`, `null`, an empty string or
collection, or under a key that ends in `_unencrypted`.

What they do not establish:

- **Who can decrypt.** This is the lesson of the second review. sops never checks labels, and an X25519 stanza holds an ephemeral share and
  a wrapped key, never the recipient, so a label cannot be bound to a stanza by any static check. Encrypt to X, rewrite the label to the
  allowed A, and the file is structurally perfect while X decrypts. For that reason a pass is worded `labels consistent`, never
  "verified", and every run says it once. A label is proven honest only by **trial decryption** by its holder (`recipients check
  --prove-with-identity`, and `--prove-absent-identity` for a key that must be revoked) or by **review against a trusted revision**
  (`verify --base`): only someone who already holds the data key can produce a new valid `enc`, so a label whose `enc` is new or changed
  needs a human, and an unchanged one cannot have been forged since the base. The base check trusts the base revision; a rotation changes
  every `enc` and needs `--allow-changed-recipients` after review. A failed trial decryption is a verdict only when sops says the identity
  matches none of the recipients; every other failure is exit 2, because sops prints the same first line ("Failed to get the data key") for a
  wrong identity, a garbage or missing identity file, and a corrupted `enc`. So identities must first be readable regular files of age secret keys,
  SSH and plugin identities are refused (an SSH key that is still a recipient would decrypt), and the absent proof requires a decrypting holder
  in the same run. sops runs with `HOME` set to its empty working directory, because it otherwise tries `~/.ssh/id_*` and the caller's key file.
- **That the data key is wrapped for the label's key,** and that the MAC and the ciphertext are intact. Nothing here decrypts or verifies a MAC.
- **Plugin, hybrid, and tag recipients** beyond "exactly one stanza".
- **Keys named `*_unencrypted`,** which sops leaves in clear on purpose. Comments outside the `sops` block count as a concern, not a pass, because nothing checks them; comments inside it are refused.
- **Settings spelled in ways a consumer reads differently.** Property names are compared without regard to letter case, as Go's JSON decoder does, every YAML boolean spelling counts, and an unknown property in the `sops` block is a concern; another parser could still disagree on something not listed.
- **Nothing in scope is skipped by content.** Content inspection cannot be made airtight: the fourth, fifth, and sixth reviews each found a spelling go-yaml reads that the detector did not (a complex key, a tag, an anchor, a trailing tab, a backslash before U+2028, and finally `!!binary c29wcw==:`, a base64-encoded `sops` key with base64 `ENC[...]` values, none of whose letters appear in the file). So `verify` uses a structural rule: inside the scope of any creation rule every regular file must be named `*.sops.*`, or the run is exit 2 and lists it as not judged; there is no sniffing in scope and no ignore list. The price is that a `.gitkeep`, a README, or any non-secret file in a scoped directory must be renamed or moved out. Files named `*.sops.*` are judged by the metadata reader, which fails closed, and which now refuses every YAML tag and `%` directive anywhere in the file because go-yaml decodes tagged scalars. The content heuristic that remains (token search after unescaping, normalisation, and separator removal) only serves outside every rule, where it lists files that look like SOPS as uncovered on a best-effort basis; it is incomplete by nature. Recommended: let rule scope directories hold only sops-named files, and have CI also run `recipients check --prove-with-identity` for a current holder and `--prove-absent-identity` for each retired key.
- **What `verify` reads.** Inside a rule's scope only files named `*.sops.*` are read (everything else is rejected by name); outside every rule only files named `*.sops.*` or with a yaml, yml, json, env, or ini extension are read, by a best-effort heuristic. A SOPS document with another name and extension outside every rule goes unnoticed. A directory whose children a rule could match is probed with sample child names, so an exotic expression could still hide a link.
- **A race on `decrypt --output`.** The destination is resolved without symbolic links and the file is renamed into place without overwriting, but a parent directory swapped for a link between the check and the rename is not detected. The temporary name is random and created exclusively, and it is removed on any failure, including cancellation.
- **A race on a replacement.** The file is compared with the bytes it was read with just before the rename, so a change during a
  long edit is refused, but a change between that comparison and the rename is lost. The new version's data is flushed to
  disk before the rename, but the directory is not, so a crash right after the rename can leave the previous version. The
  sops provider reads the file again to copy it or to list its recipients; a file swapped for a FIFO between the two reads
  can still block that copy.
- **Editors that leave the private directory.** An editor that hands the file to an already running instance (`code --wait`,
  `emacsclient`) runs outside the private `HOME` and `TMPDIR`, and that instance may keep history, backups, or caches of the
  plaintext elsewhere. A tmpfs can be written to swap. Neither is detectable here.
- **dotenv, ini, and binary files,** which are an error, not a pass.
- **A compromised `age`, `age-keygen`, or `sops` program,** the machine's memory, swap, and core dumps.

Reproduced bypasses that the design now refuses (first review):  a second YAML document, a byte order mark, or a `sops :` / `"sops":` spelling that hid the real
block behind a fake one; a repeated label over two envelopes; a repository `.sops.yaml` with `unencrypted_regex: '.*'`; `verify` passing with
no files, an unjudgeable path, or uncovered files; and an SSH recipient with white space or trailing bytes in its key. Second review: a relabelled X25519 recipient (now caught only by trial decryption or `--base`, and no longer described as verified); `verify` skipping directories named `bin`, `obj`, or `node_modules`, symbolic links, files named `prod.yaml`, `prod.enc.yaml`, `prod.SOPS.yaml`, or `secrets/.sops.yaml`; `mac_only_encrypted: True` or a differently cased JSON name; plaintext in a comment or after `#` on a value; and `ENC[hunter2]` passing as ciphertext. Third review: `--prove-absent-identity` passing for a missing, empty, garbage, unreadable, directory, FIFO, or SSH identity, and for a file sops could not read at all; sops falling back to keys in the caller's home directory; `verify` skipping one-line JSON, indented or escaped YAML, UTF-16, and extensionless JSON binary stores in scope; symbolic links outside a rule's own name; a FIFO hanging `verify`; and `--base` with a root in a subdirectory comparing a file with a top-level twin. Fourth review: in-scope files that a line heuristic called not SOPS (`? sops`, `!!str sops:`, `&x sops:`, `sops:` plus a tab, flow YAML behind a comment or `---`, an indented INI `[sops]`). Fifth review: a double-quoted scalar `"so\<U+2028>ps"` that go-yaml reads as `sops` while the decoder knew only line feed and carriage return as breaks. Sixth review: `!!binary c29wcw==:` (and `!<tag:yaml.org,2002:binary>`, and a `%TAG` handle spelled `!x!n%61ry`), whose base64 key and values contain neither `sops` nor `ENC[` in the file text.

## Deferred

- Extracting the vault manager's Bech32, redaction, and PEM helpers (issue 335) onto this library's Bech32.
- Byte input and output in `SharedKernel.Processes`, once a second engine needs it, so `SystemToolRunner` can move there.
- An `edit` for systems without a memory-backed file system, such as Windows and macOS.
- dotenv, ini, and binary SOPS formats; SOPS key groups with thresholds beyond listing their holders.
- Plugin-specific behaviour (a plugin identity is a file path here; the programs do the rest).
