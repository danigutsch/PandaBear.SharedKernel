namespace SharedKernel.Repository.Tests.Conventions;

/// <summary>
/// Guards the prefix of the identifiers that SharedKernel writes at run time or into a consumer's model, such as claim types,
/// database function names, model annotations and named HTTP clients: they use <c>SharedKernel</c>, as telemetry names and namespaces
/// do, never <c>PandaBear</c>, which names only package IDs.
/// </summary>
public sealed class RuntimeIdentifierConventionTests
{
    [Fact]
    public void No_string_literal_uses_the_lower_case_or_annotation_form_of_the_package_prefix()
    {
        // Act
        var violations = RuntimeIdentifierSourceFiles.Literals()
            .Where(static entry => entry.Value.Contains("pandabear", StringComparison.Ordinal)
                || entry.Value.Contains("PandaBear:", StringComparison.Ordinal))
            .Select(static entry => $"{entry.File}: \"{entry.Value}\"")
            .ToList();

        // Assert
        violations.ShouldBeEmpty();
    }

    [Fact]
    public void Every_named_HTTP_client_uses_the_registered_prefix()
    {
        // Act
        var names = RuntimeIdentifierSourceFiles.HttpClientNames();

        // Assert
        names.Count.ShouldBeGreaterThanOrEqualTo(3, "the scan must find the named HTTP clients it guards");
        names.Where(static entry => !entry.Name.StartsWith("SharedKernel.", StringComparison.Ordinal))
            .Select(static entry => $"{entry.File}: {entry.Name}")
            .ShouldBeEmpty();
    }
}
