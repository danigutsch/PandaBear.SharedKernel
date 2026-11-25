using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace SharedKernel.Testing.AspNetCore.Tests;

/// <summary>Exercises real Generic Host entry points through the capture seam.</summary>
[Trait(TestTraitNames.ScopeName, "Integration")]
public sealed class WorkerTestHostTests
{
    [Theory]
    [InlineData("capture")]
    [InlineData("handle-abort")]
    public void Returns_the_built_worker_host_before_it_starts(string mode)
    {
        // Arrange
        var entryPoint = typeof(Program).Assembly;

        // Act
        using var host = WorkerTestHost.Build(entryPoint, "--worker-fixture", mode, "--setting=expected");

        // Assert
        var setting = host.Services.GetRequiredService<IConfiguration>()["setting"];
        var started = host.Services.GetRequiredService<IHostApplicationLifetime>().ApplicationStarted;
        setting.ShouldBe("expected");
        started.IsCancellationRequested.ShouldBeFalse();
    }

    [Theory]
    [InlineData("exit", "The entry point returned without building a host.")]
    [InlineData("fail", "Worker configuration failed.")]
    public void Reports_missing_hosts_and_startup_failures(string mode, string expectedMessage)
    {
        // Arrange
        var entryPoint = typeof(Program).Assembly;

        // Act
        Action capture = () => WorkerTestHost.Build(entryPoint, "--worker-fixture", mode);

        // Assert
        var failure = capture.ShouldThrow<InvalidOperationException>();
        failure.Message.ShouldBe(expectedMessage);
    }

    [Fact]
    public void Rejects_an_assembly_without_a_managed_entry_point()
    {
        // Arrange
        var library = typeof(WorkerTestHost).Assembly;

        // Act
        Action capture = () => WorkerTestHost.Build(library);

        // Assert
        var failure = capture.ShouldThrow<InvalidOperationException>();
        failure.Message.ShouldBe("The assembly has no managed entry point.");
    }

    [Fact]
    public void Rejects_null_assembly_and_argument_array()
    {
        // Arrange
        var entryPoint = typeof(Program).Assembly;

        // Act
        Action missingAssembly = () => WorkerTestHost.Build(null!);
        Action missingArguments = () => WorkerTestHost.Build(entryPoint, null!);

        // Assert
        missingAssembly.ShouldThrow<ArgumentNullException>().ParamName.ShouldBe("assembly");
        missingArguments.ShouldThrow<ArgumentNullException>().ParamName.ShouldBe("args");
    }

    [Fact]
    public void Releases_a_captured_host_when_the_entry_point_fails()
    {
        // Arrange
        var entryPoint = typeof(Program).Assembly;
        using var fixture = new WorkerFixtureScope();

        // Act
        Action capture = () => WorkerTestHost.Build(entryPoint, "--worker-fixture", "fail-after-capture");

        // Assert
        var failure = capture.ShouldThrow<InvalidOperationException>();
        failure.Message.ShouldBe("Worker entry point failed after capture.");
        fixture.Disposals.ShouldBe(1);
    }

    [Fact]
    public void Preserves_both_the_entry_point_and_cleanup_failures()
    {
        // Arrange
        var entryPoint = typeof(Program).Assembly;
        using var fixture = new WorkerFixtureScope(afterDispose: () => throw new IOException("Worker cleanup failed."));

        // Act
        Action capture = () => WorkerTestHost.Build(entryPoint, "--worker-fixture", "fail-after-capture");

        // Assert
        var failures = capture.ShouldThrow<AggregateException>().InnerExceptions;
        failures.Count.ShouldBe(2);
        failures[0].Message.ShouldBe("Worker entry point failed after capture.");
        failures[1].Message.ShouldBe("Worker cleanup failed.");
        fixture.Disposals.ShouldBe(1);
    }

    [Fact]
    public void Releases_both_hosts_if_the_worker_builds_again_after_the_abort()
    {
        // Arrange
        var entryPoint = typeof(Program).Assembly;
        using var fixture = new WorkerFixtureScope();

        // Act
        Action capture = () => WorkerTestHost.Build(entryPoint, "--worker-fixture", "build-twice");

        // Assert
        var failure = capture.ShouldThrow<InvalidOperationException>();
        failure.Message.ShouldBe("The entry point built another host after its first host was captured.");
        fixture.Disposals.ShouldBe(2);
    }

    [Fact]
    public async Task Parallel_workers_capture_their_own_hosts_and_leave_an_unrelated_host_untouched()
    {
        // Arrange
        var entryPoint = typeof(Program).Assembly;
        var ct = TestContext.Current.CancellationToken;
        var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var arrivals = 0;
        using var fixture = new WorkerFixtureScope(beforeBuild: () =>
        {
            if (Interlocked.Increment(ref arrivals) == 2)
            {
                ready.SetResult();
            }

            return release.Task;
        }, releaseWaiters: () => release.TrySetResult());

        // Act
        var first = Task.Run(() => WorkerTestHost.Build(entryPoint, "--worker-fixture", "capture", "--setting=first"), ct);
        var second = Task.Run(() => WorkerTestHost.Build(entryPoint, "--worker-fixture", "capture", "--setting=second"), ct);
        await ready.Task.WaitAsync(ct);
        using var unrelated = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
        {
            DisableDefaults = true
        }).Build();
        release.SetResult();
        using var firstHost = await first;
        using var secondHost = await second;

        // Assert
        firstHost.Services.GetRequiredService<IConfiguration>()["setting"].ShouldBe("first");
        secondHost.Services.GetRequiredService<IConfiguration>()["setting"].ShouldBe("second");
        unrelated.Services.GetRequiredService<IHostApplicationLifetime>().ApplicationStarted.IsCancellationRequested.ShouldBeFalse();
    }
}
