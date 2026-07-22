using System.Diagnostics;
using System.Text;

namespace SharedKernel.AuditTrail.GeneratorTests;

internal sealed class AuditTrailPackageConsumptionWorkspace : IDisposable
{
    private readonly string rootPath;
    private readonly string projectDirectory;
    private readonly string projectFilePath;

    public AuditTrailPackageConsumptionWorkspace(AuditTrailPackageFeedFixture packageFeed)
    {
        rootPath = Path.Combine(Path.GetTempPath(), $"audit-trail-consumer-{Guid.NewGuid():N}");
        projectDirectory = Path.Combine(rootPath, "Consumer");
        projectFilePath = Path.Combine(projectDirectory, "Consumer.csproj");
        Directory.CreateDirectory(projectDirectory);
        File.WriteAllText(Path.Combine(rootPath, "NuGet.Config"), CreateNuGetConfig(packageFeed.FeedPath));
        File.WriteAllText(
            projectFilePath,
            $$"""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
                <ImplicitUsings>enable</ImplicitUsings>
                <Nullable>enable</Nullable>
                <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
                <EmitCompilerGeneratedFiles>true</EmitCompilerGeneratedFiles>
                <CompilerGeneratedFilesOutputPath>Generated</CompilerGeneratedFilesOutputPath>
              </PropertyGroup>
              <ItemGroup>
                <Compile Remove="Generated/**/*.cs" />
              </ItemGroup>
              <ItemGroup>
                <PackageReference Include="SharedKernel.AuditTrail.SourceGenerator"
                                  Version="{{packageFeed.PackageVersion}}"
                                  PrivateAssets="all" />
              </ItemGroup>
            </Project>
            """);
        File.WriteAllText(
            Path.Combine(projectDirectory, "Consumer.cs"),
            """
            using Microsoft.Extensions.DependencyInjection;
            using SharedKernel.AuditTrail;
            using SharedKernel.Domain;

            namespace Consumer;

            public sealed record BookingConfirmed(Guid BookingId) : IDomainEvent;

            public sealed record BookingAuditEntry(Guid BookingId, DateTime OccurredAtUtc) : IAuditTrailEntry;

            public static class BookingAuditMappings
            {
                [AuditTrailMapping]
                public static BookingAuditEntry Map(BookingConfirmed domainEvent, DateTimeOffset occurredAt) =>
                    new(domainEvent.BookingId, occurredAt.UtcDateTime);

                public static IServiceCollection Register(IServiceCollection services) =>
                    services.AddGeneratedAuditTrailMappings();
            }
            """);
    }

    public Task<string> Build() =>
        RunDotNet(projectDirectory, "build", projectFilePath, "--nologo", "--verbosity", "normal");

    public string[] GetGeneratedFiles(string fileName) =>
        Directory.GetFiles(projectDirectory, fileName, SearchOption.AllDirectories);

    public void Dispose()
    {
        if (Directory.Exists(rootPath))
        {
            Directory.Delete(rootPath, recursive: true);
        }
    }

    internal static async Task<string> RunDotNet(string workingDirectory, params string[] arguments)
    {
        using var process = new Process();
        process.StartInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (var argument in arguments)
        {
            process.StartInfo.ArgumentList.Add(argument);
        }

        var outputBuilder = new StringBuilder();
        process.OutputDataReceived += (_, eventArgs) =>
        {
            if (eventArgs.Data is not null)
            {
                outputBuilder.AppendLine(eventArgs.Data);
            }
        };
        process.ErrorDataReceived += (_, eventArgs) =>
        {
            if (eventArgs.Data is not null)
            {
                outputBuilder.AppendLine(eventArgs.Data);
            }
        };
        if (!process.Start())
        {
            throw new InvalidOperationException("Failed to start dotnet process.");
        }

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        await process.WaitForExitAsync();
        var output = outputBuilder.ToString();
        return process.ExitCode == 0
            ? output
            : throw new InvalidOperationException(
                $"dotnet {string.Join(' ', arguments)} failed with exit code {process.ExitCode}.{Environment.NewLine}{output}");
    }

    private static string CreateNuGetConfig(string feedPath) =>
        $$"""
        <?xml version="1.0" encoding="utf-8"?>
        <configuration>
          <packageSources>
            <clear />
            <add key="local" value="{{feedPath}}" />
            <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
          </packageSources>
          <packageSourceMapping>
            <packageSource key="local">
              <package pattern="SharedKernel.*" />
            </packageSource>
            <packageSource key="nuget.org">
              <package pattern="*" />
            </packageSource>
          </packageSourceMapping>
        </configuration>
        """;
}
