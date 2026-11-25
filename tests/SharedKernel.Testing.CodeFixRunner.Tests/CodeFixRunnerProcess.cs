using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json;

namespace SharedKernel.Testing.CodeFixRunner.Tests;

/// <summary>
/// Runs the built code-fix runner with <c>dotnet exec</c> in its own process, which has its own working directory and
/// MSBuild registration.
/// </summary>
internal static class CodeFixRunnerProcess
{
    private static readonly TimeSpan ExitTimeout = TimeSpan.FromMinutes(1);

    /// <summary>Runs the tool and waits at most one minute for it to exit.</summary>
    public static async Task<CodeFixRunnerInvocationResult> Run(string workingDirectory, IReadOnlyList<string> args, CancellationToken ct)
    {
        var startInfo = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        startInfo.Environment["DOTNET_CLI_CONTEXT_VERBOSE"] = "true";
        startInfo.ArgumentList.Add("exec");
        startInfo.ArgumentList.Add(Path.Combine(AppContext.BaseDirectory, "SharedKernel.Testing.CodeFixRunner.dll"));
        foreach (var arg in args)
        {
            startInfo.ArgumentList.Add(arg);
        }

        return await RunProcess(startInfo, ct);
    }

    /// <summary>Counts the helper in the target SDK's evaluated compile items for one configuration.</summary>
    public static async Task<int> CountHelperCompileItems(string projectPath, string configuration, CancellationToken ct)
    {
        var startInfo = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = Path.GetDirectoryName(projectPath)!,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        // Keep the independent compile-membership assertion on the SDK's JSON output.
        startInfo.Environment["DOTNET_CLI_CONTEXT_VERBOSE"] = "false";
        startInfo.Environment["DOTNET_NOLOGO"] = "true";
        startInfo.ArgumentList.Add("msbuild");
        startInfo.ArgumentList.Add(projectPath);
        startInfo.ArgumentList.Add("-nologo");
        startInfo.ArgumentList.Add("-getItem:Compile");
        startInfo.ArgumentList.Add("-p:Configuration=" + configuration);

        var result = await RunProcess(startInfo, ct);
        result.ExitCode.ShouldBe(0, result.Error);
        using var json = JsonDocument.Parse(result.Output);
        return json.RootElement.GetProperty("Items").GetProperty("Compile").EnumerateArray()
            .Count(item => item.GetProperty("Filename").GetString() == "SampleTestsHelpers");
    }

    private static async Task<CodeFixRunnerInvocationResult> RunProcess(ProcessStartInfo startInfo, CancellationToken ct)
    {
        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("The code-fix runner process did not start.");
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(ExitTimeout);
        var output = process.StandardOutput.ReadToEndAsync(timeout.Token);
        var error = process.StandardError.ReadToEndAsync(timeout.Token);
        try
        {
            await process.WaitForExitAsync(timeout.Token);
            return new CodeFixRunnerInvocationResult(process.ExitCode, await output, await error);
        }
        catch (OperationCanceledException)
        {
            await Stop(process);
            throw;
        }
    }

    // Best effort: the cancellation that led here is what the caller sees.
    private static async Task Stop(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                using var terminationTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
                await process.WaitForExitAsync(terminationTimeout.Token);
            }
        }
        catch (Exception exception) when (exception is InvalidOperationException
            or Win32Exception
            or NotSupportedException
            or AggregateException
            or OperationCanceledException)
        {
        }
    }
}
