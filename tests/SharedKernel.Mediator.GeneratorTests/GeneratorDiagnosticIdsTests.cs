namespace SharedKernel.Mediator.GeneratorTests;

[Trait(global::SharedKernel.Testing.SharedKernelTestTraitNames.CapabilityName, TestTraits.DiscoveryCapability)]
public sealed class GeneratorDiagnosticIdsTests
{
    [Fact]
    public void Mediator_diagnostic_ids_remain_stable()
    {
        Assert.Equal("SKMED001", MediatorDiagnosticIds.MissingHandler);
        Assert.Equal("SKMED002", MediatorDiagnosticIds.MultipleHandlers);
        Assert.Equal("SKMED003", MediatorDiagnosticIds.InvalidHandlerSignature);
        Assert.Equal("SKMED004", MediatorDiagnosticIds.MissingCancellationToken);
        Assert.Equal("SKMED005", MediatorDiagnosticIds.HandlerReturnTypeMismatch);
        Assert.Equal("SKMED006", MediatorDiagnosticIds.MissingCancellationForwarding);
        Assert.Equal("SKMED007", MediatorDiagnosticIds.MissingEnumeratorCancellation);
        Assert.Equal("SKMED008", MediatorDiagnosticIds.NonIteratorStreamHandlerHasCancellationToken);
        Assert.Equal("SKMED010", MediatorDiagnosticIds.InaccessibleRegistrationType);
        Assert.Equal("SKMED011", MediatorDiagnosticIds.MissingModuleMarker);
        Assert.Equal("SKMED012", MediatorDiagnosticIds.DuplicateGeneratedRegistration);
        Assert.Equal("SKMED013", MediatorDiagnosticIds.UnprovenObjectDispatchCoverage);
        Assert.Equal("SKMED020", MediatorDiagnosticIds.InvalidPipelineGenericArity);
        Assert.Equal("SKMED021", MediatorDiagnosticIds.DuplicatePipelineOrder);
        Assert.Equal("SKMED022", MediatorDiagnosticIds.NeverAppliesPipeline);
        Assert.Equal("SKMED023", MediatorDiagnosticIds.UnboundPipelineConstraints);
        Assert.Equal("SKMED200", MediatorDiagnosticIds.NotificationHandlersRequireExplicitOrder);
        Assert.Equal("SKMED201", MediatorDiagnosticIds.DuplicateNotificationHandlerOrder);
        Assert.Equal("SKMED500", MediatorDiagnosticIds.HandlerShouldNotCallSender);
    }

    [Fact]
    public void Analyzer_releases_unshipped_lists_the_current_mediator_diagnostic_ids()
    {
        var analyzerReleaseIds = File.ReadAllLines(GeneratorDiagnosticIdsTestsHelpers.GetAnalyzerReleasesPath())
            .Select(static line => line.Trim())
            .Where(static line => line.StartsWith("SKMED", StringComparison.Ordinal))
            .Select(static line => line.Split('|', StringSplitOptions.TrimEntries)[0])
            .ToArray();

        var diagnosticIds = typeof(MediatorDiagnosticIds)
            .GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
            .Where(static field => field.FieldType == typeof(string))
            .Select(static field => (string?)field.GetValue(null))
            .OfType<string>()
            .OrderBy(static value => value, StringComparer.Ordinal)
            .ToArray();

        var orderedAnalyzerReleaseIds = analyzerReleaseIds
            .OrderBy(static value => value, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(diagnosticIds, orderedAnalyzerReleaseIds);
    }

}
