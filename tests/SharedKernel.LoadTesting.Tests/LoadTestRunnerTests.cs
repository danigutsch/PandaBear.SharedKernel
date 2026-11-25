using System.Net;

namespace SharedKernel.LoadTesting.Tests;

/// <summary>Tests the runner through a recording load generator, a mapped resolver, and a scripted health handler.</summary>
public sealed class LoadTestRunnerTests
{
    [Fact]
    public async Task A_local_run_hands_the_generator_the_target_controlled_and_scenario_variables_in_order()
    {
        // Arrange
        using var repository = new LoadTestRepository();
        repository.Executables.Set("gen", "/opt/gen");
        using var handler = new ScriptedHttpHandler(HttpStatusCode.OK);
        var generator = new RecordingLoadGenerator(exitCode: 99);
        var settings = repository.Settings("smoke", ("APP_BASE_URL", "http://localhost:5000/"), ("APP_VUS", "3"), ("APP_PROFILE", " "));
        settings = new LoadTestSettings
        {
            Root = settings.Root,
            ConfigurationPath = settings.ConfigurationPath,
            Scenario = settings.Scenario,
            Environment = settings.Environment,
            GeneratorArguments = ["--vus", "2"],
        };

        // Act
        var exitCode = await new LoadTestRunner(generator, repository.Executables, handler).Run(settings, repository.Error, TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(99);
        var run = generator.Received.ShouldNotBeNull();
        var directory = repository.RunDirectories().ShouldHaveSingleItem();
        run.Executable.ShouldBe("/opt/gen");
        run.Root.ShouldBe(repository.Root);
        run.RunDirectory.ShouldBe(directory);
        run.ScriptDirectory.ShouldBe(Path.Combine(repository.Root, "perf", "scripts"));
        run.Script.ShouldBe("scenarios/smoke.js");
        run.ScriptPath.ShouldBe(Path.Combine(repository.Root, "perf", "scripts", "scenarios", "smoke.js"));
        run.Container.ShouldBeNull();
        run.Arguments.ShouldBe(["--vus", "2"]);
        run.Variables.ShouldBe(
        [
            new("APP_BASE_URL", "http://localhost:5000"),
            new(RecordingLoadGenerator.ControlledVariable, "on"),
            new("APP_VUS", "3"),
        ]);
        generator.CheckedVariables.ShouldBe(["APP_BASE_URL", RecordingLoadGenerator.ControlledVariable, "APP_PROFILE", "APP_VUS"]);
        repository.Error.ToString().ShouldBe("Results: perf/results/" + Path.GetFileName(directory) + "\n");
        handler.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task A_container_run_resolves_the_engine_and_reaches_a_loopback_target_through_the_host_alias()
    {
        // Arrange
        using var repository = new LoadTestRepository();
        repository.Executables.Set("docker", "/usr/bin/docker");
        using var handler = new ScriptedHttpHandler(HttpStatusCode.OK);
        var generator = new RecordingLoadGenerator();
        const string image = "mirror.example/gen@sha256:fedcba9876543210fedcba9876543210fedcba9876543210fedcba9876543210";
        var settings = repository.Settings("smoke", ("APP_BASE_URL", "http://127.0.0.1:5000"));
        settings = new LoadTestSettings
        {
            Root = settings.Root,
            ConfigurationPath = settings.ConfigurationPath,
            Scenario = settings.Scenario,
            Environment = settings.Environment,
            Container = true,
            Image = image,
        };

        // Act
        await new LoadTestRunner(generator, repository.Executables, handler).Run(settings, repository.Error, TestContext.Current.CancellationToken);

        // Assert
        var run = generator.Received.ShouldNotBeNull();
        run.Executable.ShouldBe("/usr/bin/docker");
        var container = run.Container.ShouldNotBeNull();
        container.Engine.ShouldBe("docker");
        container.Image.ShouldBe(image);
        run.Variables[0].ShouldBe(new("APP_BASE_URL", "http://host.docker.internal:5000"));
    }

    [Fact]
    public async Task A_rejected_argument_fails_before_a_run_directory_exists()
    {
        // Arrange
        using var repository = new LoadTestRepository();
        repository.Executables.Set("gen", "/opt/gen");
        using var handler = new ScriptedHttpHandler(HttpStatusCode.OK);
        var generator = new RecordingLoadGenerator { RejectedArgument = "--bad" };
        var settings = repository.Settings("smoke", ("APP_BASE_URL", "http://localhost:5000"));
        settings = new LoadTestSettings
        {
            Root = settings.Root,
            ConfigurationPath = settings.ConfigurationPath,
            Scenario = settings.Scenario,
            Environment = settings.Environment,
            GeneratorArguments = ["--bad"],
        };

        // Act
        var exception = await Record.ExceptionAsync(() => new LoadTestRunner(generator, repository.Executables, handler).Run(settings, repository.Error, TestContext.Current.CancellationToken));

        // Assert
        exception.ShouldBeOfType<InvalidOperationException>().Message.ShouldBe("rejected --bad");
        repository.RunDirectories().ShouldBe([]);
        generator.Received.ShouldBeNull();
    }

    [Fact]
    public async Task A_scenario_cannot_name_a_variable_that_the_generator_controls()
    {
        // Arrange
        using var repository = new LoadTestRepository(LoadTestRepository.DefaultConfiguration.Replace("\"APP_VUS\"", "\"" + RecordingLoadGenerator.ControlledVariable + "\"", StringComparison.Ordinal));
        using var handler = new ScriptedHttpHandler(HttpStatusCode.OK);

        // Act
        var exception = await Record.ExceptionAsync(() => new LoadTestRunner(new RecordingLoadGenerator(), repository.Executables, handler)
            .Run(repository.Settings(), repository.Error, TestContext.Current.CancellationToken));

        // Assert
        exception.ShouldBeOfType<InvalidOperationException>().Message
            .ShouldBe(".config/load.json: $.scenarios[0].environment[1] names GEN_CONTROLLED, which the tool sets.");
    }

    [Fact]
    public async Task An_image_that_is_not_pinned_by_digest_fails()
    {
        // Arrange
        using var repository = new LoadTestRepository();
        repository.Executables.Set("docker", "/usr/bin/docker");
        using var handler = new ScriptedHttpHandler(HttpStatusCode.OK);
        var settings = new LoadTestSettings
        {
            Root = repository.Root,
            ConfigurationPath = LoadTestRepository.ConfigurationPath,
            Scenario = "smoke",
            Container = true,
            Image = "registry.example/gen:1.0",
        };

        // Act
        var exception = await Record.ExceptionAsync(() => new LoadTestRunner(new RecordingLoadGenerator(), repository.Executables, handler)
            .Run(settings, repository.Error, TestContext.Current.CancellationToken));

        // Assert
        exception.ShouldBeOfType<InvalidOperationException>().Message
            .ShouldBe("--image requires an image reference pinned by digest, such as grafana/k6:<tag>@sha256:<digest>.");
    }

    [Fact]
    public async Task The_target_is_read_from_the_settings_environment_only()
    {
        // Arrange
        using var repository = new LoadTestRepository();
        repository.Executables.Set("gen", "/opt/gen");
        using var handler = new ScriptedHttpHandler(HttpStatusCode.OK);

        // Act
        var exception = await Record.ExceptionAsync(() => new LoadTestRunner(new RecordingLoadGenerator(), repository.Executables, handler)
            .Run(repository.Settings(), repository.Error, TestContext.Current.CancellationToken));

        // Assert
        exception.ShouldBeOfType<InvalidOperationException>().Message.ShouldBe("APP_BASE_URL is required.");
    }

    [Fact]
    public async Task A_host_is_probed_through_the_injected_handler_until_it_answers_a_success_status()
    {
        // Arrange
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using var repository = new LoadTestRepository(LoadTestRepository.HostConfiguration());
        repository.Executables.Set("gen", "/opt/gen");
        var host = repository.AddFake("app-host", "printf '%s\\n' 'http://0.0.0.0:5123' > \"$APP_READY_FILE\"\nexec /bin/sleep 300");
        using var handler = new ScriptedHttpHandler(HttpStatusCode.ServiceUnavailable, HttpStatusCode.Found, HttpStatusCode.OK);
        var generator = new RecordingLoadGenerator();

        // Act
        await new LoadTestRunner(generator, repository.Executables, handler)
            .Run(repository.Settings("upload"), repository.Error, TestContext.Current.CancellationToken);

        // Assert
        handler.Requests.ToArray().ShouldBe([.. Enumerable.Repeat(new Uri("http://127.0.0.1:5123/health"), 3)]);
        host.Arguments().ShouldBe(["--urls", "http://127.0.0.1:0"]);
        generator.Received.ShouldNotBeNull().Variables[0].ShouldBe(new("APP_UPLOAD_URL", "http://127.0.0.1:5123"));
        repository.Error.ToString().ShouldEndWith("Host ready: http://127.0.0.1:5123\n", StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_host_whose_health_check_never_succeeds_times_out_without_running_the_generator()
    {
        // Arrange
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using var repository = new LoadTestRepository(LoadTestRepository.HostConfiguration(healthTimeoutSeconds: 1));
        repository.Executables.Set("gen", "/opt/gen");
        repository.AddFake("app-host", "printf '%s\\n' 'http://127.0.0.1:5123' > \"$APP_READY_FILE\"\nexec /bin/sleep 300");
        using var handler = new ScriptedHttpHandler(HttpStatusCode.ServiceUnavailable);
        var generator = new RecordingLoadGenerator();

        // Act
        var exception = await Record.ExceptionAsync(() => new LoadTestRunner(generator, repository.Executables, handler)
            .Run(repository.Settings("upload"), repository.Error, TestContext.Current.CancellationToken));

        // Assert
        exception.ShouldBeOfType<InvalidOperationException>().Message
            .ShouldStartWith("The host of scenario upload did not answer http://127.0.0.1:5123/health with a success status within 1 seconds.", StringComparison.Ordinal);
        generator.Received.ShouldBeNull();
    }
}
