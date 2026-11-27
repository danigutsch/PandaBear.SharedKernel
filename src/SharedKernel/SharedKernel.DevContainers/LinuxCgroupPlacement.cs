using System.Globalization;

namespace SharedKernel.DevContainers;

/// <summary>Reads Linux cgroup membership, explicit slice budgets, and effective process limits.</summary>
internal static class LinuxCgroupPlacement
{
    private const string CgroupRoot = "/sys/fs/cgroup";

    /// <summary>Reports whether the mount information exposes the whole unified hierarchy at its normal path.</summary>
    internal static bool HasUnifiedRootMount(string? mountInfo) =>
        mountInfo?.Split('\n', StringSplitOptions.RemoveEmptyEntries).Any(static line =>
            line.Contains(" - cgroup2 ", StringComparison.Ordinal)
            && line.Split(' ', StringSplitOptions.RemoveEmptyEntries) is [_, _, _, "/", CgroupRoot, ..]) == true;

    /// <summary>Reads one canonical unified membership, ruling out a legacy memory controller.</summary>
    internal static string? ReadUnifiedCgroup(string? membership)
    {
        string? path = null;
        foreach (var line in membership?.Split('\n', StringSplitOptions.RemoveEmptyEntries) ?? [])
        {
            var fields = line.TrimEnd('\r').Split(':', 3);
            if (fields.Length != 3 || fields[1].Split(',').Contains("memory", StringComparer.Ordinal))
            {
                return null;
            }

            if (fields[0] != "0" || fields[1].Length != 0)
            {
                continue;
            }

            if (path is not null || !IsCanonical(fields[2]))
            {
                return null;
            }

            path = fields[2];
        }

        return path;
    }

    /// <summary>Checks that one canonical hierarchy path is strictly below another.</summary>
    internal static bool IsStrictDescendant(string member, string parent) =>
        IsCanonical(member) && IsCanonical(parent)
        && (parent == "/" ? member != "/" : member.StartsWith(parent + "/", StringComparison.Ordinal));

    /// <summary>Reads the effective caps of a slice with explicit finite limits and a launcher below it.</summary>
    /// <param name="parent">Canonical root-relative hierarchy path of the slice.</param>
    /// <param name="launcher">Canonical root-relative hierarchy path of the launcher.</param>
    /// <param name="read">Reads a cgroup file, returning null for a missing file.</param>
    internal static (long MemoryMaximumBytes, long SwapMaximumBytes)? ReadParentLimits(
        string parent, string launcher, Func<string, string?> read) =>
        IsStrictDescendant(launcher, parent) ? ReadLimits(parent, read, requireExplicit: true) : null;

    /// <summary>Reads the tightest visible finite memory and swap caps that apply to the supplied member.</summary>
    /// <param name="member">Canonical root-relative hierarchy path of the member.</param>
    /// <param name="read">Reads a cgroup file, returning null for a missing file.</param>
    internal static (long MemoryMaximumBytes, long SwapMaximumBytes)? ReadEffectiveLimits(
        string member, Func<string, string?> read) =>
        IsCanonical(member) ? ReadLimits(member, read, requireExplicit: false) : null;

    /// <summary>Reads unified membership only if the same process birth is visible before and after the read.</summary>
    internal static string? ReadStableProcessCgroup(string procRoot, int pid, Func<string, string?> read)
    {
        if (pid <= 0)
        {
            return null;
        }

        var process = procRoot.TrimEnd('/') + "/" + pid.ToString(CultureInfo.InvariantCulture);
        var stat = process + "/stat";
        try
        {
            if (read(stat) is not { } first || LinuxProcessStat.ReadLiveStartTime(stat, first) is not { } birth)
            {
                return null;
            }

            var membership = ReadUnifiedCgroup(read(process + "/cgroup"));
            return membership is not null && read(stat) is { } last && LinuxProcessStat.ReadLiveStartTime(stat, last) == birth
                ? membership : null;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            return null;
        }
    }

    private static bool IsCanonical(string path) => path == "/" || (path is ['/', ..]
        && path.Split('/').Skip(1).All(static part => part is not ("" or "." or "..") && !part.Any(char.IsControl)));

    private static (long MemoryMaximumBytes, long SwapMaximumBytes)? ReadLimits(
        string member, Func<string, string?> read, bool requireExplicit)
    {
        long? memoryMaximum = null;
        long? swapMaximum = null;
        var hierarchy = member;
        try
        {
            while (true)
            {
                var atRoot = hierarchy == "/";
                var directory = CgroupRoot + (atRoot ? string.Empty : hierarchy);
                if (!TryReadLimit(read(directory + "/memory.max"), atRoot, out var memory)
                    || !TryReadLimit(read(directory + "/memory.swap.max"), atRoot, out var swap)
                    || (requireExplicit && hierarchy == member && (memory is null or <= 0 || swap is null)))
                {
                    return null;
                }

                memoryMaximum = Tighter(memoryMaximum, memory);
                swapMaximum = Tighter(swapMaximum, swap);
                if (atRoot)
                {
                    return memoryMaximum is { } memoryBytes && swapMaximum is { } swapBytes
                        ? (memoryBytes, swapBytes) : null;
                }

                var separator = hierarchy.LastIndexOf('/', StringComparison.Ordinal);
                hierarchy = separator == 0 ? "/" : hierarchy[..separator];
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static bool TryReadLimit(string? input, bool atRoot, out long? limit)
    {
        limit = null;
        var value = input?.Trim();
        if (value == "max" || (atRoot && value is null))
        {
            return true;
        }

        if (!long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var bytes))
        {
            return false;
        }

        limit = bytes;
        return true;
    }

    private static long? Tighter(long? current, long? next) =>
        next is { } cap ? Math.Min(current ?? cap, cap) : current;
}
