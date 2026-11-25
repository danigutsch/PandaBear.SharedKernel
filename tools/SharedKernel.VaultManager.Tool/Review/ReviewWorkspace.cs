using System.Globalization;
using SharedKernel.Vault.Merge;
using SharedKernel.Vault.Pipeline;
using SharedKernel.Vault.Review;

namespace SharedKernel.VaultManager.Tool.Review;

/// <summary>Connects a review session to the files and password input chosen by the tool.</summary>
internal sealed class ReviewWorkspace
{
    private readonly VaultCommandEnvironment environment;

    /// <summary>Starts a review for the supplied files and decisions.</summary>
    /// <exception cref="ArgumentNullException">The environment or file configuration is missing.</exception>
    public ReviewWorkspace(VaultCommandEnvironment environment, ScannedVault scanned, MergeDecisions decisions, ReviewFiles files)
    {
        ArgumentNullException.ThrowIfNull(environment);
        ArgumentNullException.ThrowIfNull(files);
        this.environment = environment;
        Files = files;
        Session = new ReviewSession(environment.CreatePipeline(), scanned, decisions);
    }

    /// <summary>Gets the reusable review state.</summary>
    public ReviewSession Session { get; }

    /// <summary>Gets the files selected for this review.</summary>
    public ReviewFiles Files { get; }

    /// <summary>Gets the message shown when the review cannot write the merged vault.</summary>
    public string? WriteBlocker =>
        Files.Output is null ? "No output file was given. Start the review with --output <file> to write the merged vault."
        : !Session.Pending.IsEmpty ? string.Create(CultureInfo.InvariantCulture, $"{Session.Pending.Fields.Count} field(s) and {Session.Pending.FindingCount} finding(s) still need a decision. Nothing was written.")
        : Session.Result.HasWriteErrors ? "Bitwarden would reject the import, so nothing was written: " + string.Join(" ", Session.Result.WriteErrors.Select(ReviewText.OneLine))
        : null;

    /// <summary>Writes the checkpoint to the configured private decisions file.</summary>
    /// <exception cref="InvalidOperationException">The review has no decisions file.</exception>
    /// <exception cref="IOException">The decisions file cannot be written.</exception>
    public void Save()
    {
        if (Files.DecisionsPath is not { } path)
        {
            throw new InvalidOperationException("No decisions file was given. Start the review with --decisions <file> to save decisions.");
        }

        Session.Save(decisions => DecisionsFile.Write(path, decisions));
    }

    /// <summary>Writes the configured merged export with the password supplied by the terminal.</summary>
    /// <exception cref="InvalidOperationException">The review has no output or is blocked.</exception>
    /// <exception cref="ArgumentException"><paramref name="exportPassword"/> is missing or too short for an encrypted export.</exception>
    /// <exception cref="IOException">The export cannot be written to the configured location.</exception>
    public void Write(string? exportPassword)
    {
        if (WriteBlocker is { } blocker)
        {
            throw new InvalidOperationException(blocker);
        }

        PipelineOutput output = Files.Output!;
        if (!output.Plaintext && (exportPassword is null || exportPassword.Length < PasswordReader.MinimumNewPasswordLength))
        {
            throw new ArgumentException(
                string.Create(CultureInfo.InvariantCulture, $"The password for the new export needs at least {PasswordReader.MinimumNewPasswordLength} characters because it protects every secret in the file. Nothing was written."),
                nameof(exportPassword));
        }

        var exporter = (environment with
        {
            ReadNewPassword = _ => exportPassword ?? string.Empty
        }).CreatePipeline();
        Session.Write(output, exporter);
    }
}
