using System.Text.Json.Nodes;

namespace PandaBear.Repository.Tests;

/// <summary>Writes the invalid pins that stage-candidate must reject before it runs Git or changes a file.</summary>
internal static class CandidatePinCases
{
    /// <summary>Writes a pin file that is invalid in the way the named scenario describes, such as an oversized, malformed, commented, duplicated, mis-hashed or wrongly named pin, and throws ArgumentOutOfRangeException for an unknown scenario.</summary>
    public static void Write(CandidateStagingScope scope, string scenario)
    {
        ArgumentNullException.ThrowIfNull(scope);
        var pin = scope.CreatePin();
        var hashes = pin["packageSha256"]!.AsObject();
        var firstName = scope.PackageNames[0];
        var firstHash = hashes[firstName]!.GetValue<string>();
        var valid = pin.ToJsonString();
        switch (scenario)
        {
            case "oversized":
                pin["consumerCompatibility"] = new string('a', 1024 * 1024);
                break;
            case "malformed":
                scope.WritePin("{");
                return;
            case "comment":
                scope.WritePin("// pinned candidate\n" + valid);
                return;
            case "trailing-comma":
                scope.WritePin(valid[..^1] + ",}");
                return;
            case "duplicate-property":
                scope.WritePin("{\"libraryRevision\":\"" + new string('f', 40) + "\"," + valid[1..]);
                return;
            case "null":
                scope.WritePin("null");
                return;
            case "missing-revision":
                pin.Remove("libraryRevision");
                break;
            case "uppercase-revision":
                pin["libraryRevision"] = CandidateStagingScope.Revision.ToUpperInvariant();
                break;
            case "short-revision":
                pin["libraryRevision"] = CandidateStagingScope.Revision[..12];
                break;
            case "dirty":
                pin["libraryDirty"] = true;
                break;
            case "dirty-string":
                pin["libraryDirty"] = "false";
                break;
            case "version-separator":
                pin["packageVersion"] = "1.2.3/../evil";
                break;
            case "version-empty":
                pin["packageVersion"] = string.Empty;
                break;
            case "no-packages":
                pin["packageSha256"] = new JsonObject();
                break;
            case "uppercase-hash":
                hashes[firstName] = firstHash.ToUpperInvariant();
                break;
            case "null-hash":
                hashes[firstName] = null;
                break;
            case "name-prefix":
                hashes.Remove(firstName);
                hashes["Other.Package." + CandidateStagingScope.Version + ".nupkg"] = firstHash;
                break;
            case "name-version":
                hashes.Remove(firstName);
                hashes["PandaBear.SharedKernel.Results.9.9.9.nupkg"] = firstHash;
                break;
            case "name-traversal":
                hashes.Remove(firstName);
                hashes["../" + firstName] = firstHash;
                break;
            case "name-control":
                hashes.Remove(firstName);
                hashes["PandaBear.SharedKernel.Results\u001B[31m." + CandidateStagingScope.Version + ".nupkg"] = firstHash;
                break;
            case "name-case-duplicate":
                hashes["PandaBear.SharedKernel.RESULTS." + CandidateStagingScope.Version + ".nupkg"] = firstHash;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(scenario), scenario, "Unknown pin scenario.");
        }

        scope.WritePin(pin);
    }
}
