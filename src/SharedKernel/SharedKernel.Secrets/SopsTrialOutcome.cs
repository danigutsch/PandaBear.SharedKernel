namespace SharedKernel.Secrets;

/// <summary>What a trial decryption with one identity found out.</summary>
public enum SopsTrialOutcome
{
    /// <summary>The identity decrypted the file.</summary>
    Decrypted,

    /// <summary>sops read the file and the identity, and the identity matches none of the file's recipients: a verdict about the identity.</summary>
    NoMatchingIdentity,

    /// <summary>The trial failed for another reason (an unreadable identity or file, a bad MAC, a parse error, a timeout): no verdict about the identity.</summary>
    Failed,
}
