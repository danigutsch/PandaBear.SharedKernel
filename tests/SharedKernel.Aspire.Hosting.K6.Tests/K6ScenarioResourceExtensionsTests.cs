using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;

namespace PandaBear.Aspire.Hosting.K6.Tests;

public sealed class K6ScenarioResourceExtensionsTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("k6-host-tests").FullName;

    public void Dispose() => Directory.Delete(_root, recursive: true);

    [Fact]
    public void AddK6Scenario_pins_the_image_by_tag_and_digest()
    {
        // Arrange
        var builder = DistributedApplication.CreateBuilder([]);

        // Act
        var k6 = builder.AddK6Scenario("smoke", _root, "scenarios/smoke.js", Path.Combine(_root, "results"));

        // Assert
        var image = k6.Resource.Annotations.OfType<ContainerImageAnnotation>().ShouldHaveSingleItem();
        image.Image.ShouldBe("grafana/k6");
        image.SHA256.ShouldBe("8cd78f9d0de5f50bc8821cceecf356d5d9e839e6611c226a3fcf13c591080fbd");
    }

    [Fact]
    public void AddK6Scenario_starts_only_on_request()
    {
        // Arrange
        var builder = DistributedApplication.CreateBuilder([]);

        // Act
        var k6 = builder.AddK6Scenario("smoke", _root, "scenarios/smoke.js", Path.Combine(_root, "results"));

        // Assert
        k6.Resource.Annotations.OfType<ExplicitStartupAnnotation>().Count().ShouldBe(1);
    }

    [Fact]
    public void AddK6Scenario_mounts_scripts_and_library_read_only_and_results_writable()
    {
        // Arrange
        var builder = DistributedApplication.CreateBuilder([]);
        var results = Path.Combine(_root, "results");

        // Act
        var k6 = builder.AddK6Scenario("smoke", _root, "scenarios/smoke.js", results);

        // Assert
        var mounts = k6.Resource.Annotations.OfType<ContainerMountAnnotation>().ToDictionary(mount => mount.Target);
        mounts["/scripts"].Source.ShouldBe(_root);
        mounts["/scripts"].IsReadOnly.ShouldBeTrue();
        mounts["/k6lib"].IsReadOnly.ShouldBeTrue();
        File.Exists(Path.Combine(mounts["/k6lib"].Source!, "auth.js")).ShouldBeTrue();
        File.Exists(Path.Combine(mounts["/k6lib"].Source!, "summary.js")).ShouldBeTrue();
        mounts["/results"].Source.ShouldBe(results);
        mounts["/results"].IsReadOnly.ShouldBeFalse();
        Directory.Exists(results).ShouldBeTrue();
    }

    [Fact]
    public void AddK6Scenario_materializes_the_library_under_the_per_user_data_folder()
    {
        // Arrange
        var builder = DistributedApplication.CreateBuilder([]);

        // Act
        var k6 = builder.AddK6Scenario("smoke", _root, "scenarios/smoke.js", Path.Combine(_root, "results"));

        // Assert
        var library = k6.Resource.Annotations.OfType<ContainerMountAnnotation>().Where(mount => mount.Target == "/k6lib").ShouldHaveSingleItem();
        library.Source.ShouldStartWith(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), StringComparison.Ordinal);
    }

    [Fact]
    public void AddK6Scenario_replaces_a_linked_library_file_without_writing_through_the_link()
    {
        // Arrange
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        var builder = DistributedApplication.CreateBuilder([]);
        var first = builder.AddK6Scenario("smoke", _root, "scenarios/smoke.js", Path.Combine(_root, "results"));
        var library = first.Resource.Annotations.OfType<ContainerMountAnnotation>().Where(mount => mount.Target == "/k6lib").ShouldHaveSingleItem().Source!;
        var decoy = Path.Combine(_root, "decoy.txt");
        File.WriteAllText(decoy, "decoy");
        var authPath = Path.Combine(library, "auth.js");
        File.Delete(authPath);
        File.CreateSymbolicLink(authPath, decoy);

        // Act
        DistributedApplication.CreateBuilder([]).AddK6Scenario("smoke", _root, "scenarios/smoke.js", Path.Combine(_root, "results"));

        // Assert
        File.ReadAllText(decoy).ShouldBe("decoy");
        new FileInfo(authPath).LinkTarget.ShouldBeNull();
        File.ReadAllText(authPath).ShouldContain("getAccessToken", StringComparison.Ordinal);
    }

    [Fact]
    public void AddK6Scenario_makes_only_a_results_directory_it_created_shared_with_the_sticky_bit()
    {
        // Arrange
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        var created = Path.Combine(_root, "created");
        var existing = Directory.CreateDirectory(Path.Combine(_root, "existing")).FullName;
        File.SetUnixFileMode(existing, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);

        // Act
        DistributedApplication.CreateBuilder([]).AddK6Scenario("created", _root, "scenarios/smoke.js", created);
        DistributedApplication.CreateBuilder([]).AddK6Scenario("existing", _root, "scenarios/smoke.js", existing);

        // Assert
        var shared = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute
            | UnixFileMode.GroupRead | UnixFileMode.GroupWrite | UnixFileMode.GroupExecute
            | UnixFileMode.OtherRead | UnixFileMode.OtherWrite | UnixFileMode.OtherExecute | UnixFileMode.StickyBit;
        File.GetUnixFileMode(created).ShouldBe(shared);
        File.GetUnixFileMode(existing).ShouldBe(UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
    }

    [Fact]
    public async Task AddK6Scenario_runs_the_script_from_the_scripts_mount()
    {
        // Arrange
        var builder = DistributedApplication.CreateBuilder([]);

        // Act
        var k6 = builder.AddK6Scenario("smoke", _root, "scenarios/smoke.js", Path.Combine(_root, "results"));

        // Assert
        var (arguments, _) = await K6ResourceInspector.Evaluate(k6.Resource);
        arguments[0].ShouldBe("run");
        arguments[^1].ShouldBe("/scripts/scenarios/smoke.js");
        arguments.ShouldNotContain("--vus");
        arguments.ShouldNotContain("--duration");
    }

    [Fact]
    public async Task WithK6ClientCredentials_passes_the_token_endpoint_client_and_secret_as_environment()
    {
        // Arrange
        var builder = DistributedApplication.CreateBuilder([]);
        var identity = builder.AddContainer("identity", "quay.io/keycloak/keycloak").WithHttpEndpoint(targetPort: 8080, port: 18080, name: "http");
        var secret = builder.AddParameter("k6-client-secret", "secret-value", secret: true);
        var k6 = builder.AddK6Scenario("smoke", _root, "scenarios/smoke.js", Path.Combine(_root, "results"));

        // Act
        k6.WithK6ClientCredentials(identity, "/realms/app/protocol/openid-connect/token", "k6-smoke", secret);

        // Assert
        var environment = await K6ResourceInspector.UnresolvedEnvironment(k6.Resource);
        environment["SK_K6_CLIENT_ID"].ShouldBe("k6-smoke");
        environment["SK_K6_CLIENT_SECRET"].ShouldBe(secret.Resource);
        var host = (EndpointReferenceExpression)environment["SK_K6_TOKEN_HOST"];
        host.Property.ShouldBe(EndpointProperty.HostAndPort);
        host.Endpoint.Resource.Name.ShouldBe("identity");
        ((ReferenceExpression)environment["SK_K6_TOKEN_URL"]).ValueExpression.ShouldEndWith("/realms/app/protocol/openid-connect/token", StringComparison.Ordinal);
        k6.Resource.Annotations.OfType<WaitAnnotation>().Select(wait => wait.Resource.Name).ShouldContain("identity");
    }

    [Fact]
    public void AddK6Scenario_rejects_a_script_outside_the_script_directory()
    {
        // Arrange
        var builder = DistributedApplication.CreateBuilder([]);

        // Act
        Action act = () => builder.AddK6Scenario("smoke", _root, "../escape.js", Path.Combine(_root, "results"));

        // Assert
        act.ShouldThrow<ArgumentException>();
    }

    [Fact]
    public void Library_scripts_never_print_the_token_or_secret()
    {
        // Arrange
        var builder = DistributedApplication.CreateBuilder([]);
        var k6 = builder.AddK6Scenario("smoke", _root, "scenarios/smoke.js", Path.Combine(_root, "results"));
        var library = k6.Resource.Annotations.OfType<ContainerMountAnnotation>().Where(mount => mount.Target == "/k6lib").ShouldHaveSingleItem().Source!;

        // Act
        var source = string.Concat(Directory.GetFiles(library, "*.js").Select(File.ReadAllText));

        // Assert
        source.ShouldNotContain("console.", StringComparison.Ordinal);
    }
}
