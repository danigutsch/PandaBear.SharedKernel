using SharedKernel.Results;

namespace SharedKernel.Blazor.Site.Theming;

/// <summary>Collects validation messages by field path, in the order they are found, and merges nested results under a prefix.</summary>
internal sealed class ThemeErrors
{
    private readonly Dictionary<string, List<string>> messages = new(StringComparer.Ordinal);

    /// <summary>Gets a value indicating whether any message was collected.</summary>
    public bool HasErrors => messages.Count > 0;

    /// <summary>Creates an invalid result with one message on one field.</summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="field">The field path.</param>
    /// <param name="message">The message, which never repeats the input.</param>
    /// <returns>The invalid result.</returns>
    public static Result<T> Invalid<T>(string field, string message)
        where T : notnull => Result.Invalid<T>(message, field, message);

    /// <summary>Adds a message to a field.</summary>
    /// <param name="field">The field path.</param>
    /// <param name="message">The message.</param>
    public void Add(string field, string message)
    {
        if (!messages.TryGetValue(field, out var list))
        {
            list = [];
            messages[field] = list;
        }

        if (!list.Contains(message, StringComparer.Ordinal))
        {
            list.Add(message);
        }
    }

    /// <summary>Adds the validation messages of a failed result, each field prefixed with <paramref name="prefix" /> and a dot.</summary>
    /// <param name="prefix">The path of the value the result belongs to.</param>
    /// <param name="result">The result.</param>
    public void Merge(string prefix, Result result)
    {
        foreach (var (field, fieldMessages) in result.ValidationErrors())
        {
            foreach (var message in fieldMessages)
            {
                Add(prefix + "." + field, message);
            }
        }
    }

    /// <summary>Returns the collected messages as an invalid result.</summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="detail">The summary detail.</param>
    /// <returns>The invalid result.</returns>
    public Result<T> ToResult<T>(string detail)
        where T : notnull => Result.Invalid<T>(detail, ToDictionary());

    /// <summary>Returns the collected messages by field.</summary>
    /// <returns>A copy of the messages.</returns>
    public Dictionary<string, string[]> ToDictionary() =>
        messages.ToDictionary(pair => pair.Key, pair => pair.Value.ToArray(), StringComparer.Ordinal);
}
