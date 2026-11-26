using System.Globalization;
using static SharedKernel.RepoConfig.Tool.ResourceBudgetGuard;

namespace SharedKernel.RepoConfig.Tool;

/// <summary>Reads Linux host memory, memory pressure, visible cgroup-v2 limits, and a selected filesystem.</summary>
internal static class LinuxResourceBudgetSampler
{
    private const string CgroupRoot = "/sys/fs/cgroup";

    /// <summary>Measures the Linux host and the caller's selected scratch directory.</summary>
    internal static Sample Measure(string scratchDirectory) =>
        Measure(ReadOptional, () => ReadScratch(scratchDirectory));

    /// <summary>Measures the same resources using supplied filesystem readers.</summary>
    internal static Sample Measure(Func<string, string?> read, Func<(long? AvailableBytes, string? Filesystem)> scratch)
    {
        var meminfo = read("/proc/meminfo");
        var (cgroupMeasured, cgroupHeadroom) = ReadCgroupHeadroom(read);
        var (scratchAvailable, filesystem) = scratch();
        return new Sample(ReadMeminfo(meminfo, "MemAvailable:"), cgroupMeasured, cgroupHeadroom, scratchAvailable, filesystem,
            ReadMeminfo(meminfo, "Shmem:"), ReadPressure(read("/proc/pressure/memory")));
    }

    private static (bool Measured, long? Headroom) ReadCgroupHeadroom(Func<string, string?> read)
    {
        try
        {
            var memberships = read("/proc/self/cgroup")?.Split('\n', StringSplitOptions.RemoveEmptyEntries) ?? [];
            var membership = memberships.FirstOrDefault(static line => line.StartsWith("0::/", StringComparison.Ordinal));
            var mountLines = read("/proc/self/mountinfo")?.Split('\n', StringSplitOptions.RemoveEmptyEntries) ?? [];
            if (membership is null || !mountLines.Any(IsUnifiedRootMount)
                || memberships.Any(static line => line.Split(':', 3) is [_, var controllers, _]
                    && controllers.Split(',').Contains("memory", StringComparer.Ordinal)))
            {
                return default;
            }

            var hierarchy = membership[3..].TrimEnd('/');
            if (hierarchy.Split('/').Skip(1).Any(static segment => segment is "" or "." or ".."))
            {
                return default;
            }

            var directory = CgroupRoot + hierarchy;
            long? tightest = null;
            while (true)
            {
                var limit = read(directory + "/memory.max")?.Trim();
                if (directory == CgroupRoot && limit is null)
                {
                    // The top-level cgroup has no memory.max on a standard host mount.
                    return (true, tightest);
                }

                if (limit != "max")
                {
                    if (!TryReadNonNegative(limit, out var maximum)
                        || !TryReadNonNegative(read(directory + "/memory.current")?.Trim(), out var current))
                    {
                        return default;
                    }

                    var room = Math.Max(0, maximum - current);
                    tightest = tightest is { } previous ? Math.Min(previous, room) : room;
                }

                if (directory == CgroupRoot)
                {
                    return (true, tightest);
                }

                directory = directory[..directory.LastIndexOf('/', StringComparison.Ordinal)];
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return default;
        }
    }

    private static bool IsUnifiedRootMount(string line)
    {
        if (!line.Contains(" - cgroup2 ", StringComparison.Ordinal))
        {
            return false;
        }

        var fields = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return fields is [_, _, _, "/", CgroupRoot, ..];
    }

    private static long? ReadMeminfo(string? raw, string key)
    {
        var line = raw?.Split('\n').FirstOrDefault(line => line.StartsWith(key, StringComparison.Ordinal));
        var fields = line?.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return fields is [_, var value, "kB"] && TryReadNonNegative(value, out var kib) && kib <= long.MaxValue / 1024
            ? kib * 1024 : null;
    }

    private static decimal? ReadPressure(string? raw)
    {
        var line = raw?.Split('\n').FirstOrDefault(static line => line.StartsWith("some ", StringComparison.Ordinal));
        var average = line?.Split(' ').FirstOrDefault(static field => field.StartsWith("avg10=", StringComparison.Ordinal));
        return average is not null
            && decimal.TryParse(average.AsSpan(6), NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var pressure)
            && pressure is >= 0m and <= 100m
            ? pressure : null;
    }

    private static bool TryReadNonNegative(string? text, out long value) =>
        long.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out value);

    private static string? ReadOptional(string path)
    {
        try
        {
            return File.ReadAllText(path);
        }
        catch (Exception exception) when (exception is FileNotFoundException or DirectoryNotFoundException)
        {
            return null;
        }
        catch (Exception exception) when (!path.StartsWith(CgroupRoot, StringComparison.Ordinal)
            && (exception is IOException or UnauthorizedAccessException))
        {
            return null;
        }
    }

    private static (long? AvailableBytes, string? Filesystem) ReadScratch(string directory)
    {
        try
        {
            var drive = new DriveInfo(directory);
            return (drive.AvailableFreeSpace, ReadFilesystem(drive));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or OverflowException)
        {
            return default;
        }
    }

    private static string? ReadFilesystem(DriveInfo drive)
    {
        try
        {
            var filesystem = drive.DriveFormat;
            return string.IsNullOrWhiteSpace(filesystem) || string.Equals(filesystem, "unknown", StringComparison.OrdinalIgnoreCase)
                ? null : filesystem;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }
}
