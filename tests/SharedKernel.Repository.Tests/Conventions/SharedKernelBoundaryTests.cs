using static PandaBear.Repository.Tests.Conventions.SharedKernelBoundaryFiles;

namespace PandaBear.Repository.Tests.Conventions;

public sealed class SharedKernelBoundaryTests
{

    [Fact]
    public void SharedKernel_projects_should_not_reference_product_projects()
    {
        // Arrange
        var repositoryRoot = GetRepositoryRoot();

        // Act
        var unexpectedReferences = FindSharedKernelProductReferences(repositoryRoot);

        // Assert
        unexpectedReferences.ShouldBeEmpty();
    }

    [Fact]
    public void Provider_neutral_sharedkernel_projects_should_not_reference_adapter_packages()
    {
        // Arrange
        var repositoryRoot = GetRepositoryRoot();

        // Act
        var unexpectedReferences = FindProviderNeutralSharedKernelAdapterPackageReferences(repositoryRoot);

        // Assert
        unexpectedReferences.ShouldBeEmpty();
    }

    [Fact]
    public void SharedKernel_entityframeworkcore_adapter_projects_should_name_entityframeworkcore()
    {
        // Arrange
        var repositoryRoot = GetRepositoryRoot();

        // Act
        var unexpectedReferences = FindSharedKernelEntityFrameworkCoreAdapterNamingViolations(repositoryRoot);

        // Assert
        unexpectedReferences.ShouldBeEmpty();
    }

    [Fact]
    public void SharedKernel_project_names_should_not_use_core_as_a_module_segment()
    {
        // Arrange
        var repositoryRoot = GetRepositoryRoot();

        // Act
        var unexpectedProjects = FindSharedKernelCoreSegmentProjectViolations(repositoryRoot);

        // Assert
        unexpectedProjects.ShouldBeEmpty();
    }

    [Fact]
    public void SharedKernel_primary_modules_should_not_reference_descendant_optional_submodules_at_runtime()
    {
        // Arrange
        var repositoryRoot = GetRepositoryRoot();

        // Act
        var unexpectedReferences = FindSharedKernelRuntimeReferencesToDescendantOptionalSubmodules(repositoryRoot);

        // Assert
        unexpectedReferences.ShouldBeEmpty();
    }

    [Fact]
    public void SharedKernel_optional_submodules_should_not_reference_same_family_optional_siblings_at_runtime()
    {
        // Arrange
        var repositoryRoot = GetRepositoryRoot();

        // Act
        var unexpectedReferences = FindSharedKernelRuntimeReferencesToSameFamilyOptionalSiblingSubmodules(repositoryRoot);

        // Assert
        unexpectedReferences.ShouldBeEmpty();
    }

    [Fact]
    public void Observability_backends_are_opt_in_and_route_through_collector_in_library()
    {

        var sharedHostingText = File.ReadAllText(Path.Combine(
            GetRepositoryRoot(),
            "src",
            "SharedKernel",
            "SharedKernel.Aspire.Hosting.Grafana",
            "GrafanaLgtmStackResourceExtensions.cs"));

        var sharedDefaultsText = File.ReadAllText(Path.Combine(
            GetRepositoryRoot(),
            "src",
            "SharedKernel",
            "SharedKernel.Aspire.Hosting.Grafana",
            "GrafanaLgtmStackDefaults.cs"));
        sharedHostingText.ShouldContain("GrafanaLgtmStackDefaults.ResourceNames", StringComparison.Ordinal);
        sharedDefaultsText.ShouldContain("\"opentelemetry-collector\"", StringComparison.Ordinal);
        sharedDefaultsText.ShouldContain("\"grafana\"", StringComparison.Ordinal);
        sharedDefaultsText.ShouldContain("\"loki\"", StringComparison.Ordinal);
        sharedDefaultsText.ShouldContain("\"tempo\"", StringComparison.Ordinal);
        sharedDefaultsText.ShouldContain("\"prometheus\"", StringComparison.Ordinal);
        sharedHostingText.ShouldContain("AddOpenTelemetryCollectorGateway(", StringComparison.Ordinal);
        sharedHostingText.ShouldContain("ConfigureAppForwarding(collector)", StringComparison.Ordinal);
        sharedHostingText.ShouldContain("ResolveCollectorEndpointName", StringComparison.Ordinal);
        (sharedHostingText.Split("ExcludeFromManifest()").Length - 1).ShouldBe(5);
        sharedHostingText.ShouldNotContain("AddContainer(", StringComparison.Ordinal);
        sharedHostingText.ShouldContain("AddGrafana(resourceNames.Grafana", StringComparison.Ordinal);
        sharedHostingText.ShouldContain("AddLoki(resourceNames.Loki", StringComparison.Ordinal);
        sharedHostingText.ShouldContain("AddTempo(resourceNames.Tempo", StringComparison.Ordinal);
        sharedHostingText.ShouldContain("AddPrometheus(", StringComparison.Ordinal);
        sharedDefaultsText.ShouldContain("ASPIRE_ENABLE_OBSERVABILITY_STACK", StringComparison.Ordinal);
    }

