namespace SharedKernel.BuildingBlocks.Tests;

internal static class ValueObjectTestsHelpers
{
    public static bool EqualsObject(object instance, object? other)
    {
        return instance.Equals(other);
    }
}
