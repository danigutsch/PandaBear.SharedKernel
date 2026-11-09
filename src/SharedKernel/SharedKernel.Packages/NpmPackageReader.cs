using System.Text.Json;
using PandaBear.Results;

namespace PandaBear.Packages;

/// <summary>Reads the globally installed npm packages with <c>npm ls --global --depth=0 --json</c>. It changes nothing.</summary>
public sealed class NpmPackageReader : IPackageReader
{
    private static readonly string[] Arguments = ["ls", "--global", "--depth=0", "--json"];
    private readonly IProcessRunner _runner;
    private readonly string _npm;

    /// <summary>Creates a reader.</summary>
    /// <param name="runner">Runs <c>npm</c>.</param>
    /// <param name="npmPath">The absolute path of <c>npm</c>.</param>
    /// <exception cref="ArgumentNullException"><paramref name="runner"/> or <paramref name="npmPath"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="npmPath"/> is not an absolute path.</exception>
    public NpmPackageReader(IProcessRunner runner, string npmPath)
    {
        ArgumentNullException.ThrowIfNull(runner);
        _runner = runner;
        _npm = PackageReaderSupport.RequireAbsolute(npmPath, nameof(npmPath));
    }

    /// <inheritdoc />
    public async Task<Result<IReadOnlyList<InstalledPackage>>> ListInstalled(CancellationToken cancellationToken)
    {
        var (output, failure) = await PackageReaderSupport.Run(_runner, _npm, "npm", Arguments, cancellationToken).ConfigureAwait(false);
        if (failure is not null)
        {
            return PackageReaderSupport.Failure(failure);
        }

        try
        {
            using var document = JsonDocument.Parse(output!.StandardOutput);
            List<InstalledPackage> packages = [];
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return PackageReaderSupport.Failure("npm did not print a JSON object.");
            }

            if (document.RootElement.TryGetProperty("error", out _))
            {
                return PackageReaderSupport.Failure("npm reported an error instead of a package list.");
            }

            if (document.RootElement.TryGetProperty("dependencies", out var dependencies))
            {
                if (dependencies.ValueKind != JsonValueKind.Object)
                {
                    return PackageReaderSupport.Failure("npm printed dependencies that are not an object.");
                }

                var index = 0;
                foreach (var dependency in dependencies.EnumerateObject())
                {
                    index++;
                    var version = dependency.Value.ValueKind == JsonValueKind.Object && dependency.Value.TryGetProperty("version", out var value) && value.ValueKind == JsonValueKind.String
                        ? value.GetString()!
                        : string.Empty;
                    var package = PackageReaderSupport.TryCreate(dependency.Name, version);
                    if (package is null)
                    {
                        return PackageReaderSupport.Failure($"npm dependency {index} has an invalid name or no version.");
                    }

                    packages.Add(package);
                }
            }

            return PackageReaderSupport.Finish("npm", packages);
        }
        catch (JsonException)
        {
            return PackageReaderSupport.Failure("npm did not print valid JSON.");
        }
    }
}