    [Fact]
    [Trait(Testing.TestTraitNames.CategoryName, Testing.TestTraitValues.SecurityCategory)]
    public void Every_hosted_profile_routes_through_the_trusted_collector_gateway_in_library()
    {
        var sharedHostingText = File.ReadAllText(Path.Combine(
            GetRepositoryRoot(),
            "src",
            "SharedKernel",
            "SharedKernel.Aspire.Hosting.Grafana",
            "GrafanaLgtmStackResourceExtensions.cs"));
        sharedHostingText.ShouldContain("WithConfig(Path.GetFullPath(privacyConfigurationFile))", StringComparison.Ordinal);
        sharedHostingText.ShouldContain("WithConfig(Path.GetFullPath(routingConfigurationFile))", StringComparison.Ordinal);
    }

    [Fact]
    public void Media_object_storage_source_configuration_is_private_and_pinned_in_library()
    {
        var seaweedFsText = File.ReadAllText(Path.Combine(GetRepositoryRoot(), "src", "SharedKernel", "SharedKernel.Aspire.Hosting.SeaweedFs", "SeaweedFsResourceExtensions.cs"));
        seaweedFsText.ShouldContain("WithImageSHA256", StringComparison.Ordinal);
        seaweedFsText.ShouldContain("isExternal: false", StringComparison.Ordinal);
        seaweedFsText.ShouldContain("-dir=/data", StringComparison.Ordinal);
        seaweedFsText.ShouldContain("-webdav=false", StringComparison.Ordinal);
        seaweedFsText.ShouldContain("-admin.ui=false", StringComparison.Ordinal);
        seaweedFsText.ShouldContain("WithHttpHealthCheck", StringComparison.Ordinal);
        seaweedFsText.ShouldContain("SeaweedFsResource", StringComparison.Ordinal);
        seaweedFsText.ShouldContain("secret: true", StringComparison.Ordinal);
        seaweedFsText.ShouldContain("persist: true", StringComparison.Ordinal);
        seaweedFsText.ShouldContain("AWS_ACCESS_KEY_ID", StringComparison.Ordinal);
        seaweedFsText.ShouldContain("AWS_SECRET_ACCESS_KEY", StringComparison.Ordinal);
        seaweedFsText.ShouldContain("$\"{name}-bucket\"", StringComparison.Ordinal);
        seaweedFsText.ShouldNotContain("viajantes-media", StringComparison.Ordinal);
        seaweedFsText.ShouldNotContain("Catalog__MediaObjectStorage", StringComparison.Ordinal);
        seaweedFsText.ShouldContain("DcpPublisher:ResourceNameSuffix", StringComparison.Ordinal);
    }

