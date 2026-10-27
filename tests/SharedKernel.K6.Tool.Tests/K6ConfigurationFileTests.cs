namespace PandaBear.K6.Tool.Tests;

/// <summary>Tests loading and validation of the k6 configuration file.</summary>
public sealed class K6ConfigurationFileTests
{
    private const string Host = """
        "host": {
          "command": "app-host",
          "arguments": ["--urls", "{listenUrl}"],
          "readyFileVariable": "APP_READY_FILE",
          "healthPath": "/health",
          "readyTimeoutSeconds": 5,
          "healthTimeoutSeconds": 5
        }
        """;

    [Fact]
    public async Task A_missing_configuration_fails_with_the_file_name()
    {
        // Arrange
        using var repository = new K6TestRepository();
        File.Delete(Path.Combine(repository.Root, ".config", "sharedkernel-k6.json"));

        // Act
        var exitCode = await repository.Invoke(["run", "--scenario", "smoke"], TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(2);
        repository.Error.ToString().ShouldBe(".config/sharedkernel-k6.json does not exist." + Environment.NewLine);
    }

    [Theory]
    [InlineData("""{ "version": 1, "scriptDirectory": "perf/k6", "resultsDirectory": "perf/results", "scenarios": [], "extra": 1 }""", "$.extra")]
    [InlineData("""{ "version": 1, "scriptDirectory": "perf/k6", "resultsDirectory": "perf/results", "scenarios": [{ "name": "smoke", "script": "a.js" }] }""", "$.scenarios[0]")]
    [InlineData("""{ "version": 1, "version": 1, "scriptDirectory": "perf/k6", "resultsDirectory": "perf/results", "scenarios": [] }""", "$.version")]
    [InlineData("""{ "version": 1, /* comment */ "scriptDirectory": "perf/k6", "resultsDirectory": "perf/results", "scenarios": [] }""", "$")]
    public async Task Malformed_json_fails_with_the_json_path(string configuration, string path)
    {
        // Arrange
        using var repository = new K6TestRepository(configuration);

        // Act
        var exitCode = await repository.Invoke(["run", "--scenario", "smoke"], TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(2);
        repository.Error.ToString().ShouldStartWith($".config/sharedkernel-k6.json is not valid at {path}", StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("\"version\": 1", "\"version\": 2", "$.version must be 1.")]
    [InlineData("\"perf/k6\"", "\"/perf/k6\"", "$.scriptDirectory must be a repository-relative path with '/' separators and without empty, '.', or '..' segments.")]
    [InlineData("\"perf/results\"", "\"perf/../results\"", "$.resultsDirectory must be a repository-relative path with '/' separators and without empty, '.', or '..' segments.")]
    [InlineData("\"name\": \"smoke\"", "\"name\": \"Smoke\"", "$.scenarios[0].name must be lowercase letters and digits in words separated by '-'.")]
    [InlineData("\"scenarios/smoke.js\"", "\"../smoke.js\"", "$.scenarios[0].script must be a path relative to $.scriptDirectory with '/' separators and without empty, '.', or '..' segments.")]
    [InlineData("\"APP_BASE_URL\"", "\"1APP\"", "$.scenarios[0].baseUrlVariable must be an environment variable name: letters, digits, and '_', not starting with a digit.")]
    [InlineData("\"APP_PROFILE\"", "\"GH_TOKEN\"", "$.scenarios[0].environment[0] names GH_TOKEN, which the tool never passes to a child process.")]
    [InlineData("\"APP_VUS\"", "\"APP_PROFILE\"", "$.scenarios[0].environment[1] repeats APP_PROFILE.")]
    [InlineData("\"APP_VUS\"", "\"APP_BASE_URL\"", "$.scenarios[0].environment[1] repeats APP_BASE_URL.")]
    [InlineData("\"APP_VUS\"", "\"K6_NO_USAGE_REPORT\"", "$.scenarios[0].environment[1] names K6_NO_USAGE_REPORT, which the tool sets.")]
    [InlineData("\"podman\"", "\"lxc\"", "$.container.engine must be docker or podman.")]
    [InlineData("@sha256:0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef", "", "$.container.image must be an image reference pinned by digest, such as grafana/k6:<tag>@sha256:<digest>.")]
    public async Task Invalid_values_fail_with_the_json_path(string original, string replacement, string message)
    {
        // Arrange
        using var repository = new K6TestRepository(K6TestRepository.DefaultConfiguration.Replace(original, replacement, StringComparison.Ordinal));

        // Act
        var exitCode = await repository.Invoke(["run", "--scenario", "smoke"], TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(2);
        repository.Error.ToString().ShouldBe(".config/sharedkernel-k6.json: " + message + Environment.NewLine);
    }

    [Theory]
    [InlineData("\"app-host\"", "\"./app-host\"", "$.scenarios[0].host.command must be a command name: letters, digits, '.', '_', and '-', without a path.")]
    [InlineData("\"APP_READY_FILE\"", "\"GITHUB_TOKEN\"", "$.scenarios[0].host.readyFileVariable names GITHUB_TOKEN, which the tool never passes to a child process.")]
    [InlineData("\"/health\"", "\"health\"", "$.scenarios[0].host.healthPath must be a path that starts with '/', without a query or fragment.")]
    [InlineData("\"/health\"", "\"//evil.example/health\"", "$.scenarios[0].host.healthPath must be a path that starts with '/', without a query or fragment.")]
    [InlineData("\"readyTimeoutSeconds\": 5", "\"readyTimeoutSeconds\": 0", "$.scenarios[0].host.readyTimeoutSeconds must be from 1 to 3600.")]
    [InlineData("\"healthTimeoutSeconds\": 5", "\"healthTimeoutSeconds\": 3601", "$.scenarios[0].host.healthTimeoutSeconds must be from 1 to 3600.")]
    public async Task Invalid_host_values_fail_with_the_json_path(string original, string replacement, string message)
    {
        // Arrange
        var configuration = K6TestRepository.DefaultConfiguration
            .Replace("\"environment\": [\"APP_PROFILE\", \"APP_VUS\"]", "\"environment\": [\"APP_PROFILE\", \"APP_VUS\"]," + Host, StringComparison.Ordinal)
            .Replace(original, replacement, StringComparison.Ordinal);
        using var repository = new K6TestRepository(configuration);

        // Act
        var exitCode = await repository.Invoke(["run", "--scenario", "smoke"], TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(2);
        repository.Error.ToString().ShouldBe(".config/sharedkernel-k6.json: " + message + Environment.NewLine);
    }

    [Fact]
    public async Task Repeated_scenario_names_fail()
    {
        // Arrange
        var scenario = """{ "name": "smoke", "script": "scenarios/smoke.js", "baseUrlVariable": "APP_BASE_URL" }""";
        using var repository = new K6TestRepository(
            K6TestRepository.DefaultConfiguration.Replace("\"scenarios\": [", "\"scenarios\": [" + scenario + ",", StringComparison.Ordinal));

        // Act
        var exitCode = await repository.Invoke(["run", "--scenario", "smoke"], TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(2);
        repository.Error.ToString().ShouldBe(".config/sharedkernel-k6.json: $.scenarios[1].name repeats the scenario smoke." + Environment.NewLine);
    }

    [Fact]
    public async Task An_unknown_scenario_fails_with_the_configured_names()
    {
        // Arrange
        using var repository = new K6TestRepository();

        // Act
        var exitCode = await repository.Invoke(["run", "--scenario", "stress"], TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(2);
        repository.Error.ToString().ShouldBe(".config/sharedkernel-k6.json defines no scenario stress. Scenarios: smoke." + Environment.NewLine);
    }

    [Fact]
    public async Task Container_runs_require_the_container_section()
    {
        // Arrange
        using var repository = new K6TestRepository(K6TestRepository.DefaultConfiguration.Replace(
            $$"""
              "container": { "engine": "podman", "image": "{{K6TestRepository.Image}}" },
            """,
            string.Empty,
            StringComparison.Ordinal));
        repository.SetEnvironment("APP_BASE_URL", "http://localhost:5000");

        // Act
        var exitCode = await repository.Invoke(["run", "--scenario", "smoke", "--container"], TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(2);
        repository.Error.ToString().ShouldBe(".config/sharedkernel-k6.json: $.container must name the engine and image that --container uses." + Environment.NewLine);
    }
}
