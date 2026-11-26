using System.Globalization;

namespace SharedKernel.DevContainers;

/// <summary>Reads identity data from Linux process stat records.</summary>
internal static class LinuxProcessStat
{
    /// <summary>Reads field 22, the process start time in kernel clock ticks.</summary>
    /// <exception cref="InvalidOperationException">The stat record or its process start time is invalid.</exception>
    internal static ulong ReadStartTime(string statPath, string stat) => ReadStat(statPath, stat).StartTime;

    /// <summary>Reads the process start time only for a stat record whose state can represent a live workload.</summary>
    /// <exception cref="InvalidOperationException">The stat record or its process start time is invalid.</exception>
    internal static ulong? ReadLiveStartTime(string statPath, string stat)
    {
        var (startTime, state) = ReadStat(statPath, stat);
        return state is 'Z' or 'X' ? null : startTime;
    }

    private static (ulong StartTime, char State) ReadStat(string statPath, string stat)
    {
        stat = stat.TrimEnd('\r', '\n');
        var commandEnd = stat.LastIndexOf(')', StringComparison.Ordinal);
        if (commandEnd < 0 || commandEnd + 2 >= stat.Length)
        {
            throw new InvalidOperationException($"{statPath} has an invalid process stat record");
        }

        var fields = stat[(commandEnd + 2)..].Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (fields.Length <= 19 || !ulong.TryParse(fields[19], NumberStyles.None, CultureInfo.InvariantCulture, out var startTime))
        {
            throw new InvalidOperationException($"{statPath} has an invalid process start time");
        }

        return (startTime, fields[0][0]);
    }
}
