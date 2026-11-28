using System.CommandLine;
using System.Diagnostics;
using System.Text.Json;
using SharedKernel.Processes;
using static SharedKernel.RepoConfig.Tool.ResourceBudgetGuard;

namespace SharedKernel.RepoConfig.Tool;

/// <summary>Exposes the opt-in Linux command budget and its bounded evidence.</summary>
internal static class ResourceBudgetCommandLine
{
    /// <summary>The Linux subcommand that needs time to verify owned process shutdown after a signal.</summary>
    internal const string Name = "run-with-budget";

    private const long BytesPerMiB = 1024 * 1024;
    private const int MaxDelaySeconds = 4_294_967;
    private const int GuardExitCode = 125;
    private const int CancelledExitCode = 130;

    /// <summary>Creates the Linux budget subcommand.</summary>
    internal static Command Create()
    {
        var workingDirectory = new Option<string>("--working-directory")
        {
            Description = "Existing absolute working directory for the trusted command.",
            Required = true,
        };
        var scratch = new Option<string>("--scratch")
        {
            Description = "Existing absolute scratch directory to measure and set as child TMPDIR.",
            Required = true,
        };
        var report = new Option<string>("--report")
        {
            Description = "New absolute file path for the bounded JSON report.",
            Required = true,
        };
        var hostMemory = new Option<long>("--minimum-host-memory-mib")
        {
            Description = "Minimum Linux host MemAvailable in MiB.",
            Required = true,
        };
        var cgroup = new Option<long>("--minimum-cgroup-headroom-mib")
        {
            Description = "Optional minimum remaining headroom in MiB under a finite inherited cgroup-v2 cap.",
        };
        var scratchSpace = new Option<long>("--minimum-scratch-mib")
        {
            Description = "Minimum available space in the scratch filesystem in MiB.",
            Required = true,
        };
        var interval = new Option<int>("--sample-seconds")
        {
            Description = "Seconds between samples while the command is running.",
            Required = true,
        };
        var pressure = new Option<decimal>("--maximum-memory-pressure-percent")
        {
            Description = "Optional maximum Linux host memory PSI some avg10 percentage, from zero to 100.",
        };
        var streak = new Option<int>("--pressure-samples")
        {
            Description = "Consecutive samples above the pressure maximum needed to stop. Defaults to 2.",
            DefaultValueFactory = static _ => 2,
        };
        var rejectMemoryFilesystem = new Option<bool>("--reject-memory-filesystem")
        {
            Description = "Reject tmpfs and ramfs for scratch and require a recognized filesystem type.",
        };
        var cooperativePodmanCleanup = new Option<bool>(ResourceBudgetPodmanAbort.OptionName)
        {
            Description = "Allow nested run-isolated-podman commands to retire their verified private engines before forced shutdown.",
        };
        var child = new Argument<string[]>("command")
        {
            Arity = ArgumentArity.OneOrMore,
            Description = "Trusted executable and literal arguments. Put '--' before the executable.",
        };
        Command command = new(Name, "Run one trusted Linux command within caller-supplied resource floors.")
        {
            workingDirectory, scratch, report, hostMemory, cgroup, scratchSpace,
            interval, pressure, streak, rejectMemoryFilesystem, cooperativePodmanCleanup, child,
        };
        ValidateMiB(hostMemory);
        ValidateMiB(cgroup);
        ValidateMiB(scratchSpace);
        interval.Validators.Add(result =>
        {
            if (result.GetValueOrDefault<int>() is <= 0 or > MaxDelaySeconds)
            {
                result.AddError("--sample-seconds must be between 1 and 4294967.");
            }
        });
        pressure.Validators.Add(result =>
        {
            if (result.GetValueOrDefault<decimal>() is < 0m or > 100m)
            {
                result.AddError("--maximum-memory-pressure-percent must be between 0 and 100.");
            }
        });
        streak.Validators.Add(result =>
        {
            if (result.GetValueOrDefault<int>() < 2)
            {
                result.AddError("--pressure-samples must be at least 2.");
            }
        });
        command.SetAction((parse, ct) => RepoConfigToolApplication.Guard(parse.InvocationConfiguration.Error, () =>
        {
            var limits = new Limits(parse.GetRequiredValue(hostMemory) * BytesPerMiB, parse.GetRequiredValue(scratchSpace) * BytesPerMiB,
                parse.GetResult(cgroup) is null ? null : parse.GetValue(cgroup) * BytesPerMiB)
            {
                MaximumMemoryPressurePercent = parse.GetResult(pressure) is null ? null : parse.GetValue(pressure),
                ConsecutivePressureSamples = parse.GetValue(streak),
                RejectMemoryBackedScratch = parse.GetValue(rejectMemoryFilesystem),
            };
            return Run(parse.GetRequiredValue(workingDirectory), parse.GetRequiredValue(scratch), parse.GetRequiredValue(report),
                limits, TimeSpan.FromSeconds(parse.GetRequiredValue(interval)), parse.GetValue(cooperativePodmanCleanup),
                parse.GetRequiredValue(child), ct);
        }, ct));
        return command;
    }

    private static void ValidateMiB(Option<long> option) =>
        option.Validators.Add(result =>
        {
            if (result.GetValueOrDefault<long>() is <= 0 or > long.MaxValue / BytesPerMiB)
            {
                result.AddError($"{option.Name} requires a positive whole MiB value that fits in a byte count.");
            }
        });

