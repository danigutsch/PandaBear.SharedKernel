# PandaBear.SharedKernel.ConfigurationFiles

[![Repository-wide build status](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml)

Packages remain preview (prerelease); stable packages, tags, and releases require separate authorization.

Reads the strict JSON configuration files that a consuming repository gives command-line tools and their engines, such
as `.config/sharedkernel-ci.json`, the way the
[command-line tool guidance](https://github.com/danigutsch/PandaBear.SharedKernel/blob/main/docs/command-line-tools.md#consumer-configuration)
requires. Targets .NET 10, references only `PandaBear.SharedKernel.IO`, and is trimming- and NativeAOT-compatible: the
caller passes source-generated JSON metadata.

```csharp
using System.Text.Json.Serialization;
using PandaBear.ConfigurationFiles;

var configuration = await JsonConfigurationFile.Read(
    root, ".config/sharedkernel-k6.json", K6ConfigurationJsonContext.Default.K6Configuration, cancellationToken);
if (configuration.Version != 1)
{
    throw new InvalidOperationException(".config/sharedkernel-k6.json: $.version must be 1.");
}

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    AllowDuplicateProperties = false,
    RespectRequiredConstructorParameters = true,
    RespectNullableAnnotations = true)]
[JsonSerializable(typeof(K6Configuration))]
internal sealed partial class K6ConfigurationJsonContext : JsonSerializerContext;
```

- `Read` resolves the path under the root with `PandaBear.IO.RepositoryPath`, which refuses a symbolic link in the path
  or its parents, reads at most 1 MiB, and deserializes it. `ReadOptional` returns `null` when the file does not exist.
- `Parse` deserializes bytes the caller read itself, such as a file at a Git revision. A leading UTF-8 byte order mark
  is ignored.
- Every error is an `InvalidOperationException` whose message starts with the relative path: `does not exist.`,
  `is larger than 1 MiB.`, `must contain a JSON object.`, or `is not valid at <JSON path> (line <n>).` with a hint. The
  serializer's own message is not repeated, because it names .NET types. I/O failures other than a missing file
  propagate as `IOException` or `UnauthorizedAccessException`.
- Strictness lives in the caller's context: `UnmappedMemberHandling.Disallow` and `AllowDuplicateProperties = false`
  reject unknown and repeated properties, and the default reader options reject comments and trailing commas.
  Validation beyond the JSON shape, such as `"version": 1` and the meaning of each value, stays with the caller, which
  reports it as `<relative path>: <JSON path> <problem>`.

## Package identity and availability

Package ID: `PandaBear.SharedKernel.ConfigurationFiles`. C# namespace: `PandaBear.ConfigurationFiles`. Assembly/project identity: `SharedKernel.ConfigurationFiles`.

Source is public; registry publication remains deferred. Consume a validated candidate from an explicitly configured local/private feed. This README does not imply availability on nuget.org.
