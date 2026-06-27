namespace SharedKernel.Mediator.GeneratorTests;

[Trait(global::SharedKernel.Testing.SharedKernelTestTraitNames.CapabilityName, TestTraits.DispatchCapability)]
public sealed class GeneratorDeterminismTests
{
    [Fact]
    public void Repeat_run_keeps_generated_sources_and_diagnostics_stable_for_notification_handlers()
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
    public void Repeat_run_keeps_generated_sources_and_diagnostics_stable_for_multi_handler_notifications()
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
    public void Reordered_but_equivalent_handler_declarations_keep_generated_output_stable()
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
