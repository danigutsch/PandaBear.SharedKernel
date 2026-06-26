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
        var firstSources = GeneratorDeterminismTestsHelpers.GetGeneratedSourceMap(firstRun);
        var secondSources = GeneratorDeterminismTestsHelpers.GetGeneratedSourceMap(secondRun);
        var firstDiagnostics = GeneratorDeterminismTestsHelpers.GetDiagnostics(firstRun);
        var secondDiagnostics = GeneratorDeterminismTestsHelpers.GetDiagnostics(secondRun);

        // Assert
        Assert.Equal(firstSources, secondSources);
        Assert.Equal(firstDiagnostics, secondDiagnostics);
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
        var firstSources = GeneratorDeterminismTestsHelpers.GetGeneratedSourceMap(firstRun);
        var secondSources = GeneratorDeterminismTestsHelpers.GetGeneratedSourceMap(secondRun);
        var firstDiagnostics = GeneratorDeterminismTestsHelpers.GetDiagnostics(firstRun);
        var secondDiagnostics = GeneratorDeterminismTestsHelpers.GetDiagnostics(secondRun);

        // Assert
        Assert.Equal(firstSources, secondSources);
        Assert.Equal(firstDiagnostics, secondDiagnostics);
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
        var firstSources = GeneratorDeterminismTestsHelpers.GetGeneratedSourceMap(firstRun);
        var reorderedSources = GeneratorDeterminismTestsHelpers.GetGeneratedSourceMap(reorderedRun);

        // Assert
        Assert.Equal(firstSources, reorderedSources);
    }

}
