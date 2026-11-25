# PandaBear.SharedKernel.CommandLine

[![Repository-wide build status](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml)

Packages remain preview (prerelease); stable packages, tags, and releases require separate authorization.

The [System.CommandLine](https://learn.microsoft.com/dotnet/standard/commandline/) options that the SharedKernel
command-line tools share, so that each one validates its value the same way and reports the same messages in every tool.
References `System.CommandLine`, `PandaBear.SharedKernel.Forgejo`, and
`PandaBear.SharedKernel.InputNormalization`, and is trimming- and NativeAOT-compatible.

```csharp
using System.CommandLine;
using SharedKernel.CommandLine;

var root = new RootOption(Environment.CurrentDirectory);
var repository = new RepositoryOption("GitHub repository whose labels are synchronized.") { Required = true };
var forgejo = new ForgejoOption("Forgejo instance that hosts --repository instead of GitHub.");
var command = new Command("sync-labels") { root, repository, forgejo };
command.SetAction(parseResult =>
{
    var directory = root.GetExistingDirectory(parseResult);
    var server = parseResult.GetValue(forgejo);
    return 0;
});
```

- `RootOption` is `--root <path>`, the repository root. Without the option the root is the working directory that the
  constructor takes, which must be absolute. A relative value resolves against that working directory. An empty or
  white-space value is a parse error (`--root requires a directory path.`) instead of the working directory, so an
  unset shell variable cannot select it, and so is a value with a null character from a response file.
  `GetDirectory(parseResult)` returns the absolute root, which need not exist, for a command that creates it.
  `GetExistingDirectory(parseResult)` also requires an existing directory and otherwise throws a
  `DirectoryNotFoundException` whose message is `--root is not an existing directory: <path>`, with control characters
  escaped. The check runs when the command runs, not when it parses, so a documented command line with a placeholder
  root still parses.
- `RepositoryOption` is `--repository <owner/name>`. The value becomes part of a request address, so it must be two parts
  of ASCII letters, digits, `-`, `.`, and `_`, neither `.` nor `..`, the form that `ForgejoRepositoryName` accepts and
  that includes every GitHub repository. Anything else is the parse error `--repository requires owner/name: ...`. The
  forge client still applies its own, narrower rule; GitHub, for example, allows no `_` in an owner.
- `ForgejoOption` is `--forgejo <url>`, a Forgejo instance that replaces GitHub. Only an address that
  `ForgejoApiClient.ApiBase` accepts parses: absolute HTTPS without user information, a query, or a fragment.

Each option takes its help text, so a tool says what the root holds or what the repository is for. `Required` and the
rest of `Option<T>` stay available.
