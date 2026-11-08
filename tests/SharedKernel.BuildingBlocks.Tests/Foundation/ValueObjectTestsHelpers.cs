namespace PandaBear.BuildingBlocks.Tests;

/// <summary>
/// Helpers for value object tests.
/// </summary>
internal static class ValueObjectTestsHelpers
{
    /// <summary>
    /// Calls the object overload of Equals on the instance with the given, possibly null, argument.
    /// </summary>
    public static bool EqualsObject(object instance, object? other)
    {
        return instance.Equals(other);
    }
}
