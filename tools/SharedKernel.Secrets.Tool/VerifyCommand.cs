using System.Globalization;
using PandaBear.Results;

namespace PandaBear.Secrets.Tool;

/// <summary>
/// Implements <c>verify</c>: every <c>*.sops.*</c> file under the root must be encrypted to exactly the recipients of the first creation rule that
/// matches it, with nothing in clear. It starts no process. Verifying nothing is a failure, a file whose path cannot be judged is a failure, and a
/// file that no rule covers makes the run differ unless the caller names that risk with <c>--allow-unverified-files</c>.
/// </summary>
internal static class VerifyCommand
{
    private const string SopsConfigurationName = ".sops.yaml";

    /// <summary>
    /// Returns 0 when every file is covered and exact, 1 when one differs or is uncovered, and 2 when nothing was verified or a file cannot be
    /// read or judged. A failure outranks a difference.
    /// </summary>
    /// <exception cref="InvalidOperationException">The configuration is invalid.</exception>
    public static async Task<int> Run(string root, bool allowUnverifiedFiles, TextWriter output, TextWriter error, CancellationToken cancellationToken)
    {
        var rules = await SecretsConfigurationFile.Read(root, cancellationToken).ConfigureAwait(false);
        var covered = 0;
        var uncovered = 0;
        var different = 0;
        var unreadable = 0;
        var unjudgeable = 0;
        foreach (var file in SopsFileFinder.Find(root))
        {
            if (Path.GetFileName(file) == SopsConfigurationName)
            {
                continue;
            }

            var path = EncryptedFilePath.Create(file);
            if (path.IsFailure)
            {
                unjudgeable++;
                await EscapedOutput.Line(error, $"{file}: the path cannot be checked: {path.ErrorDetails?.Detail}", cancellationToken).ConfigureAwait(false);
                continue;
            }

            var rule = rules.FirstMatch(path.Value);
            if (rule is null)
            {
                uncovered++;
                await EscapedOutput.Line(output, $"{file}: no creation rule matches, not verified", cancellationToken).ConfigureAwait(false);
                continue;
            }

            covered++;
            var resolved = path.Value.ResolveUnder(root);
            var holders = resolved.IsSuccess
                ? await SopsFileReader.ReadRecipients(resolved.Value, cancellationToken).ConfigureAwait(false)
                : Result.Error<SopsFileRecipients>("The file path cannot be resolved under the root.");
            if (holders.IsFailure)
            {
                unreadable++;
                await EscapedOutput.Line(error, $"{file}: {holders.ErrorDetails?.Detail}", cancellationToken).ConfigureAwait(false);
                continue;
            }

            var verification = RecipientVerification.Compare(holders.Value, rule);
            if (!verification.IsExact)
            {
                different++;
            }

            await RecipientsCommand.Report(output, file, verification, cancellationToken).ConfigureAwait(false);
        }

        await EscapedOutput.Line(
            output,
            string.Create(CultureInfo.InvariantCulture, $"{covered} files match a creation rule: {covered - different - unreadable} exact, {different} differ, {unreadable} unreadable. {uncovered} other *.sops.* files are not covered by a rule, {unjudgeable} have paths that cannot be checked."),
            cancellationToken).ConfigureAwait(false);
        if (covered == 0)
        {
            await EscapedOutput.Line(error, "No file matches a creation rule, so nothing was verified.", cancellationToken).ConfigureAwait(false);
            return ExitCodes.Failure;
        }

        if (unreadable > 0 || unjudgeable > 0)
        {
            return ExitCodes.Failure;
        }

        return different > 0 || (uncovered > 0 && !allowUnverifiedFiles) ? ExitCodes.Differences : ExitCodes.Success;
    }
}
