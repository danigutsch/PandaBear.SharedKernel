namespace SharedKernel.Results.GeneratorTests;

public sealed class ResultErrorCatalogGeneratorTests
{
    [Fact]
    public void Generates_Error_Catalog_For_Static_Error_Provider_Methods()
    {
        // Arrange
        const string source = """
            namespace Demo;

            public static class UserErrors
            {
                public static Result NotFound(Guid id) => Result.NotFound($"User with id {id} was not found.");

                public static Result DuplicateEmail(string email) => Result.Conflict($"User with email '{email}' already exists.");

                public static Result InvalidEmail() => Result.Invalid(
                    detail: "Email is not valid.",
                    field: "Email",
                    message: "Email is invalid.");
            }
            """;
        var compilation = GeneratorTestHarness.CreateCompilation(source);

        // Act
        var runResult = GeneratorTestHarness.RunGeneratorDriver(compilation);
        var generatedSource = GeneratorTestHarness.GetGeneratedSource(runResult);

        // Assert
        Assert.Contains("[assembly: SharedKernel.Results.ResultErrorCatalogProviderAttribute(typeof(SharedKernel.Results.GeneratedResultErrorCatalogProvider))]", generatedSource, StringComparison.Ordinal);
        Assert.Contains("internal static class ResultErrorCatalogGenerated", generatedSource, StringComparison.Ordinal);
        Assert.Contains("internal sealed class GeneratedResultErrorCatalogProvider", generatedSource, StringComparison.Ordinal);
        Assert.Contains("demo-usererrors-notfound", generatedSource, StringComparison.Ordinal);
        Assert.Contains("demo-usererrors-duplicateemail", generatedSource, StringComparison.Ordinal);
        Assert.Contains("demo-usererrors-invalidemail", generatedSource, StringComparison.Ordinal);
        Assert.Contains("docs/errors/README.md", generatedSource, StringComparison.Ordinal);
        Assert.Contains("ResultStatus.NotFound", generatedSource, StringComparison.Ordinal);
        Assert.Contains("ResultStatus.Conflict", generatedSource, StringComparison.Ordinal);
        Assert.Contains("ResultStatus.Invalid", generatedSource, StringComparison.Ordinal);
        Assert.Contains("\"not_found\"", generatedSource, StringComparison.Ordinal);
        Assert.Contains("\"conflict\"", generatedSource, StringComparison.Ordinal);
        Assert.Contains("\"invalid\"", generatedSource, StringComparison.Ordinal);
        Assert.Contains("User with id {id} was not found.", generatedSource, StringComparison.Ordinal);
        Assert.Contains("User with email '{email}' already exists.", generatedSource, StringComparison.Ordinal);
        Assert.Contains("Email is not valid.", generatedSource, StringComparison.Ordinal);
    }

    [Fact]
    public void Ignores_Non_Error_Classes_And_Non_Result_Members()
    {
        // Arrange
        const string source = """
            namespace Demo;

            public static class UserMessages
            {
                public static string Greeting() => "hello";
            }

            public static class UserErrors
            {
                public static string Greeting() => "hello";
            }
            """;
        var compilation = GeneratorTestHarness.CreateCompilation(source);

        // Act
        var runResult = GeneratorTestHarness.RunGeneratorDriver(compilation);
        var generatedSource = GeneratorTestHarness.GetGeneratedSource(runResult);

        // Assert
        Assert.DoesNotContain("Greeting", generatedSource, StringComparison.Ordinal);
        Assert.Contains("generatedEntries =", generatedSource, StringComparison.Ordinal);
        Assert.Contains("GeneratedResultErrorCatalogProvider", generatedSource, StringComparison.Ordinal);
    }

    [Fact]
    public void Generates_Error_Catalog_For_Internal_Error_Providers_Returning_Generic_Results()
    {
        // Arrange
        const string source = """
            namespace Demo;

            internal static class CsvErrors
            {
                internal static Result<string> MissingHeader(string columnName) => Result.Invalid<string>(
                    detail: $"Required column '{columnName}' is missing.",
                    field: "Headers",
                    message: "Required column is missing.");
            }
            """;
        var compilation = GeneratorTestHarness.CreateCompilation(source);

        // Act
        var runResult = GeneratorTestHarness.RunGeneratorDriver(compilation);
        var generatedSource = GeneratorTestHarness.GetGeneratedSource(runResult);

        // Assert
        Assert.Contains("demo-csverrors-missingheader", generatedSource, StringComparison.Ordinal);
        Assert.Contains("ResultStatus.Invalid", generatedSource, StringComparison.Ordinal);
        Assert.Contains("\"invalid\"", generatedSource, StringComparison.Ordinal);
        Assert.Contains("Required column '{columnName}' is missing.", generatedSource, StringComparison.Ordinal);
    }
}
