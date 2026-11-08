using System.Text;
using PandaBear.Results;

namespace PandaBear.Packages;

/// <summary>Checks and conversions that the readers share.</summary>
internal static class PackageReaderSupport
{
    private const int MaxDetailLength = 200;

    /// <summary>Returns <paramref name="path"/> after checking that it is an absolute path.</summary>
    /// <exception cref="ArgumentNullException"><paramref name="path"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="path"/> is not an absolute path.</exception>
    public static string RequireAbsolute(string path, string parameterName)
    {
        ArgumentNullException.ThrowIfNull(path);
        return Path.IsPathFullyQualified(path) ? path : throw new ArgumentException("The path must be absolute.", parameterName);
    }

    /// <summary>Builds a package from text, or returns <see langword="null"/> when the name or version is invalid.</summary>
    public static InstalledPackage? TryCreate(string name, string version)
    {
        var packageName = PackageName.Create(name);
        var packageVersion = PackageVersion.Create(version);
        return packageName.IsSuccess && packageVersion.IsSuccess ? new InstalledPackage(packageName.Value, packageVersion.Value) : null;
    }

    /// <summary>Builds a read failure that names the tool and a line, and never repeats the output.</summary>
    public static Result<IReadOnlyList<InstalledPackage>> Failure(string message) => Result.Error<IReadOnlyList<InstalledPackage>>(message);

    /// <summary>
    /// Runs a tool. A tool that cannot start, runs too long, writes too much, or exits with a non-zero code is a failure
    /// message, never an exception; cancellation by the caller still propagates.
    /// </summary>
    public static async Task<(ProcessOutput? Output, string? Failure)> Run(
        IProcessRunner runner,
        string executable,
        string tool,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken)
    {
        ProcessOutput output;
        try
        {
            output = await runner.Run(executable, arguments, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is InvalidOperationException or TimeoutException)
        {
            return (null, $"{tool} could not run: {exception.Message}");
        }

        return output.ExitCode == 0 ? (output, null) : (null, $"{tool} failed with exit code {output.ExitCode}.{Detail(output.StandardError)}");
    }

    /// <summary>Orders the packages by name and fails when one name appears twice, because a plan cannot tell which entry is meant.</summary>
    public static Result<IReadOnlyList<InstalledPackage>> Finish(string tool, List<InstalledPackage> packages)
    {
        HashSet<PackageName> seen = [];
        foreach (var package in packages)
        {
            if (!seen.Add(package.Name))
            {
                return Failure($"{tool} listed {package.Name} more than once; remove the extra installation, branch, or entry, because a plan cannot tell which one is meant.");
            }
        }

        return Result.Ok<IReadOnlyList<InstalledPackage>>([.. packages.OrderBy(static p => p.Name.Value, StringComparer.Ordinal)]);
    }

    // The first non-blank line of the tool's error output, without control characters and cut to a safe length.
    private static string Detail(string standardError)
    {
        var line = standardError.Split('\n').Select(static l => l.Trim()).FirstOrDefault(static l => l.Length > 0);
        if (line is null)
        {
            return string.Empty;
        }

        StringBuilder clean = new();
        foreach (var character in line.Take(MaxDetailLength))
        {
            clean.Append(char.IsControl(character) ? ' ' : character);
        }

        return $" {clean}";
    }
}
