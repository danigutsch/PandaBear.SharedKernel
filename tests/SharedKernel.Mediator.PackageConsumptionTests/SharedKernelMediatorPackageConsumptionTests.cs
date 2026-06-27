namespace SharedKernel.Mediator.PackageConsumptionTests;

public sealed class SharedKernelMediatorPackageConsumptionTests(MediatorPackageFeedFixture packageFeed)
    : IClassFixture<MediatorPackageFeedFixture>
{
    [Fact]
    public async Task Abstractions_package_can_be_consumed_by_a_fresh_project()
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
    public async Task Runtime_and_source_generator_packages_can_be_consumed_by_a_fresh_project()
    {
        // Arrange
        using var workspace = new PackageConsumptionWorkspace(packageFeed, "MediatorGeneratedConsumer");
        GeneratedMediatorPackageConsumerProjects.Write(workspace, packageFeed);

        // Act
        var buildOutput = await workspace.Build();
        var runOutput = await workspace.Run();
        var appMediatorFiles = workspace.GetGeneratedFiles("SharedKernel.Mediator.Generated.AppMediator.g.cs");
        var dependencyInjectionFiles = workspace.GetGeneratedFiles("SharedKernel.Mediator.Generated.DependencyInjection.g.cs");

        // Assert
        Assert.Contains("Build succeeded.", buildOutput, StringComparison.Ordinal);
        Assert.Contains("result=VT-42", runOutput, StringComparison.Ordinal);
        Assert.NotEmpty(appMediatorFiles);
        Assert.NotEmpty(dependencyInjectionFiles);
    }

    [Fact]
    public async Task Runtime_and_source_generator_packages_can_be_published_by_a_fresh_project()
    {
        // Arrange
        using var workspace = new PackageConsumptionWorkspace(packageFeed, "MediatorPublishedConsumer");
        GeneratedMediatorPackageConsumerProjects.Write(workspace, packageFeed);

        // Act
        var publishOutput = await workspace.Publish("-c", "Release");
        var publishDirectory = workspace.GetPublishDirectory();
        var publishDirectoryExists = Directory.Exists(publishDirectory);

        // Assert
        Assert.NotEmpty(publishOutput);
        Assert.True(publishDirectoryExists);
        Assert.NotEmpty(Directory.GetFiles(publishDirectory, "*", SearchOption.TopDirectoryOnly));
    }

    [Fact]
    public async Task Runtime_and_source_generator_packages_report_aot_publish_metrics()
    {
        // Arrange
        using var workspace = new PackageConsumptionWorkspace(packageFeed, "MediatorAotMetricsConsumer");
        GeneratedMediatorPackageConsumerProjects.Write(workspace, packageFeed);
        var runtimeIdentifier = PackageConsumptionMetrics.GetCurrentRuntimeIdentifier();

        // Act
        var publishOutput = await workspace.Publish(
            "-c",
            "Release",
            "-r",
            runtimeIdentifier,
            "--self-contained",
            "true",
            "-p:PublishAot=true");
        var publishDirectory = workspace.GetPublishDirectory(runtimeIdentifier: runtimeIdentifier);
        var publishedExecutable = workspace.GetPublishedExecutablePath(runtimeIdentifier);
        var generatedSourceSize = PackageConsumptionMetrics.GetGeneratedSourceSize(workspace);
        var trimWarningCount = PackageConsumptionMetrics.CountTrimWarnings(publishOutput);
        var coldStartMeasurement = await PackageConsumptionMetrics.RunPublishedExecutable(publishedExecutable);
        var runtimeMetrics = PackageConsumptionMetrics.ParseRuntimeMetrics(coldStartMeasurement.Output);
        var nativeBinarySize = new FileInfo(publishedExecutable).Length;
        var publishDirectoryExists = Directory.Exists(publishDirectory);
        var publishedExecutableExists = File.Exists(publishedExecutable);

        // Assert
        Assert.NotEmpty(publishOutput);
        Assert.True(publishDirectoryExists);
        Assert.True(publishedExecutableExists);
        Assert.Equal(0, trimWarningCount);
        Assert.True(nativeBinarySize > 0, "Native binary size must be greater than zero.");
        Assert.True(coldStartMeasurement.Duration > TimeSpan.Zero, "Cold start time must be measurable.");
        Assert.True(runtimeMetrics.FirstDispatch > TimeSpan.Zero, "First dispatch time must be measurable.");
        Assert.True(runtimeMetrics.SteadyStateDispatch > TimeSpan.Zero, "Steady-state dispatch time must be measurable.");
        Assert.True(generatedSourceSize > 0, "Generated source size must be greater than zero.");
        Assert.Contains("pipeline-before=LookupTour", coldStartMeasurement.Output, StringComparison.Ordinal);
        Assert.Contains("pipeline-after=LookupTour", coldStartMeasurement.Output, StringComparison.Ordinal);
        Assert.Contains("notification-handled=VT-42", coldStartMeasurement.Output, StringComparison.Ordinal);
        Assert.Contains("stream-count=3", coldStartMeasurement.Output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Analyzer_package_produces_diagnostics_for_missing_cancellation_forwarding()
    {
        // Arrange
        using var workspace = new PackageConsumptionWorkspace(packageFeed, "MediatorAnalyzerConsumer");
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
                {{workspace.GetPackageReference("SharedKernel.Mediator.Analyzers", "PrivateAssets=\"all\" IncludeAssets=\"build;analyzers;buildTransitive\"")}}
              </ItemGroup>
            </Project>
            """,
            ("Consumer.cs", """
            using System.Threading;
            using System.Threading.Tasks;
            using SharedKernel.Mediator;

            namespace Consumer;

            public sealed record FindTour(int Id) : IQuery<string>;

            public sealed class DispatchingService(ISender sender)
            {
                public async Task<string> Dispatch(CancellationToken ct)
                {
                    // SKMED006: ct is available but not forwarded
                    return await sender.Send(new FindTour(1), CancellationToken.None);
                }
            }
            """));

        // Act
        var buildOutput = await workspace.Build();

        // Assert
        Assert.Contains("SKMED006", buildOutput, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Code_fix_package_forwards_available_cancellation_token()
    {
        // Arrange
        using var workspace = new PackageConsumptionWorkspace(packageFeed, "MediatorCodeFixConsumer");
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
                {{workspace.GetPackageReference("SharedKernel.Mediator.Analyzers", "PrivateAssets=\"all\" IncludeAssets=\"build;analyzers;buildTransitive\"")}}
                {{workspace.GetPackageReference("SharedKernel.Mediator.CodeFixes", "PrivateAssets=\"all\" IncludeAssets=\"build;analyzers;buildTransitive\"")}}
              </ItemGroup>
            </Project>
            """,
            ("Consumer.cs", """
            using System.Threading;
            using System.Threading.Tasks;
            using SharedKernel.Mediator;

            namespace Consumer;

            public sealed record FindTour(int Id) : IQuery<string>;

            public sealed class DispatchingService(ISender sender)
            {
                public async Task<string> Dispatch(CancellationToken ct)
                {
                    // SKMED006: ct is available but not forwarded
                    return await sender.Send(new FindTour(1), CancellationToken.None);
                }
            }
            """));

        var buildOutputBefore = await workspace.Build();
        Assert.Contains("SKMED006", buildOutputBefore, StringComparison.Ordinal);

        // Act
        await workspace.Format("SKMED006");
        var buildOutputAfter = await workspace.Build();

        // Assert
        Assert.DoesNotContain("SKMED006", buildOutputAfter, StringComparison.Ordinal);
        Assert.Contains("Build succeeded.", buildOutputAfter, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Abstractions_are_available_transitively_through_runtime_package()
    {
        // Arrange
        using var workspace = new PackageConsumptionWorkspace(packageFeed, "MediatorTransitiveConsumer");
        workspace.WriteProject(
            $$"""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
                <ImplicitUsings>enable</ImplicitUsings>
                <Nullable>enable</Nullable>
              </PropertyGroup>
              <ItemGroup>
                {{workspace.GetPackageReference("SharedKernel.Mediator")}}
              </ItemGroup>
            </Project>
            """,
            ("Consumer.cs", """
            using System.Threading;
            using System.Threading.Tasks;
            using SharedKernel.Mediator;

            namespace Consumer;

            // IQuery<string> comes from SharedKernel.Mediator.Abstractions — transitively referenced
            public sealed record LookupTour(string Code) : IQuery<string>;

            public sealed class LookupTourHandler : IQueryHandler<LookupTour, string>
            {
                public ValueTask<string> Handle(LookupTour request, CancellationToken ct)
                    => ValueTask.FromResult(request.Code);
            }
            """));

        // Act
        var buildOutput = await workspace.Build();

        // Assert
        Assert.Contains("Build succeeded.", buildOutput, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Source_generator_privateassets_prevents_transitive_generation()
    {
        // Arrange
        const string libraryName = "MediatorLibraryModule";
        const string consumerName = "MediatorLibraryConsumer";

        using var workspace = new PackageConsumptionWorkspace(packageFeed, consumerName);

        workspace.WriteAdditionalProject(
            libraryName,
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
                {{workspace.GetPackageReference("SharedKernel.Mediator")}}
                {{workspace.GetPackageReference("SharedKernel.Mediator.SourceGenerator", "PrivateAssets=\"all\" IncludeAssets=\"build;analyzers;buildTransitive\"")}}
              </ItemGroup>
            </Project>
            """,
            ("AssemblyInfo.cs", """
            using SharedKernel.Mediator;

            [assembly: MediatorModule]
            """),
            ("Handler.cs", """
            using System.Threading;
            using System.Threading.Tasks;
            using SharedKernel.Mediator;

            namespace MediatorLibraryModule;

            public sealed record LookupTour(string Code) : IQuery<string>;

            public sealed class LookupTourHandler : IQueryHandler<LookupTour, string>
            {
                public ValueTask<string> Handle(LookupTour request, CancellationToken ct)
                    => ValueTask.FromResult(request.Code);
            }
            """));

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
                {{workspace.GetProjectReference(libraryName)}}
              </ItemGroup>
            </Project>
            """,
            ("Consumer.cs", """
            using System.Threading;
            using System.Threading.Tasks;
            using MediatorLibraryModule;

            namespace Consumer;

            public static class Program
            {
                public static async Task Main()
                {
                    var handler = new LookupTourHandler();
                    var result = await handler.Handle(new LookupTour("VT-42"), CancellationToken.None);
                    System.Console.WriteLine(result);
                }
            }
            """));

        // Act
        await workspace.BuildProject(libraryName);
        var consumerBuildOutput = await workspace.Build();

        // Assert
        Assert.Contains("Build succeeded.", consumerBuildOutput, StringComparison.Ordinal);

        var libraryGeneratedFiles = workspace.GetAdditionalProjectGeneratedFiles(libraryName, "SharedKernel.Mediator.Generated.AppMediator.g.cs");
        var consumerGeneratedFiles = workspace.GetGeneratedFiles("SharedKernel.Mediator.Generated.AppMediator.g.cs");

        Assert.NotEmpty(libraryGeneratedFiles);
        Assert.Empty(consumerGeneratedFiles);
    }

    [Fact]
    public async Task Multi_project_module_composition_registers_all_handlers()
    {
        // Arrange
        const string moduleName = "MediatorModuleA";
        const string appName = "MediatorComposedApp";

        using var workspace = new PackageConsumptionWorkspace(packageFeed, appName);

        workspace.WriteAdditionalProject(
            moduleName,
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
            ("AssemblyInfo.cs", """
            using SharedKernel.Mediator;

            [assembly: MediatorModule]
            """),
            ("Handler.cs", """
            using System.Threading;
            using System.Threading.Tasks;
            using SharedKernel.Mediator;

            namespace MediatorModuleA;

            public sealed record GetTourName(int Id) : IQuery<string>;

            public sealed class GetTourNameHandler : IQueryHandler<GetTourName, string>
            {
                public ValueTask<string> Handle(GetTourName request, CancellationToken ct)
                    => ValueTask.FromResult($"Tour-{request.Id}");
            }
            """));

        var appFiles = new (string FileName, string Content)[]
        {
            ("LookupTour.cs", """
            using System.Threading;
            using System.Threading.Tasks;
            using SharedKernel.Mediator;

            namespace MediatorComposedApp;

            public sealed record LookupTour(string Code) : IQuery<string>;

            public sealed class LookupTourHandler : IQueryHandler<LookupTour, string>
            {
                public ValueTask<string> Handle(LookupTour request, CancellationToken ct)
                    => ValueTask.FromResult(request.Code);
            }
            """),
            ("MediatorHarness.cs", MediatorConsumerSourceTemplates.CreateHarness("MediatorComposedApp")),
            ("Program.cs", """
            using MediatorComposedApp;
            using MediatorModuleA;

            using var harness = MediatorHarness.Create();

            var lookupResult = await harness.Mediator.Send(new LookupTour("VT-42"), CancellationToken.None);
            var getTourResult = await harness.Mediator.Send(new GetTourName(7), CancellationToken.None);

            Console.WriteLine($"lookup={lookupResult}");
            Console.WriteLine($"getTour={getTourResult}");
            """)
        };

        workspace.WriteProject(
            $$"""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
                <OutputType>Exe</OutputType>
                <ImplicitUsings>enable</ImplicitUsings>
                <Nullable>enable</Nullable>
              </PropertyGroup>
              <ItemGroup>
                {{workspace.GetProjectReference(moduleName)}}
                {{workspace.GetPackageReference("SharedKernel.Mediator.Abstractions")}}
                {{workspace.GetPackageReference("SharedKernel.Mediator")}}
                {{workspace.GetPackageReference("SharedKernel.Mediator.SourceGenerator", "PrivateAssets=\"all\" IncludeAssets=\"build;analyzers;buildTransitive\"")}}
                <PackageReference Include="Microsoft.Extensions.DependencyInjection" Version="{{packageFeed.DependencyInjectionPackageVersion}}" />
              </ItemGroup>
            </Project>
            """,
            appFiles);

        // Act
        var buildOutput = await workspace.Build();
        var runOutput = await workspace.Run();

        // Assert
        Assert.Contains("Build succeeded.", buildOutput, StringComparison.Ordinal);
        Assert.Contains("lookup=VT-42", runOutput, StringComparison.Ordinal);
        Assert.Contains("getTour=Tour-7", runOutput, StringComparison.Ordinal);
    }

}
