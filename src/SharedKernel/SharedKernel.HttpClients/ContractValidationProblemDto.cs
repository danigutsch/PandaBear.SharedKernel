namespace SharedKernel.HttpClients;

/// <summary>
/// Represents the RFC 7807 validation problem members used by contract-owned API clients.
/// </summary>
public sealed record ContractValidationProblemDto
{
    /// <summary>
    /// Gets the validation errors keyed by field name.
    /// </summary>
    public Dictionary<string, string[]>? Errors { get; init; }

    internal bool HasWellFormedErrors()
    {
        if (Errors is null or { Count: 0 })
        {
            return false;
        }

        foreach (var (field, messages) in Errors)
        {
            if ((field.Length > 0 && string.IsNullOrWhiteSpace(field))
                || messages is null or { Length: 0 }
                || messages.Any(string.IsNullOrWhiteSpace))
            {
                return false;
            }
        }

        return true;
    }
}
