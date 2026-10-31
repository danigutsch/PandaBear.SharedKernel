namespace PandaBear.OpenApi.Tests;

/// <summary>Protects operation matching across API descriptions and generated document routes.</summary>
[Xunit.Trait(Testing.TestTraitNames.ScopeName, "unit")]
public sealed class OpenApiOperationKeyTests
{
    [Xunit.Theory]
    [Xunit.InlineData("")]
    [Xunit.InlineData(" ")]
    [Xunit.InlineData("GET POST")]
    [Xunit.InlineData("GET\n")]
    public void Invalid_http_method_tokens_cannot_form_operation_keys(string method)
    {
        // Act
        Action create = () => _ = OpenApiOperationKey.Create("files", method);

        // Assert
        create.ShouldThrow<ArgumentException>();
    }

    [Xunit.Theory]
    [Xunit.InlineData("/Files/{id:guid}", "post", "files/{ID}", "POST")]
    [Xunit.InlineData("/files/{id:int=1}/", "GET", "files/{id}", "get")]
    public void Equivalent_routes_and_methods_share_dictionary_identity(string firstRoute, string firstMethod, string secondRoute, string secondMethod)
    {
        // Arrange
        var first = OpenApiOperationKey.Create(firstRoute, firstMethod);
        var second = OpenApiOperationKey.Create(secondRoute, secondMethod);
        var operations = new Dictionary<OpenApiOperationKey, string> { [first] = "operation" };

        // Act
        var found = operations.TryGetValue(second, out var operation);

        // Assert
        found.ShouldBeTrue();
        operation.ShouldBe("operation");
        second.ShouldBe(first);
    }

    [Xunit.Theory]
    [Xunit.InlineData("files/{id}", "POST")]
    [Xunit.InlineData("files/{name}", "GET")]
    public void Different_methods_and_parameter_names_remain_distinct(string route, string method)
    {
        // Arrange
        var original = OpenApiOperationKey.Create("files/{id}", "GET");

        // Act
        var candidate = OpenApiOperationKey.Create(route, method);

        // Assert
        candidate.ShouldNotBe(original);
    }
}
