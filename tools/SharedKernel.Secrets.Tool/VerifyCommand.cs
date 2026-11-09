using System.Globalization;
using PandaBear.Results;

namespace PandaBear.Secrets.Tool;

/// <summary>
/// Implements <c>verify</c>: every <c>*.sops.*</c> file under the root that a creation rule matches must be encrypted to exactly that
/// rule's recipients. Files that no rule matches are counted and not judged. It starts no process.
/// </summary>
internal static class VerifyCommand
{
    /// <summary>Returns 0 when every covered file is exact, 1 when one differs, and 2 when a covered file cannot be read.</summary>
    /// <exception cref="InvalidOperationException">The configuration is invalid.</exception>
    public static async Task<int> Run(string root, TextWriter output, TextWriter error, CancellationToken cancellationToken)
    {
        var rules = await SecretsConfigurationFile.Read(root, cancellationToken).ConfigureAwait(false);
        var covered = 0;
        var uncovered = 0;
        var different = 0;
        var unreadable = 0;
        foreach (var file in SopsFileFinder.Find(root))
        {
            var path = EncryptedFilePath.Create(file);
            var rule = path.IsSuccess ? rules.FirstMatch(path.Value) : null;
            if (rule is null)
            {
                uncovered++;
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
            string.Create(CultureInfo.InvariantCulture, $"{covered} files match a creation rule: {covered - different - unreadable} exact, {different} differ, {unreadable} unreadable. {uncovered} other *.sops.* files are not covered by a rule."),
            cancellationToken).ConfigureAwait(false);
        return unreadable > 0 ? ExitCodes.Failure : different > 0 ? ExitCodes.Differences : ExitCodes.Success;
    }
}
