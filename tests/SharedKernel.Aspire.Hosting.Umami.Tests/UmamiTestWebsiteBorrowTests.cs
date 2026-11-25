using SharedKernel.Aspire.Hosting.Umami.Testing;

namespace SharedKernel.Aspire.Hosting.Umami.Tests;

/// <summary>Tests how the Umami test website is borrowed from a connection string in the environment.</summary>
public sealed class UmamiTestWebsiteBorrowTests
{
    [Fact]
    public void The_contract_names_the_variable_and_the_marker_that_the_owner_host_and_the_ci_tool_agree_on()
    {
        // Act
        var variable = UmamiTestWebsite.ConnectionStringVariable;
        var unavailable = UmamiTestWebsite.Unavailable;

        // Assert
        variable.ShouldBe("SHAREDKERNEL_TESTING_UMAMI_CONNECTION_STRING", "the CI tool exports this name to the test phase");
        unavailable.ShouldBe("Unavailable", "the owner host hands this over when its Umami did not start");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" \t\r\n")]
    public void An_unset_or_blank_variable_means_no_borrowed_website(string? value)
    {
        // Act
        var borrowed = UmamiTestWebsite.Borrowed(UmamiTestEnvironment.WithWebsite(value));

        // Assert
        borrowed.ShouldBeNull("a blank variable means that the assembly starts an Umami of its own on demand");
    }

    [Fact]
    public void A_website_connection_string_is_borrowed_with_its_endpoint_and_website_ID()
    {
        // Act
        var borrowed = UmamiTestWebsite.Borrowed(UmamiTestEnvironment.WithWebsite("  " + UmamiTestEnvironment.Website + "\n"));

        // Assert
        var website = borrowed.ShouldNotBeNull();
        website.ConnectionString.ShouldBe(UmamiTestEnvironment.Website);
        website.Endpoint.ShouldBe(new Uri("http://localhost:41300"));
        website.WebsiteId.ShouldBe(Guid.Parse("6f1c2d57-8a35-5b1e-9e43-2f6a2c9d4b10"));
    }

    [Theory]
    [InlineData("Host=localhost;Password=secret-canary")]
    [InlineData("Endpoint=http://localhost:41300;Password=secret-canary")]
    [InlineData("Endpoint=ftp://secret-canary;WebsiteId=6f1c2d57-8a35-5b1e-9e43-2f6a2c9d4b10")]
    [InlineData("Endpoint=secret-canary;WebsiteId=6f1c2d57-8a35-5b1e-9e43-2f6a2c9d4b10")]
    [InlineData("Endpoint=http://localhost:41300;WebsiteId=secret-canary")]
    [InlineData("secret-canary")]
    [InlineData("Endpoint='secret-canary")]
    public void A_value_that_is_no_website_connection_string_fails_naming_the_variable_without_repeating_the_value(string value)
    {
        // Arrange
        Func<object?> borrow = () => UmamiTestWebsite.Borrowed(UmamiTestEnvironment.WithWebsite(value));

        // Act
        var failure = borrow.ShouldThrow<InvalidOperationException>();

        // Assert
        failure.Message.ShouldContain(UmamiTestWebsite.ConnectionStringVariable, StringComparison.Ordinal, "the message must tell the reader which variable to fix");
        failure.Message.ShouldNotContain("secret-canary", StringComparison.Ordinal);
    }

    [Fact]
    public void An_owner_whose_umami_did_not_start_fails_the_borrower_instead_of_letting_it_start_a_second_umami()
    {
        // Arrange
        Func<object?> borrow = () => UmamiTestWebsite.Borrowed(UmamiTestEnvironment.WithWebsite(UmamiTestWebsite.Unavailable));

        // Act
        var failure = borrow.ShouldThrow<InvalidOperationException>();

        // Assert
        failure.Message.ShouldBe(
            "The process that owns the shared Umami set SHAREDKERNEL_TESTING_UMAMI_CONNECTION_STRING to Unavailable: Umami did not start there. Its standard error says why.");
    }
}
