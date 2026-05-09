using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

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
        WriteGeneratedConsumerProject(workspace);

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
    public async Task Runtime_And_Source_Generator_Packages_Can_Be_Published_By_A_Fresh_Project()
    {
        // Arrange
        using var workspace = new PackageConsumptionWorkspace(packageFeed, "MediatorPublishedConsumer");
        WriteGeneratedConsumerProject(workspace);

        // Act
        var publishOutput = await workspace.Publish("-c", "Release");
        var publishDirectory = workspace.GetPublishDirectory();

        // Assert
        Assert.NotEmpty(publishOutput);
        Assert.True(Directory.Exists(publishDirectory));
        Assert.NotEmpty(Directory.GetFiles(publishDirectory, "*", SearchOption.TopDirectoryOnly));
    }

    [Fact]
    public async Task Runtime_And_Source_Generator_Packages_Report_Aot_Publish_Metrics()
    {
        // Arrange
        using var workspace = new PackageConsumptionWorkspace(packageFeed, "MediatorAotMetricsConsumer");
        WriteGeneratedConsumerProject(workspace);
        var runtimeIdentifier = GetCurrentRuntimeIdentifier();

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
        var generatedSourceSize = GetGeneratedSourceSize(workspace);
        var trimWarningCount = CountTrimWarnings(publishOutput);
        var coldStartMeasurement = await RunPublishedExecutable(publishedExecutable);
        var runtimeMetrics = ParseRuntimeMetrics(coldStartMeasurement.Output);
        var nativeBinarySize = new FileInfo(publishedExecutable).Length;

        // Assert
        Assert.NotEmpty(publishOutput);
        Assert.True(Directory.Exists(publishDirectory));
        Assert.True(File.Exists(publishedExecutable));
        Assert.Equal(0, trimWarningCount);
        Assert.True(nativeBinarySize > 0, "Native binary size must be greater than zero.");
        Assert.True(coldStartMeasurement.Duration > TimeSpan.Zero, "Cold start time must be measurable.");
        Assert.True(runtimeMetrics.FirstDispatch > TimeSpan.Zero, "First dispatch time must be measurable.");
        Assert.True(runtimeMetrics.SteadyStateDispatch > TimeSpan.Zero, "Steady-state dispatch time must be measurable.");
        Assert.True(generatedSourceSize > 0, "Generated source size must be greater than zero.");
    }

    [Fact]
    public async Task Analyzer_Package_Produces_Diagnostics_For_Missing_Cancellation_Forwarding()
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
    public async Task Code_Fix_Package_Forwards_Available_Cancellation_Token()
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
    public async Task Abstractions_Are_Available_Transitively_Through_Runtime_Package()
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
    public async Task Source_Generator_PrivateAssets_Prevents_Transitive_Generation()
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
    public async Task Multi_Project_Module_Composition_Registers_All_Handlers()
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
            ("Program.cs", """
            using MediatorComposedApp;
            using MediatorModuleA;
            using Microsoft.Extensions.DependencyInjection;
            using SharedKernel.Mediator;

            var services = new ServiceCollection();
            services.AddSharedKernelMediator();

            using var provider = services.BuildServiceProvider();
            using var scope = provider.CreateScope();

            var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

            var lookupResult = await mediator.Send(new LookupTour("VT-42"), CancellationToken.None);
            var getTourResult = await mediator.Send(new GetTourName(7), CancellationToken.None);

            Console.WriteLine($"lookup={lookupResult}");
            Console.WriteLine($"getTour={getTourResult}");
            """));

        // Act
        var buildOutput = await workspace.Build();
        var runOutput = await workspace.Run();

        // Assert
        Assert.Contains("Build succeeded.", buildOutput, StringComparison.Ordinal);
        Assert.Contains("lookup=VT-42", runOutput, StringComparison.Ordinal);
        Assert.Contains("getTour=Tour-7", runOutput, StringComparison.Ordinal);
    }

    private void WriteGeneratedConsumerProject(PackageConsumptionWorkspace workspace)
    {
        workspace.WriteProject(
            $$"""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
                <OutputType>Exe</OutputType>
                <ImplicitUsings>enable</ImplicitUsings>
                <Nullable>enable</Nullable>
                <IsAotCompatible>true</IsAotCompatible>
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
            """),
            ("Program.cs", """
            using System.Diagnostics;
            using System.Globalization;
            using Consumer;
            using Microsoft.Extensions.DependencyInjection;
            using SharedKernel.Mediator;

            var services = new ServiceCollection();
            services.AddSharedKernelMediator();

            using var provider = services.BuildServiceProvider();
            using var scope = provider.CreateScope();

            var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

            var firstDispatchStopwatch = Stopwatch.StartNew();
            var firstDispatchResult = await mediator.Send(new LookupTour("VT-42"), CancellationToken.None);
            firstDispatchStopwatch.Stop();

            const int steadyStateIterations = 200;
            var steadyStateStopwatch = Stopwatch.StartNew();

            for (var iteration = 0; iteration < steadyStateIterations; iteration++)
            {
                _ = await mediator.Send(new LookupTour("VT-42"), CancellationToken.None);
            }

            steadyStateStopwatch.Stop();

            Console.WriteLine($"result={firstDispatchResult}");
            Console.WriteLine(FormattableString.Invariant($"first-dispatch-ms={firstDispatchStopwatch.Elapsed.TotalMilliseconds:F4}"));
            Console.WriteLine(FormattableString.Invariant($"steady-state-dispatch-ms={steadyStateStopwatch.Elapsed.TotalMilliseconds / steadyStateIterations:F4}"));
            """));
    }

    private static int CountTrimWarnings(string publishOutput)
    {
        return Regex.Count(publishOutput, @"warning IL\d{4}", RegexOptions.CultureInvariant);
    }

    private static long GetGeneratedSourceSize(PackageConsumptionWorkspace workspace)
    {
        var generatedFiles = new[]
        {
            "SharedKernel.Mediator.Generated.AppMediator.g.cs",
            "SharedKernel.Mediator.Generated.DependencyInjection.g.cs",
            "SharedKernel.Mediator.Generated.DiscoveryReport.g.cs",
            "SharedKernel.Mediator.Generated.GeneratedDispatch.g.cs",
        };

        return generatedFiles
            .SelectMany(workspace.GetGeneratedFiles)
            .Distinct(StringComparer.Ordinal)
            .Sum(static file => new FileInfo(file).Length);
    }

    private static string GetCurrentRuntimeIdentifier()
    {
        string os;

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            os = "win";
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            os = "osx";
        }
        else
        {
            os = "linux";
        }
        var architecture = RuntimeInformation.ProcessArchitecture switch
        {
            Architecture.Arm64 => "arm64",
            Architecture.X64 => "x64",
            _ => throw new InvalidOperationException(
                $"Unsupported architecture for publish validation: {RuntimeInformation.ProcessArchitecture}."),
        };

        return $"{os}-{architecture}";
    }

    private static (TimeSpan FirstDispatch, TimeSpan SteadyStateDispatch) ParseRuntimeMetrics(string output)
    {
        double? firstDispatchMilliseconds = null;
        double? steadyStateMilliseconds = null;

        foreach (var line in output.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries))
        {
            if (line.StartsWith("first-dispatch-ms=", StringComparison.Ordinal))
            {
                firstDispatchMilliseconds = double.Parse(
                    line["first-dispatch-ms=".Length..],
                    CultureInfo.InvariantCulture);
            }

            if (line.StartsWith("steady-state-dispatch-ms=", StringComparison.Ordinal))
            {
                steadyStateMilliseconds = double.Parse(
                    line["steady-state-dispatch-ms=".Length..],
                    CultureInfo.InvariantCulture);
            }
        }

        if (firstDispatchMilliseconds is null || steadyStateMilliseconds is null)
        {
            throw new InvalidOperationException($"Runtime metrics output was incomplete:{Environment.NewLine}{output}");
        }

        return (TimeSpan.FromMilliseconds(firstDispatchMilliseconds.Value), TimeSpan.FromMilliseconds(steadyStateMilliseconds.Value));
    }

    private static async Task<(string Output, TimeSpan Duration)> RunPublishedExecutable(string publishedExecutable)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = publishedExecutable,
            WorkingDirectory = Path.GetDirectoryName(publishedExecutable)!,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        using var process = new Process { StartInfo = startInfo };
        var stopwatch = Stopwatch.StartNew();

        if (!process.Start())
        {
            throw new InvalidOperationException($"Failed to start published executable '{publishedExecutable}'.");
        }

        var standardOutput = await process.StandardOutput.ReadToEndAsync(TestContext.Current.CancellationToken);
        var standardError = await process.StandardError.ReadToEndAsync(TestContext.Current.CancellationToken);
        await process.WaitForExitAsync(TestContext.Current.CancellationToken);
        stopwatch.Stop();

        var output = string.Concat(standardOutput, standardError);

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"Published executable '{publishedExecutable}' failed with exit code {process.ExitCode}.{Environment.NewLine}{output}");
        }

        return (output, stopwatch.Elapsed);
    }
}
