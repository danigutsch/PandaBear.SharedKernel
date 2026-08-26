namespace SharedKernel.Mediator.GeneratorTests;

[Trait(Testing.SharedKernelTestTraitNames.CapabilityName, TestTraits.DiscoveryCapability)]
public sealed class GeneratorDiagnosticIdsTests
{
    [Fact]
    public void Mediator_diagnostic_ids_remain_stable()
    {
        (MediatorDiagnosticIds.MissingHandler).ShouldBe("SKMED001");
        (MediatorDiagnosticIds.MultipleHandlers).ShouldBe("SKMED002");
        (MediatorDiagnosticIds.InvalidHandlerSignature).ShouldBe("SKMED003");
        (MediatorDiagnosticIds.MissingCancellationToken).ShouldBe("SKMED004");
        (MediatorDiagnosticIds.HandlerReturnTypeMismatch).ShouldBe("SKMED005");
        (MediatorDiagnosticIds.MissingCancellationForwarding).ShouldBe("SKMED006");
        (MediatorDiagnosticIds.MissingEnumeratorCancellation).ShouldBe("SKMED007");
        (MediatorDiagnosticIds.NonIteratorStreamHandlerHasCancellationToken).ShouldBe("SKMED008");
        (MediatorDiagnosticIds.InaccessibleRegistrationType).ShouldBe("SKMED010");
        (MediatorDiagnosticIds.MissingModuleMarker).ShouldBe("SKMED011");
        (MediatorDiagnosticIds.DuplicateGeneratedRegistration).ShouldBe("SKMED012");
        (MediatorDiagnosticIds.UnprovenObjectDispatchCoverage).ShouldBe("SKMED013");
        (MediatorDiagnosticIds.InvalidPipelineGenericArity).ShouldBe("SKMED020");
        (MediatorDiagnosticIds.DuplicatePipelineOrder).ShouldBe("SKMED021");
        (MediatorDiagnosticIds.NeverAppliesPipeline).ShouldBe("SKMED022");
        (MediatorDiagnosticIds.UnboundPipelineConstraints).ShouldBe("SKMED023");
        (MediatorDiagnosticIds.NotificationHandlersRequireExplicitOrder).ShouldBe("SKMED200");
        (MediatorDiagnosticIds.DuplicateNotificationHandlerOrder).ShouldBe("SKMED201");
        (MediatorDiagnosticIds.HandlerShouldNotCallSender).ShouldBe("SKMED500");
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

        (orderedAnalyzerReleaseIds).ShouldBe(diagnosticIds);
    }

}
