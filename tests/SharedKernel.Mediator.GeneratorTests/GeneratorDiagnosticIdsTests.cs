namespace SharedKernel.Mediator.GeneratorTests;

[Trait(TestTraits.CapabilityName, TestTraits.DiscoveryCapability)]
public sealed class GeneratorDiagnosticIdsTests
{
    [Fact]
    public void Mediator_Diagnostic_Ids_Remain_Stable()
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
    public void Analyzer_Releases_Unshipped_Lists_The_Current_Mediator_Diagnostic_Ids()
    {
        var analyzerReleaseIds = File.ReadAllLines(GetAnalyzerReleasesPath())
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

    private static string GetAnalyzerReleasesPath()
    {
        return FindRepositoryPathContaining(
            Path.Combine(
                "src",
                "SharedKernel",
                "SharedKernel.Mediator.SourceGenerator",
                "AnalyzerReleases.Unshipped.md"));
    }

    private static string FindRepositoryPathContaining(string relativePath)
    {
        var currentDirectory = new DirectoryInfo(AppContext.BaseDirectory);

        while (currentDirectory is not null)
        {
            var candidatePath = Path.Combine(currentDirectory.FullName, relativePath);
            if (File.Exists(candidatePath))
            {
                return candidatePath;
            }

            currentDirectory = currentDirectory.Parent;
        }

        throw new InvalidOperationException($"Could not locate repository path for '{relativePath}'.");
    }
}
