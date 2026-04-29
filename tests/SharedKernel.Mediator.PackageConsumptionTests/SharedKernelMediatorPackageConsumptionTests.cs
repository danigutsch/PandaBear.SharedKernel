namespace SharedKernel.Mediator.PackageConsumptionTests;

public sealed class SharedKernelMediatorPackageConsumptionTests(MediatorPackageFeedFixture packageFeed)
    : IClassFixture<MediatorPackageFeedFixture>
{
    [Fact]
    public async Task Abstractions_Package_Can_Be_Consumed_By_A_Fresh_Project()
    {
        // Arrange
        using var workspace = new PackageConsumptionWorkspace(packageFeed, "MediatorAbstractionsConsumer");
        workspace.WriteProject(
            $$"""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
                <ImplicitUsings>enable</ImplicitUsings>
                <Nullable>enable</Nullable>
              </PropertyGroup>
              <ItemGroup>
                {{workspace.GetPackageReference("SharedKernel.Mediator.Abstractions")}}
              </ItemGroup>
            </Project>
            """,
            ("Consumer.cs", """
            using SharedKernel.Mediator;

            namespace Consumer;

            public sealed record LookupTour(string Code) : IQuery<string>;

            public static class Requests
            {
                public static IRequest<string> Create()
                {
                    return new LookupTour("VT-42");
                }
            }
            """));

        // Act
        var buildOutput = await workspace.Build();

        // Assert
        Assert.Contains("Build succeeded.", buildOutput, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Runtime_And_Source_Generator_Packages_Can_Be_Consumed_By_A_Fresh_Project()
    {
        // Arrange
        using var workspace = new PackageConsumptionWorkspace(packageFeed, "MediatorGeneratedConsumer");
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
                {{workspace.GetPackageReference("SharedKernel.Mediator.Abstractions")}}
                {{workspace.GetPackageReference("SharedKernel.Mediator")}}
                {{workspace.GetPackageReference("SharedKernel.Mediator.SourceGenerator", "PrivateAssets=\"all\" IncludeAssets=\"build;analyzers;buildTransitive\"")}}
                <PackageReference Include="Microsoft.Extensions.DependencyInjection" Version="{{packageFeed.DependencyInjectionPackageVersion}}" />
              </ItemGroup>
            </Project>
            """,
            ("Consumer.cs", """
            using Microsoft.Extensions.DependencyInjection;
            using SharedKernel.Mediator;

            namespace Consumer;

            public sealed record LookupTour(string Code) : IQuery<string>;

            public sealed class LookupTourHandler : IQueryHandler<LookupTour, string>
            {
                public ValueTask<string> Handle(LookupTour request, CancellationToken ct)
                {
                    return ValueTask.FromResult(request.Code);
                }
            }

            public static class Bootstrap
            {
                public static IServiceProvider CreateProvider()
                {
                    var services = new ServiceCollection();
                    services.AddSharedKernelMediator();
                    return services.BuildServiceProvider();
                }
            }
            """));

        // Act
        var buildOutput = await workspace.Build();
        var appMediatorFiles = workspace.GetGeneratedFiles("SharedKernel.Mediator.Generated.AppMediator.g.cs");
        var dependencyInjectionFiles = workspace.GetGeneratedFiles("SharedKernel.Mediator.Generated.DependencyInjection.g.cs");

        // Assert
        Assert.Contains("Build succeeded.", buildOutput, StringComparison.Ordinal);
        Assert.NotEmpty(appMediatorFiles);
        Assert.NotEmpty(dependencyInjectionFiles);
    }
}
