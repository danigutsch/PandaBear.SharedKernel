namespace PandaBear.Testing.AspNetCore.TestApp;

/// <summary>Runs a Generic Host entry point for worker capture tests.</summary>
internal static class WorkerFixture
{
    private static readonly AsyncLocal<Func<Task>?> BuildCallback = new();
    private static readonly AsyncLocal<Action?> DisposeCallback = new();

    /// <summary>Gets or sets an optional checkpoint before host construction in this test context.</summary>
    internal static Func<Task>? BeforeBuild
    {
        get => BuildCallback.Value;
        set => BuildCallback.Value = value;
    }

    /// <summary>Gets or sets an optional observer of host disposal in this test context.</summary>
    internal static Action? AfterDispose
    {
        get => DisposeCallback.Value;
        set => DisposeCallback.Value = value;
    }

    /// <summary>Runs the selected startup mode and builds the Generic Host for the given command-line arguments.</summary>
    internal static async Task Run(string[] args)
    {
        var mode = args[0];
        if (mode == "exit")
        {
            return;
        }

        if (mode == "fail")
        {
            throw new InvalidOperationException("Worker configuration failed.");
        }

        if (BeforeBuild is { } beforeBuild)
        {
            await beforeBuild();
        }

        var builder = CreateBuilder(args[1..]);
        try
        {
            using var host = builder.Build();
            await host.StartAsync();
            throw new InvalidOperationException("The worker continued past host creation.");
        }
        catch (HostAbortedException) when (mode is "handle-abort" or "fail-after-capture" or "build-twice")
        {
            if (mode == "fail-after-capture")
            {
                throw new InvalidOperationException("Worker entry point failed after capture.");
            }

            if (mode == "build-twice")
            {
                using var secondHost = CreateBuilder(args[1..]).Build();
            }
        }
    }

    private static HostApplicationBuilder CreateBuilder(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(args);
        if (AfterDispose is { } afterDispose)
        {
            builder.Services.AddSingleton<ILoggerProvider>(_ => new WorkerFixtureLoggerProvider(afterDispose));
        }

        return builder;
    }
}
