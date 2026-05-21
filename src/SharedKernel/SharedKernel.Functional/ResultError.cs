using System.Collections.ObjectModel;

namespace SharedKernel.Functional;

/// <summary>
/// Represents error details for a failed result.
/// </summary>
public sealed class ResultError : IEquatable<ResultError>
{
    private readonly IReadOnlyDictionary<string, IReadOnlyList<string>>? validationErrors;

    /// <summary>
    /// Initializes a new instance of the <see cref="ResultError"/> class.
    /// </summary>
    /// <param name="detail">Specific explanation of the problem instance.</param>
    /// <param name="code">Stable machine-readable error code.</param>
    /// <param name="validationErrors">Optional validation errors keyed by field name.</param>
    public ResultError(
        string detail,
        string code = ResultErrorCodes.Error,
        IReadOnlyDictionary<string, string[]>? validationErrors = null)
        : this(detail, code, CloneValidationErrors(validationErrors))
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ResultError"/> class.
    /// </summary>
    /// <param name="detail">Specific explanation of the problem instance.</param>
    /// <param name="code">Stable machine-readable error code.</param>
    /// <param name="validationErrors">Optional validation errors keyed by field name.</param>
    internal ResultError(
        string detail,
        string code,
        IReadOnlyDictionary<string, IReadOnlyList<string>>? validationErrors)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(detail);
        ArgumentException.ThrowIfNullOrWhiteSpace(code);

        Detail = detail;
        Code = code;
        this.validationErrors = validationErrors;
    }

    /// <summary>
    /// Gets the stable machine-readable error code.
    /// </summary>
    public string Code { get; }

    /// <summary>
    /// Gets specific explanation of the problem instance.
    /// </summary>
    public string Detail { get; }

    /// <summary>
    /// Gets validation errors keyed by field name.
    /// </summary>
    public IReadOnlyDictionary<string, IReadOnlyList<string>>? ValidationErrors => validationErrors;

    /// <inheritdoc />
    public bool Equals(ResultError? other)
    {
        if (other is null)
        {
            return false;
        }

        if (ReferenceEquals(this, other))
        {
            return true;
        }

        return string.Equals(Code, other.Code, StringComparison.Ordinal)
            && string.Equals(Detail, other.Detail, StringComparison.Ordinal)
            && ValidationErrorsEqual(validationErrors, other.validationErrors);
    }

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is ResultError other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Code, StringComparer.Ordinal);
        hash.Add(Detail, StringComparer.Ordinal);

        if (validationErrors is not null)
        {
            foreach (var (field, messages) in validationErrors.OrderBy(static pair => pair.Key, StringComparer.Ordinal))
            {
                hash.Add(field, StringComparer.Ordinal);

                foreach (var message in messages)
                {
                    hash.Add(message, StringComparer.Ordinal);
                }
            }
        }

        return hash.ToHashCode();
    }

    /// <inheritdoc />
    public override string ToString() => $"{Code}: {Detail}";

    private static bool ValidationErrorsEqual(
        IReadOnlyDictionary<string, IReadOnlyList<string>>? left,
        IReadOnlyDictionary<string, IReadOnlyList<string>>? right)
    {
        if (ReferenceEquals(left, right))
        {
            return true;
        }

        if (left is null || right is null || left.Count != right.Count)
        {
            return false;
        }

        foreach (var (field, leftMessages) in left)
        {
            if (!right.TryGetValue(field, out var rightMessages))
            {
                return false;
            }

            if (!leftMessages.SequenceEqual(rightMessages, StringComparer.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    private static ReadOnlyDictionary<string, IReadOnlyList<string>>? CloneValidationErrors(
        IReadOnlyDictionary<string, string[]>? validationErrors)
    {
        if (validationErrors is null)
        {
            return null;
        }

        var clone = new Dictionary<string, IReadOnlyList<string>>(validationErrors.Count, StringComparer.Ordinal);
        foreach (var (field, messages) in validationErrors)
        {
            clone[field] = Array.AsReadOnly([.. messages]);
        }

        return new ReadOnlyDictionary<string, IReadOnlyList<string>>(clone);
    }
}