    [Fact]
    public void Admin_hosted_test_infrastructure_should_use_the_approved_fixture_and_base_class_model_in_library()
    {
        var repositoryRoot = GetRepositoryRoot();
        var publicSchemaResetText = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "src",
            "SharedKernel",
            "SharedKernel.IntegrationTesting",
            "PostgreSqlPublicSchemaReset.cs"));
        publicSchemaResetText.ShouldContain(
            "public static async Task Reset(DbConnection connection, CancellationToken ct)",
            StringComparison.Ordinal);
    }
    [Fact]
    public void Npgsql_registrations_share_tracing_privacy_configuration_without_disabling_tracing_or_parameter_logging_in_library()
    {
        // Arrange
        var repositoryRoot = GetRepositoryRoot();
        var sharedNpgsql = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "src",
            "SharedKernel",
            "SharedKernel.Npgsql",
            "NpgsqlDataSourceBuilderExtensions.cs"));

        // Assert
        sharedNpgsql.ShouldContain("ConfigureTracingWithoutFirstResponseEvent", StringComparison.Ordinal);
        sharedNpgsql.ShouldContain("EnableFirstResponseEvent(enable: false)", StringComparison.Ordinal);
        sharedNpgsql.ShouldNotContain("DisableTracing = true", StringComparison.Ordinal);
        sharedNpgsql.ShouldNotContain("EnableParameterLogging", StringComparison.Ordinal);
    }

    [Fact]
    public void Admin_projects_and_flow_documentation_should_not_describe_mediator_composition_in_library()
    {
        // Arrange
        var repositoryRoot = GetRepositoryRoot();
        var domainEventsReadme = File.ReadAllText(
            Path.Combine(repositoryRoot, "src", "SharedKernel", "SharedKernel.Domain", "README.md"));
        domainEventsReadme.ShouldNotContain("SharedKernel.Mediator", StringComparison.Ordinal);
    }

    [Fact]
    public void Aspire_test_application_bounds_build_start_and_resource_health_waits()
    {
        // Arrange
        var testApplicationText = File.ReadAllText(Path.Combine(
            GetRepositoryRoot(),
            "src",
            "SharedKernel",
            "SharedKernel.IntegrationTesting",
            "AspireTestApplication.cs"));

        // Act
        var usesBoundedStartup = testApplicationText.Contains("RunWithResourceStartupTimeout", StringComparison.Ordinal)
            && testApplicationText.Contains("BuildAsync(startupCt)", StringComparison.Ordinal)
            && testApplicationText.Contains("await builtApp.StartAsync(startupCt)", StringComparison.Ordinal)
            && testApplicationText.Contains(
                "WaitForResourceHealthyAsync(resourceName, startupCt)",
                StringComparison.Ordinal);

        // Assert
        usesBoundedStartup.ShouldBeTrue();
        testApplicationText.ShouldContain(
            "CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token)",
            StringComparison.Ordinal);
    }

    [Fact]
    public void Aspire_test_application_awaits_uncancellable_resource_teardown()
    {
        // Arrange
        var testApplicationText = File.ReadAllText(Path.Combine(
            GetRepositoryRoot(),
            "src",
            "SharedKernel",
            "SharedKernel.IntegrationTesting",
            "AspireTestApplication.cs"));

        // Act
        var awaitsUncancellableTeardown = testApplicationText.Contains(
            "RunWithResourceTeardownTimeout(app.StopAsync)",
            StringComparison.Ordinal)
            && testApplicationText.Contains("await operation(timeoutCts.Token)", StringComparison.Ordinal)
            && testApplicationText.Contains(
                "CaptureTeardownFailure(() => app.DisposeAsync().AsTask()",
                StringComparison.Ordinal)
            && testApplicationText.Contains(
                "CaptureTeardownFailure(() => appBuilder.DisposeAsync().AsTask()",
                StringComparison.Ordinal);

        // Assert
        awaitsUncancellableTeardown.ShouldBeTrue();
        testApplicationText.ShouldNotContain(".WaitAsync(", StringComparison.Ordinal);
        testApplicationText.ShouldContain("CaptureTeardownFailure", StringComparison.Ordinal);
        testApplicationText.ShouldContain("new AggregateException(", StringComparison.Ordinal);
        testApplicationText.ShouldContain("var teardownFailures = await DisposeAfterFailedStart", StringComparison.Ordinal);
    }

    [Fact]
    public void Aspire_test_application_preserves_aspire_testing_concurrency_defaults()
    {
        // Arrange
        var repositoryRoot = GetRepositoryRoot();
        var testApplicationText = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "src",
            "SharedKernel",
            "SharedKernel.IntegrationTesting",
            "AspireTestApplication.cs"));
        var endpointLeasePath = Path.Combine(
            repositoryRoot,
            "src",
            "SharedKernel",
            "SharedKernel.IntegrationTesting",
            "AspireTestEndpointLease.cs");
        var publicApiText = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "src",
            "SharedKernel",
            "SharedKernel.IntegrationTesting",
            "PublicAPI.Unshipped.txt"));

        // Act
        var usesTestingBuilder = testApplicationText.Contains(
            "DistributedApplicationTestingBuilder",
            StringComparison.Ordinal);

        // Assert
        usesTestingBuilder.ShouldBeTrue();
        File.Exists(endpointLeasePath).ShouldBeFalse();
        testApplicationText.ShouldNotContain("AspireTestEndpointLease", StringComparison.Ordinal);
        testApplicationText.ShouldNotContain("DcpPublisher:RandomizePorts", StringComparison.Ordinal);
        publicApiText.ShouldNotContain(
            "Aspire.Hosting.IDistributedApplicationBuilder! builder",
            StringComparison.Ordinal);
    }

    [Fact]
    public void Observability_stack_uses_pinned_container_images()
    {
        // Arrange
        var sharedHostingText = File.ReadAllText(Path.Combine(
            GetRepositoryRoot(),
            "src",
            "SharedKernel",
            "SharedKernel.Aspire.Hosting.Grafana",
            "GrafanaLgtmStackResourceExtensions.cs"));

        // Act
        var imageDigestCalls = sharedHostingText.Split("WithImageSHA256(").Length - 1;

        // Assert
        imageDigestCalls.ShouldBe(5);
        sharedHostingText.ShouldContain("OpenTelemetryCollectorImageDigest", StringComparison.Ordinal);
        sharedHostingText.ShouldContain("GrafanaImageDigest", StringComparison.Ordinal);
        sharedHostingText.ShouldContain("LokiImageDigest", StringComparison.Ordinal);
        sharedHostingText.ShouldContain("TempoImageDigest", StringComparison.Ordinal);
        sharedHostingText.ShouldContain("PrometheusImageDigest", StringComparison.Ordinal);
    }

    [Fact]
    public void Seaweedfs_uses_named_storage()
    {
        // Arrange
        var seaweedFsText = File.ReadAllText(Path.Combine(
            GetRepositoryRoot(),
            "src",
            "SharedKernel",
            "SharedKernel.Aspire.Hosting.SeaweedFs",
            "SeaweedFsResourceExtensions.cs"));

        // Act
        var usesNamedStorage = seaweedFsText.Contains("DcpPublisher:ResourceNameSuffix", StringComparison.Ordinal)
            && seaweedFsText.Contains("WithVolume(dataVolumeName, \"/data\")", StringComparison.Ordinal);

        // Assert
        usesNamedStorage.ShouldBeTrue();
    }

    [Fact]
    public void SharedKernel_testing_should_not_own_product_or_area_specific_trait_values()
    {
        var sharedKernelTestingRoot = Path.Combine(GetRepositoryRoot(), "src", "SharedKernel", "SharedKernel.Testing");
        var offendingLines = Directory.GetFiles(sharedKernelTestingRoot, "*.cs", SearchOption.AllDirectories)
            .Where(path => !IsGeneratedTestPath(path))
            .SelectMany(FindProductSpecificSharedKernelTestingCoupling)
            .ToArray();

        (offendingLines.Length != 0).ShouldBeFalse(
            $"Expected PandaBear.Testing to stay neutral and leave product or area-specific trait values local, but found:{Environment.NewLine}{string.Join(Environment.NewLine, offendingLines)}");
    }

    [Fact]
    public void PostgreSql_index_health_hosted_service_delays_after_each_completed_collection_cycle()
    {
        // Arrange
        var hostedServicePath = Path.Combine(
            GetRepositoryRoot(),
            "src",
            "SharedKernel",
            "SharedKernel.Observability.Npgsql",
            "PostgreSqlIndexHealthHostedService.cs");
        var hostedServiceText = File.ReadAllText(hostedServicePath);

        // Act
        var waitsAfterCompletedCollection = hostedServiceText.Contains(
            """
                            foreach (var collector in collectors)
                            {
                                _ = await collector.Collect(stoppingToken).ConfigureAwait(false);
                            }

                            using var timer = new PeriodicTimer(registration.Options.PollingInterval);
                            _ = await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false);
            """,
            StringComparison.Ordinal);

        // Assert
        waitsAfterCompletedCollection.ShouldBeTrue();
    }
}
