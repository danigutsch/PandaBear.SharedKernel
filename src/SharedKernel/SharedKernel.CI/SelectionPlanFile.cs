using System.Text.Json;

namespace PandaBear.CI;

/// <summary>Reads and writes the validation plan, <c>selection.json</c>, that test selection writes and a slice run reads.</summary>
public static class SelectionPlanFile
{
    /// <summary>The plan file name in the selection directory.</summary>
    public const string FileName = "selection.json";

    private const int MaxBytes = 16 * 1024 * 1024;

    /// <summary>Reads the plan in <paramref name="directory"/>. Unknown, repeated, and missing properties are refused.</summary>
    /// <param name="directory">The selection directory.</param>
    /// <param name="ct">Cancels reading.</param>
    /// <returns>The plan.</returns>
    /// <exception cref="InvalidDataException">The file is larger than 16 MiB or holds no object.</exception>
    /// <exception cref="JsonException">The file is not a valid plan.</exception>
    /// <exception cref="IOException">The file cannot be read.</exception>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public static async Task<SelectionPlan> Read(string directory, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(directory);
        var bytes = await BoundedFile.Read(Path.Combine(directory, FileName), MaxBytes, FileName, ct).ConfigureAwait(false);
        return JsonSerializer.Deserialize(bytes, SelectionPlanJsonContext.Default.SelectionPlan)
            ?? throw new InvalidDataException(FileName + " must contain a JSON object.");
    }

    /// <summary>Writes <paramref name="plan"/> to <paramref name="directory"/>, replacing an earlier plan.</summary>
    /// <param name="plan">The plan.</param>
    /// <param name="directory">The selection directory, which must exist.</param>
    /// <param name="ct">Cancels writing.</param>
    /// <exception cref="IOException">The file cannot be written.</exception>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public static Task Write(SelectionPlan plan, string directory, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(directory);
        return File.WriteAllBytesAsync(Path.Combine(directory, FileName), JsonSerializer.SerializeToUtf8Bytes(plan, SelectionPlanJsonContext.Default.SelectionPlan), ct);
    }
}
