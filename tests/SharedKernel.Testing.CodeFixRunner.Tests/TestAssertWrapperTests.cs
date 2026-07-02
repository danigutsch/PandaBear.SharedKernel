
namespace SharedKernel.Testing.CodeFixRunner.Tests;

public sealed class TestAssertWrapperTests
{
    [Fact]
    public void ExactlyOne_returns_the_only_collection_item()
    {
        int[] values = [42];

        var value = values.ShouldHaveSingleItem();

        (value).ShouldBe(42);
    }
}
