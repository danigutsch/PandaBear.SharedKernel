using SharedKernel.Results;

namespace SharedKernel.BuildingBlocks.Tests;

/// <summary>Reads the messages that collected validation errors hold for one field.</summary>
internal static class FieldMessages
{
    /// <summary>Gets the messages of a field in the order they were recorded.</summary>
    /// <param name="errors">The collected errors; at least one.</param>
    /// <param name="field">The field path.</param>
    /// <returns>The messages.</returns>
    public static string[] Of(ValidationErrors errors, string field) =>
        [.. errors.ToResult().ErrorDetails.ShouldNotBeNull().ValidationErrors.ShouldNotBeNull()[field]];
}
