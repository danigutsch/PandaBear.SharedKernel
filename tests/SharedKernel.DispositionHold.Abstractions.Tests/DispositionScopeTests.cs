namespace PandaBear.DispositionHold.Tests;

/// <summary>
/// Tests DispositionScope identifier validation.
/// </summary>
public sealed class DispositionScopeTests
{
    [Fact]
    public void From_trims_and_keeps_opaque_identifiers()
    {
        // Arrange
        const string kind = " document-lineage ";
        const string key = " 0194f2a0-aaaa-7bbb-8ccc-0123456789ab ";

        // Act
        var scope = DispositionScope.From(kind, key);

        // Assert
        scope.Kind.ShouldBe("document-lineage");
        scope.Key.ShouldBe("0194f2a0-aaaa-7bbb-8ccc-0123456789ab");
        scope.ToString().ShouldBe("document-lineage/0194f2a0-aaaa-7bbb-8ccc-0123456789ab");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("ada@example.org")]
    [InlineData("Ada Lovelace")]
    [InlineData("naïve")]
    public void From_rejects_values_that_could_carry_free_text_or_personal_data(string key)
    {
        // Arrange, Act
        Action create = () => DispositionScope.From("document", key);

        // Assert
        _ = create.ShouldThrow<ArgumentException>();
    }

    [Fact]
    public void From_rejects_null_and_over_long_values()
    {
        // Arrange
        var tooLongKey = new string('a', DispositionScope.MaxKeyLength + 1);
        var tooLongKind = new string('a', DispositionScope.MaxKindLength + 1);

        // Act
        Action nullKind = () => DispositionScope.From(null, "key");
        Action longKey = () => DispositionScope.From("document", tooLongKey);
        Action longKind = () => DispositionScope.From(tooLongKind, "key");

        // Assert
        _ = nullKind.ShouldThrow<ArgumentNullException>();
        _ = longKey.ShouldThrow<ArgumentException>();
        _ = longKind.ShouldThrow<ArgumentException>();
    }

    [Fact]
    public void From_accepts_values_at_the_length_limits()
    {
        // Arrange
        var kind = new string('k', DispositionScope.MaxKindLength);
        var key = new string('v', DispositionScope.MaxKeyLength);

        // Act
        var scope = DispositionScope.From(kind, key);

        // Assert
        scope.Kind.Length.ShouldBe(DispositionScope.MaxKindLength);
        scope.Key.Length.ShouldBe(DispositionScope.MaxKeyLength);
    }

    [Fact]
    public void Default_instance_rejects_member_access()
    {
        // Arrange
        var scope = default(DispositionScope);

        // Act
        Func<string> kind = () => scope.Kind;
        Func<string> key = () => scope.Key;

        // Assert
        _ = kind.ShouldThrow<InvalidOperationException>();
        _ = key.ShouldThrow<InvalidOperationException>();
    }
}
