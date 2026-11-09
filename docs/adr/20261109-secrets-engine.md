# Secrets engine and tool

Date: 2026-11-09. Status: accepted.

Repositories that keep secrets in git encrypt them with [age](https://age-encryption.org) and [SOPS](https://getsops.io).
Three questions recur and are easy to get wrong: is this recipient a public key or a private identity, does this file
open for exactly the people the rule names, and did a secret ever reach an argument, an environment variable, or a log.
`SharedKernel.Secrets` answers them as types and a small set of adapters, and `sharedkernel-secrets` is a thin command line over
it. The infrastructure inventory checks (issues 265 and 266) validate recipient sets of their own; they build on the
primitives here (`AgeRecipientSet`, `RecipientVerification`) instead of repeating them.

## Decision

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
- **A small runner of our own.** `SystemToolRunner` follows `SharedKernel.Packages`' `SystemProcessRunner`, but the secrets
  library must not reference the packages library, and the needs differ: bytes in and out, standard input, no inherited
  environment, and overwritten buffers. A few hundred lines are therefore duplicated in spirit (the limits, the kill-the-tree
  handling, the drain timeout). When a third consumer appears, extract a neutral process-runner library with an options
  object; until then, duplication is the cheaper coupling.
- **Reading recipients without a YAML library.** `sops.age[].recipient` is the only metadata the checks need. A YAML package
  would add a dependency and its transitive packages to a security tool, and would parse far more than we trust. A purpose-built
  reader handles the subset SOPS writes (block mappings and sequences, plain and quoted scalars, block scalars that it skips,
  empty flow collections), converts it to JSON, and reuses the JSON reader. Everything else is an error. The reader fails
  closed: an unreadable file is never an empty recipient list, a repeated key is an error, and a non-age key holder (`kms`,
  `pgp`, `hc_vault`, or any other non-empty holder list) makes a file inexact, because its holder can read the file.
- **No rotate or updatekeys for now.** An earlier draft had `UpdateRecipients` and `Rotate` (`sops rotate -i --add-age --rm-age`). They
  rewrite a file in place without a backup, and no command with a preview, `--force`, and an atomic write existed, so they were
  removed from the public API. They return together with such a command.
- **sops runs with no ambient configuration.** sops reads `.sops.yaml` from its working directory. Every call passes `--config
  /dev/null` and runs in a new empty directory, so a repository file cannot turn on `unencrypted_regex` and make `encrypt` write plaintext.
- **Honest memory handling.** `SecretBytes` is a pinned array, overwritten on dispose, with a fixed `ToString`. .NET cannot
  guarantee that no copy exists (strings, streams, swap, core dumps); the README says so.

## Command line

`sharedkernel-secrets` reads `.config/sharedkernel-secrets.json` (`creationRules`), finds `age`, `age-keygen`, and `sops` on
`PATH` with the shared resolver (absolute `PATH` directories only), and is preview-by-default: `keygen`, `encrypt`, and
`decrypt --output` change nothing without `--force`, and none of them overwrites a file. `decrypt --output` stays inside the root, refuses
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
- **What `verify` reads.** Inside a rule's scope every file is read and anything not positively non-SOPS is a failure; outside every rule only files named `*.sops.*` or with a yaml, yml, json, env, or ini extension are read. A SOPS document with another name and extension outside every rule goes unnoticed. A directory whose children a rule could match is probed with sample child names, so an exotic expression could still hide a link.
- **A race on `decrypt --output`.** The destination is resolved without symbolic links and the file is renamed into place without overwriting, but a parent directory swapped for a link between the check and the rename is not detected. The temporary name is random and created exclusively, and it is removed on any failure, including cancellation.
- **dotenv, ini, and binary files,** which are an error, not a pass.
- **A compromised `age`, `age-keygen`, or `sops` program,** the machine's memory, swap, and core dumps.

Reproduced bypasses that the design now refuses (first review):  a second YAML document, a byte order mark, or a `sops :` / `"sops":` spelling that hid the real
block behind a fake one; a repeated label over two envelopes; a repository `.sops.yaml` with `unencrypted_regex: '.*'`; `verify` passing with
no files, an unjudgeable path, or uncovered files; and an SSH recipient with white space or trailing bytes in its key. Second review: a relabelled X25519 recipient (now caught only by trial decryption or `--base`, and no longer described as verified); `verify` skipping directories named `bin`, `obj`, or `node_modules`, symbolic links, files named `prod.yaml`, `prod.enc.yaml`, `prod.SOPS.yaml`, or `secrets/.sops.yaml`; `mac_only_encrypted: True` or a differently cased JSON name; plaintext in a comment or after `#` on a value; and `ENC[hunter2]` passing as ciphertext. Third review: `--prove-absent-identity` passing for a missing, empty, garbage, unreadable, directory, FIFO, or SSH identity, and for a file sops could not read at all; sops falling back to keys in the caller's home directory; `verify` skipping one-line JSON, indented or escaped YAML, UTF-16, and extensionless JSON binary stores in scope; symbolic links outside a rule's own name; a FIFO hanging `verify`; and `--base` with a root in a subdirectory comparing a file with a top-level twin.

## Deferred

- Extracting the vault manager's Bech32, redaction, and PEM helpers (issue 335) onto this library's Bech32.
- A neutral process-runner library shared with `SharedKernel.Packages`.
- dotenv, ini, and binary SOPS formats; SOPS key groups with thresholds beyond listing their holders.
- Plugin-specific behaviour (a plugin identity is a file path here; the programs do the rest).
