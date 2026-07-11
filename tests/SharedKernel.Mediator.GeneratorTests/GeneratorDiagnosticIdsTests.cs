namespace SharedKernel.Mediator.GeneratorTests;

[Trait(global::SharedKernel.Testing.SharedKernelTestTraitNames.CapabilityName, TestTraits.DiscoveryCapability)]
public sealed class GeneratorDiagnosticIdsTests
{
    [Fact]
    public void Mediator_diagnostic_ids_remain_stable()
    {
        TestAssert.Equal("SKMED001", MediatorDiagnosticIds.MissingHandler);
        TestAssert.Equal("SKMED002", MediatorDiagnosticIds.MultipleHandlers);
        TestAssert.Equal("SKMED003", MediatorDiagnosticIds.InvalidHandlerSignature);
        TestAssert.Equal("SKMED004", MediatorDiagnosticIds.MissingCancellationToken);
        TestAssert.Equal("SKMED005", MediatorDiagnosticIds.HandlerReturnTypeMismatch);
        TestAssert.Equal("SKMED006", MediatorDiagnosticIds.MissingCancellationForwarding);
        TestAssert.Equal("SKMED007", MediatorDiagnosticIds.MissingEnumeratorCancellation);
        TestAssert.Equal("SKMED008", MediatorDiagnosticIds.NonIteratorStreamHandlerHasCancellationToken);
        TestAssert.Equal("SKMED010", MediatorDiagnosticIds.InaccessibleRegistrationType);
        TestAssert.Equal("SKMED011", MediatorDiagnosticIds.MissingModuleMarker);
        TestAssert.Equal("SKMED012", MediatorDiagnosticIds.DuplicateGeneratedRegistration);
        TestAssert.Equal("SKMED013", MediatorDiagnosticIds.UnprovenObjectDispatchCoverage);
        TestAssert.Equal("SKMED020", MediatorDiagnosticIds.InvalidPipelineGenericArity);
        TestAssert.Equal("SKMED021", MediatorDiagnosticIds.DuplicatePipelineOrder);
        TestAssert.Equal("SKMED022", MediatorDiagnosticIds.NeverAppliesPipeline);
        TestAssert.Equal("SKMED023", MediatorDiagnosticIds.UnboundPipelineConstraints);
        TestAssert.Equal("SKMED200", MediatorDiagnosticIds.NotificationHandlersRequireExplicitOrder);
        TestAssert.Equal("SKMED201", MediatorDiagnosticIds.DuplicateNotificationHandlerOrder);
        TestAssert.Equal("SKMED500", MediatorDiagnosticIds.HandlerShouldNotCallSender);
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

        TestAssert.Equal(diagnosticIds, orderedAnalyzerReleaseIds);
    }

}
