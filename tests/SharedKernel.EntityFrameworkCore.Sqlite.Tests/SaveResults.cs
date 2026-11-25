using Microsoft.EntityFrameworkCore;
using SharedKernel.Results;

namespace SharedKernel.EntityFrameworkCore.Sqlite.Tests;

/// <summary>Rule mappings for the result-returning save tests.</summary>
public static class SaveResults
{
    /// <summary>Gets the field error every mapped rule becomes.</summary>
    public static Result FieldError { get; } = Result.Invalid("taken", "email", "taken");

    /// <summary>Fails the test: the save was not expected to report a rule.</summary>
    /// <param name="rule">The reported rule.</param>
    /// <returns>Never returns.</returns>
    /// <exception cref="InvalidOperationException">Always.</exception>
    public static Result UnexpectedRule(string rule) => throw new InvalidOperationException($"The save reported the unexpected rule '{rule}'.");

    /// <summary>Records the reported rule and answers <see cref="FieldError"/>.</summary>
    /// <param name="rules">The rules reported so far.</param>
    /// <param name="rule">The reported rule.</param>
    /// <returns><see cref="FieldError"/>.</returns>
    public static Result Recorded(ICollection<string> rules, string rule)
    {
        ArgumentNullException.ThrowIfNull(rules);

        rules.Add(rule);
        return FieldError;
    }

    /// <summary>Saves through <see cref="DbContextResultExtensions.SaveChangesAsResult"/> and returns what it threw.</summary>
    /// <param name="context">The context with pending changes.</param>
    /// <param name="parser">The parser.</param>
    /// <returns>The thrown exception.</returns>
    public static async Task<Exception> FailedSave(DbContext context, IUniqueViolationParser parser)
    {
        try
        {
            await context.SaveChangesAsResult(parser, UnexpectedRule, TestContext.Current.CancellationToken);
        }
        catch (DbUpdateException exception)
        {
            return exception;
        }

        throw new InvalidOperationException("The save did not throw.");
    }
}
