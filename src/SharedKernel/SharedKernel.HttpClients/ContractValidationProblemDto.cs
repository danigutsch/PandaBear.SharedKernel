namespace PandaBear.HttpClients;

/// <summary>
/// Represents the validation problem errors member consumed by contract-owned API clients.
/// </summary>
public sealed record ContractValidationProblemDto
{
    /// <summary>
    /// Gets validation errors keyed by field name, or by an empty string for a model-level error.
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
