# PandaBear.SharedKernel.Secrets.Sops

[![Repository-wide build status](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml)

Packages remain preview (prerelease); stable packages, tags, and releases require separate authorization.

The [SOPS](https://getsops.io) provider of
[PandaBear.SharedKernel.Secrets](https://github.com/danigutsch/PandaBear.SharedKernel/blob/main/src/SharedKernel/SharedKernel.Secrets/README.md):
`ProcessSopsTool` implements `ISopsTool` by running the local `sops` program with age keys. It implements no
cryptography and makes no network request; install and pin `sops` yourself.

## Usage

```csharp
using PandaBear.Processes;
using PandaBear.Secrets;
using PandaBear.Secrets.Sops;

var sops = ProcessSopsTool.Find(new SystemToolRunner(), PathExecutableResolver.Default);
var identity = AgeIdentityRef.Create("/home/me/.config/age/work.txt").Value;
using var password = (await sops.DecryptValue("/repo", SecretRef.Create("secrets/app.sops.yaml#db.password").Value, identity, ct)).Value;
```

- `Find` looks for `sops` in the absolute directories of `PATH` only and throws `InvalidOperationException` when it is
  missing. The constructor takes an absolute path instead.
- Every call passes `--config /dev/null` and starts in a new empty directory that is also `HOME` and is deleted
  afterwards. `sops` reads `.sops.yaml` from its working directory, and a creation rule there
  (`unencrypted_regex: '.*'`) would otherwise write plaintext; with `HOME` empty, no key in the caller's `~/.ssh` or
  `~/.config/sops` can decrypt in place of the identity that was given.
- The identity is the path in `SOPS_AGE_KEY_FILE`, the only variable the child gets besides `HOME` and `LC_ALL=C`.
  Plaintext goes in on standard input (`sops encrypt` reads `/dev/stdin`) and comes out on standard output.
- `TrialDecrypt` discards the plaintext unread. Only sops reporting that the identity matches none of the recipients is
  `NoMatchingIdentity`; every other failure, such as a bad MAC or an unreadable identity, is `Failed`, so a broken file
  is never mistaken for an identity that cannot decrypt.
- `UpdateKeys`, `Rotate`, and `Edit` take the encrypted document as bytes, never the file: sops works on an owner-only copy
  named with a lowercase extension, in a directory that is removed afterwards, and the type is passed with `--input-type` and
  `--output-type`. `UpdateKeys` runs `sops updatekeys` with a configuration that lists exactly the recipients; `Rotate` runs
  `sops rotate --add-age --rm-age` for the difference; `Edit` runs `sops edit` on the caller's terminal inside the session's
  private directory. Each returns the new document for the caller to check and write.
- `ListRecipients` and `VerifyRecipients` read the file's metadata with the core library's `SopsFileReader` and start
  no process.

## Limits

- The adapter is for Unix-like systems: it reads plaintext from `/dev/stdin` and passes `/dev/null` as configuration.
- Only age recipients are written. A file with another key holder (`kms`, `pgp`, `hc_vault`) is read, never created.
