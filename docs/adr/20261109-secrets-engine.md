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
- **`updatekeys` without a config file.** `sops updatekeys` needs a `.sops.yaml`. `UpdateRecipients` instead computes the
  difference against the rule and runs `sops rotate -i --add-age --rm-age`, then reads the file again and fails unless it is
  exact. The command rotates the data key and rewrites the file in place, so the CLI previews by default.
- **Honest memory handling.** `SecretBytes` is a pinned array, overwritten on dispose, with a fixed `ToString`. .NET cannot
  guarantee that no copy exists (strings, streams, swap, core dumps); the README says so.

## Command line

`sharedkernel-secrets` reads `.config/sharedkernel-secrets.json` (`creationRules`), finds `age`, `age-keygen`, and `sops` on
`PATH` with the shared resolver (absolute `PATH` directories only), and is preview-by-default: `keygen`, `encrypt`, and
`decrypt --output` change nothing without `--force`. `decrypt` writes to standard output unless `--output` and `--force`
are both given. Secrets are never arguments. Exit codes: 0 success or a preview, 1 a check found differences, 2 the result
cannot be known or the request was refused.

## Deferred

- Extracting the vault manager's Bech32, redaction, and PEM helpers (issue 335) onto this library's Bech32.
- A neutral process-runner library shared with `SharedKernel.Packages`.
- dotenv, ini, and binary SOPS formats; SOPS key groups with thresholds beyond listing their holders.
- Plugin-specific behaviour (a plugin identity is a file path here; the programs do the rest).
