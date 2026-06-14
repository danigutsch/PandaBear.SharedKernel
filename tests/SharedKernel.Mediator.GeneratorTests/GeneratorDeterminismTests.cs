using System.Globalization;
using Microsoft.CodeAnalysis;

namespace SharedKernel.Mediator.GeneratorTests;

[Trait(TestTraits.CapabilityName, TestTraits.DispatchCapability)]
public sealed class GeneratorDeterminismTests
{
    [Fact]
    public void Repeat_Run_Keeps_Generated_Sources_And_Diagnostics_Stable_For_Notification_Handlers()
    {
        // Arrange
        var compilation = GeneratorTestHarness.CreateCompilation(
            TestSources.ModuleHeader
            + TestSources.CreateTourWithHandler
            + TestSources.TourCreatedWithHandler
            + TestSources.StreamToursWithHandler);

        // Act
        var firstRun = GeneratorTestHarness.RunGeneratorDriver(compilation);
        var secondRun = GeneratorTestHarness.RunGeneratorDriver(compilation);

        // Assert
        Assert.Equal(GetGeneratedSourceMap(firstRun), GetGeneratedSourceMap(secondRun));
        Assert.Equal(GetDiagnostics(firstRun), GetDiagnostics(secondRun));
    }

    [Fact]
    public void Repeat_Run_Keeps_Generated_Sources_And_Diagnostics_Stable_For_Multi_Handler_Notifications()
    {
        // Arrange
        var compilation = GeneratorTestHarness.CreateCompilation(
            TestSources.ModuleHeader
            + """
            public sealed record TourPublished(int Id) : INotification;

            public sealed class PublishAuditHandler : INotificationHandler<TourPublished>
            {
                public ValueTask Handle(TourPublished notification, CancellationToken ct) => ValueTask.CompletedTask;
            }

            public sealed class PublishProjectionHandler : INotificationHandler<TourPublished>
            {
                public ValueTask Handle(TourPublished notification, CancellationToken ct) => ValueTask.CompletedTask;
            }
            """);

        // Act
        var firstRun = GeneratorTestHarness.RunGeneratorDriver(compilation);
        var secondRun = GeneratorTestHarness.RunGeneratorDriver(compilation);

        // Assert
        Assert.Equal(GetGeneratedSourceMap(firstRun), GetGeneratedSourceMap(secondRun));
        Assert.Equal(GetDiagnostics(firstRun), GetDiagnostics(secondRun));
    }

    [Fact]
    public void Reordered_But_Equivalent_Handler_Declarations_Keep_Generated_Output_Stable()
    {
        // Arrange
        var firstSource = TestSources.ModuleHeader + """
            public sealed record TourPublished(int Id) : INotification;

            public sealed class PublishAuditHandler : INotificationHandler<TourPublished>
            {
                public ValueTask Handle(TourPublished notification, CancellationToken ct) => ValueTask.CompletedTask;
            }

            public sealed class PublishProjectionHandler : INotificationHandler<TourPublished>
            {
                public ValueTask Handle(TourPublished notification, CancellationToken ct) => ValueTask.CompletedTask;
            }
            """;
        var reorderedSource = TestSources.ModuleHeader + """
            public sealed record TourPublished(int Id) : INotification;

            public sealed class PublishProjectionHandler : INotificationHandler<TourPublished>
            {
                public ValueTask Handle(TourPublished notification, CancellationToken ct) => ValueTask.CompletedTask;
            }

            public sealed class PublishAuditHandler : INotificationHandler<TourPublished>
            {
                public ValueTask Handle(TourPublished notification, CancellationToken ct) => ValueTask.CompletedTask;
            }
            """;

        // Act
        var firstRun = GeneratorTestHarness.RunGeneratorDriver(firstSource);
        var reorderedRun = GeneratorTestHarness.RunGeneratorDriver(reorderedSource);

        // Assert
        Assert.Equal(GetGeneratedSourceMap(firstRun), GetGeneratedSourceMap(reorderedRun));
    }

    private static Dictionary<string, string> GetGeneratedSourceMap(GeneratorDriverRunResult runResult)
    {
        return runResult.Results.Single()
            .GeneratedSources
            .OrderBy(static generated => generated.HintName, StringComparer.Ordinal)
            .ToDictionary(
                static generated => generated.HintName,
                static generated => generated.SourceText.ToString(),
                StringComparer.Ordinal);
    }

    private static string[] GetDiagnostics(GeneratorDriverRunResult runResult)
    {
        return runResult.Results.Single()
            .Diagnostics
            .Select(static diagnostic =>
                $"{diagnostic.Id}|{diagnostic.GetMessage(CultureInfo.InvariantCulture)}")
            .ToArray();
    }
}
