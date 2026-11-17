# PandaBear.SharedKernel.Secrets.Age

[![Repository-wide build status](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml)

Packages remain preview (prerelease); stable packages, tags, and releases require separate authorization.

The [age](https://age-encryption.org) provider of
[PandaBear.SharedKernel.Secrets](https://github.com/danigutsch/PandaBear.SharedKernel/blob/main/src/SharedKernel/SharedKernel.Secrets/README.md):
`ProcessAgeTool` implements `IAgeTool` by running the local `age` and `age-keygen` programs. It implements no
cryptography and makes no network request; install and pin the programs yourself.

## Usage

```csharp
using PandaBear.Processes;
using PandaBear.Secrets;
using PandaBear.Secrets.Age;

var age = ProcessAgeTool.Find(new SystemToolRunner(), PathExecutableResolver.Default);
var identity = AgeIdentityRef.Create("/home/me/.config/age/work.txt").Value;
var recipient = (await age.GenerateIdentity(identity, ct)).Value;
```

- `Find` looks for both programs in the absolute directories of `PATH` only, so a checkout cannot supply them, and
  throws `InvalidOperationException` when one is missing. The constructor takes absolute paths instead.
- `GenerateIdentity` runs `age-keygen -o <path>`, which writes the identity file with mode 0600. It refuses an existing
  path, so a key is never overwritten, returns only the public recipient, and fails when the file is readable by others.
- `Encrypt` and `Decrypt` pass recipients and the identity file's path as arguments and the plaintext and ciphertext on
  standard input and output. The child gets no environment variable but `LC_ALL=C`.
- A failure is a `Result` whose message holds the program, the exit code, and the first line of standard error, cleaned
  of control characters and cut at 200 characters. Standard output is never part of a message.

The process rules (no shell, no inherited environment, output limits, a time limit) are those of
`SystemToolRunner` in the core library.
