using Aspire.Hosting;

namespace SharedKernel.Aspire.Hosting.Tests;

/// <summary>Tests AddGeneratedSecret: a secret parameter whose value is generated unless configuration supplies one.</summary>
public sealed class GeneratedSecretTests
{
    [Fact]
    public async Task Without_configuration_the_value_is_generated_with_at_least_the_minimum_length_from_letters_and_digits()
    {
        // Arrange
        var builder = DistributedApplication.CreateBuilder([]);

        // Act
        var secret = builder.AddGeneratedSecret("api-key", minLength: 40);
        var value = await secret.Resource.GetValueAsync(TestContext.Current.CancellationToken);

        // Assert
        secret.Resource.Secret.ShouldBeTrue("the dashboard must mask it and a publish must treat it as a secret");
        value.ShouldNotBeNull().Length.ShouldBeGreaterThanOrEqualTo(40);
        value.ShouldAllSatisfy(character => char.IsAsciiLetterOrDigit(character).ShouldBeTrue(), "no character needs escaping in a connection string or URL");
    }

    [Fact]
    public async Task A_configured_value_wins_over_the_generated_one()
    {
        // Arrange
        var builder = DistributedApplication.CreateBuilder([]);
        builder.Configuration["Parameters:api-key"] = "configured-by-user-secrets";

        // Act
        var secret = builder.AddGeneratedSecret("api-key");
        var value = await secret.Resource.GetValueAsync(TestContext.Current.CancellationToken);

        // Assert
        value.ShouldBe("configured-by-user-secrets");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(15)]
    public void A_minimum_length_below_sixteen_is_refused(int minLength)
    {
        // Arrange
        var builder = DistributedApplication.CreateBuilder([]);

        // Act
        Action add = () => builder.AddGeneratedSecret("api-key", minLength);

        // Assert
        add.ShouldThrow<ArgumentOutOfRangeException>().ParamName.ShouldBe("minLength");
    }
}
