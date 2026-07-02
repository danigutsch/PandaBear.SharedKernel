
namespace SharedKernel.Testing.CodeFixRunner.Tests;

public sealed class TestAssertWrapperTests
{
    [Fact]
    public void ExactlyOne_returns_the_only_collection_item()
    {
        var value = TestAssert.ExactlyOne([42]);

        TestAssert.Equal(42, value);
    }
}
