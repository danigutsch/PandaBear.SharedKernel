
namespace PandaBear.BuildingBlocks.Tests;

/// <summary>
/// A value object made of a first name, last name and age, used to test equality over several components.
/// </summary>
/// /// <param name="firstName">The first name component.</param>
/// /// <param name="lastName">The last name component.</param>
/// /// <param name="age">The age component.</param>
internal sealed class TestPerson(string firstName, string lastName, int age) : ValueObject
{
    /// <summary>
    /// Gets the first name.
    /// </summary>
    public string FirstName { get; } = firstName;

    /// <summary>
    /// Gets the last name.
    /// </summary>
    public string LastName { get; } = lastName;

    /// <summary>
    /// Gets the age in years.
    /// </summary>
    public int Age { get; } = age;

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return FirstName;
        yield return LastName;
        yield return Age;
    }
}
