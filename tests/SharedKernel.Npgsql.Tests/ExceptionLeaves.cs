namespace PandaBear.Npgsql.Tests;

/// <summary>Flattens exceptions into the individual failures a test wants to assert on.</summary>
internal static class ExceptionLeaves
{
    /// <summary>Returns the exception itself, or the flattened inner exceptions when it is an aggregate.</summary>
    public static Exception[] Of(Exception exception) =>
        exception is AggregateException aggregate ? [.. aggregate.Flatten().InnerExceptions] : [exception];
}
