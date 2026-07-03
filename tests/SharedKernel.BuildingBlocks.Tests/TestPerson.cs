
namespace SharedKernel.BuildingBlocks.Tests;

internal sealed class TestPerson(string firstName, string lastName, int age) : ValueObject
{
    public string FirstName { get; } = firstName;

    public string LastName { get; } = lastName;

    public int Age { get; } = age;

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return FirstName;
        yield return LastName;
        yield return Age;
    }
}
