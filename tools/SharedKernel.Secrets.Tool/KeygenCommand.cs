namespace PandaBear.Secrets.Tool;

/// <summary>
/// Implements <c>keygen</c>: previews, or with <c>--force</c> runs <c>age-keygen</c>, which writes the identity file with
/// mode 0600. Only the public recipient is printed. An existing file is never overwritten.
/// </summary>
internal static class KeygenCommand
{
    /// <summary>Runs the command and returns its exit code.</summary>
    /// <exception cref="InvalidOperationException">The path is invalid or taken, a tool is missing, or age-keygen failed.</exception>
    public static async Task<int> Run(SecretsEnvironment environment, string outputPath, bool force, TextWriter output, CancellationToken cancellationToken)
    {
        var full = Path.GetFullPath(outputPath, environment.WorkingDirectory);
        var identity = AgeIdentityRef.Create(full, "output");
        if (identity.IsFailure)
        {
            throw new InvalidOperationException(identity.ErrorDetails?.Detail ?? "The identity file path is invalid.");
        }

        if (File.Exists(full) || Directory.Exists(full))
        {
            throw new InvalidOperationException("The identity file already exists; it is never overwritten.");
        }

        var age = ToolLocator.Age(environment);
        if (!force)
        {
            await EscapedOutput.Line(output, $"Preview: would create the age identity file {full} with mode 0600.", cancellationToken).ConfigureAwait(false);
            await EscapedOutput.Line(output, "Nothing was written. Run again with --force to create it.", cancellationToken).ConfigureAwait(false);
            return ExitCodes.Success;
        }

        var recipient = await age.GenerateIdentity(identity.Value, cancellationToken).ConfigureAwait(false);
        if (recipient.IsFailure)
        {
            throw new InvalidOperationException(recipient.ErrorDetails?.Detail ?? "age-keygen failed.");
        }

        await EscapedOutput.Line(output, $"Public recipient: {recipient.Value}", cancellationToken).ConfigureAwait(false);
        await EscapedOutput.Line(output, $"Identity file written to {full} (mode 0600). Keep it private and back it up; add the public recipient to a creation rule.", cancellationToken).ConfigureAwait(false);
        return ExitCodes.Success;
    }
}