    private static async Task<int> Run(
        string workingDirectory, string scratch, string reportPath, Limits limits, TimeSpan interval,
        bool cooperativePodmanCleanup, string[] arguments, CancellationToken ct)
    {
        if (!OperatingSystem.IsLinux())
        {
            throw new InvalidOperationException("run-with-budget requires Linux.");
        }

        RequireDirectory(workingDirectory, "--working-directory");
        RequireDirectory(scratch, "--scratch");
        if (!Path.IsPathFullyQualified(reportPath))
        {
            throw new ArgumentException("--report requires a new absolute file path.", nameof(reportPath));
        }

        using var reportStream = new FileStream(reportPath, FileMode.CreateNew, FileAccess.Write, FileShare.Read);
        var result = await ResourceBudgetGuard.Run(limits, () => LinuxResourceBudgetSampler.Measure(scratch),
            token => Task.Delay(interval, token), token => Start(workingDirectory, scratch, cooperativePodmanCleanup, arguments, token),
            ct).ConfigureAwait(false);
        WriteReport(reportStream, result, limits, interval);
        return result.Reason switch
        {
            StopReason.None => result.ExitCode.GetValueOrDefault(),
            StopReason.Cancelled => CancelledExitCode,
            _ => GuardExitCode,
        };
    }

    private static Task<ChildResult> Start(
        string workingDirectory, string scratch, bool cooperativePodmanCleanup, string[] arguments, CancellationToken ct)
    {
        string executable;
        try
        {
            executable = PathExecutableResolver.Default.Resolve(arguments[0]);
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            return Task.FromResult(new ChildResult(null, Started: false));
        }

        var startInfo = new ProcessStartInfo(executable) { WorkingDirectory = workingDirectory, UseShellExecute = false };
        startInfo.Environment["TMPDIR"] = scratch;
        foreach (var argument in arguments.Skip(1))
        {
            startInfo.ArgumentList.Add(argument);
        }

        return ResourceBudgetProcess.Run(startInfo, cooperativePodmanCleanup, ct);
    }

    private static void RequireDirectory(string path, string option)
    {
        if (!Path.IsPathFullyQualified(path) || !Directory.Exists(path))
        {
            throw new ArgumentException($"{option} requires an existing absolute directory.", nameof(path));
        }
    }

    private static void WriteReport(Stream stream, Report report, Limits limits, TimeSpan interval)
    {
        using var json = new Utf8JsonWriter(stream);
        json.WriteStartObject();
        json.WriteNumber("schemaVersion", 1);
        json.WriteString("reason", report.Reason.ToString());
        json.WriteBoolean("childStarted", report.Started);
        WriteNumber(json, "childExitCode", report.ExitCode);
        json.WriteNumber("sampleCount", report.SampleCount);
        json.WriteStartObject("limits");
        json.WriteNumber("minimumHostMemoryBytes", limits.MinimumHostMemoryBytes);
        WriteNumber(json, "minimumCgroupHeadroomBytes", limits.MinimumCgroupHeadroomBytes);
        json.WriteNumber("minimumScratchBytes", limits.MinimumScratchBytes);
        json.WriteNumber("sampleSeconds", (long)interval.TotalSeconds);
        json.WriteBoolean("rejectMemoryFilesystem", limits.RejectMemoryBackedScratch);
        WriteNumber(json, "maximumMemoryPressurePercent", limits.MaximumMemoryPressurePercent);
        json.WriteNumber("pressureSamples", limits.ConsecutivePressureSamples);
        json.WriteEndObject();
        WriteSample(json, "initial", report.InitialSample);
        WriteSample(json, "latest", report.LastSample);
        if (report.Cleanup is { } cleanup)
        {
            json.WriteStartObject("cleanup");
            json.WriteBoolean("observationComplete", cleanup.ObservationComplete);
            json.WriteNumber("observedProcesses", cleanup.ObservedProcesses);
            json.WriteNumber("remainingObservedProcesses", cleanup.RemainingObservedProcesses);
            if (cleanup.PodmanTeardown is { } teardown)
            {
                json.WriteString("isolatedPodmanTeardown", teardown.ToString());
            }
            json.WriteEndObject();
        }
        else
        {
            json.WriteNull("cleanup");
        }

        json.WriteEndObject();
    }

    private static void WriteSample(Utf8JsonWriter json, string field, Sample sample)
    {
        json.WriteStartObject(field);
        WriteNumber(json, "hostAvailableBytes", sample.HostAvailableBytes);
        WriteNumber(json, "hostShmemBytes", sample.ShmemBytes);
        json.WriteBoolean("cgroupMeasured", sample.CgroupMeasured);
        WriteNumber(json, "cgroupHeadroomBytes", sample.CgroupHeadroomBytes);
        WriteNumber(json, "scratchAvailableBytes", sample.ScratchAvailableBytes);
        json.WriteString("scratchFilesystem", sample.ScratchFilesystem);
        WriteNumber(json, "memoryPressurePercent", sample.MemoryPressurePercent);
        json.WriteEndObject();
    }

    private static void WriteNumber(Utf8JsonWriter json, string field, decimal? value)
    {
        if (value is { } number)
        {
            json.WriteNumber(field, number);
        }
        else
        {
            json.WriteNull(field);
        }
    }
}
