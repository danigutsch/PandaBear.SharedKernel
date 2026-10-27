using PandaBear.Testing;

namespace PandaBear.Mediator.PackageConsumptionTests;

/// <summary>
/// Builds fresh consumer projects against the packed Domain and Results packages to prove their bundled source generators run for package consumers.
/// </summary>
/// <param name="packageFeed">The shared local package feed the consumer workspaces restore from.</param>
[Collection(MediatorPackageFeedSharing.Name)]
[Trait(SharedKernelTestTraitNames.CategoryName, "PackageConsumption")]
public sealed class SharedKernelGeneratorPackageConsumptionTests(MediatorPackageFeedFixture packageFeed)
{
    [Fact]
    public async Task Domain_source_generator_package_generates_model_support_in_a_fresh_project()
    {
        // Arrange
        using var workspace = packageFeed.CreateWorkspace("DomainGeneratedConsumer");
        workspace.WriteProject(
            $$"""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <OutputType>Exe</OutputType>
                <TargetFramework>net10.0</TargetFramework>
                <ImplicitUsings>enable</ImplicitUsings>
                <Nullable>enable</Nullable>
                <EmitCompilerGeneratedFiles>true</EmitCompilerGeneratedFiles>
                <CompilerGeneratedFilesOutputPath>Generated</CompilerGeneratedFilesOutputPath>
              </PropertyGroup>
              <ItemGroup>
                <Compile Remove="Generated/**/*.cs" />
              </ItemGroup>
              <ItemGroup>
                {{workspace.GetPackageReference("PandaBear.SharedKernel.BuildingBlocks")}}
                {{workspace.GetPackageReference("PandaBear.SharedKernel.Domain")}}
                {{workspace.GetPackageReference("PandaBear.SharedKernel.Domain.SourceGenerator", "PrivateAssets=\"all\" IncludeAssets=\"build;analyzers;buildTransitive\"")}}
              </ItemGroup>
            </Project>
            """,
            ("Consumer.cs", """
            using PandaBear.BuildingBlocks;
            using PandaBear.Domain;

            namespace Consumer;

            [GenerateModelSupport(Identity = true)]
            public sealed partial class Customer : IIdentified<int>
            {
                public Customer(int id) => Id = id;

                public int Id { get; private init; }
            }

            public static class Program
            {
                public static void Main()
                {
                    var first = new Customer(7);
                    var second = new Customer(7);
                    Console.WriteLine($"equal={first.Equals(second)}");
                }
            }
            """));

        // Act
        var buildOutput = await workspace.Build();
        var runOutput = await workspace.Run();
        var generatedFiles = workspace.GetGeneratedFiles("Consumer.Customer.ModelSupport.g.cs");

        // Assert
        buildOutput.ShouldContain("Build succeeded.", StringComparison.Ordinal);
        runOutput.ShouldContain("equal=True", StringComparison.Ordinal);
        generatedFiles.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task Results_package_bundles_the_source_generator_that_generates_the_error_catalog_in_a_fresh_project()
    {
        // Arrange
        using var workspace = packageFeed.CreateWorkspace("ResultsGeneratedConsumer");
        workspace.WriteProject(
            $$"""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
                <ImplicitUsings>enable</ImplicitUsings>
                <Nullable>enable</Nullable>
                <EmitCompilerGeneratedFiles>true</EmitCompilerGeneratedFiles>
                <CompilerGeneratedFilesOutputPath>Generated</CompilerGeneratedFilesOutputPath>
              </PropertyGroup>
              <ItemGroup>
                <Compile Remove="Generated/**/*.cs" />
              </ItemGroup>
              <ItemGroup>
                {{workspace.GetPackageReference("PandaBear.SharedKernel.Results")}}
              </ItemGroup>
            </Project>
            """,
            ("Consumer.cs", """
            using PandaBear.Results;

            namespace Consumer;

            public static class TourErrors
            {
                public static Result NotFound(Guid id) => Result.NotFound($"Tour {id} was not found.");
            }
            """));

        // Act
        var buildOutput = await workspace.Build();
        var generatedFiles = workspace.GetGeneratedFiles("SharedKernel.Results.Generated.ResultErrorCatalog.g.cs");

        // Assert
        buildOutput.ShouldContain("Build succeeded.", StringComparison.Ordinal);
        generatedFiles.Select(File.ReadAllText).ShouldHaveSingleItem().ShouldContain("consumer-tourerrors-notfound", StringComparison.Ordinal);
    }
}
