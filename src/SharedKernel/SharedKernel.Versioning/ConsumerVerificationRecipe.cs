using System.Text.Json;
using SharedKernel.ConfigurationFiles;
using SharedKernel.IO;

namespace SharedKernel.Versioning;

/// <summary>Reads an explicitly selected recipe from the verified consumer checkout.</summary>
internal static class ConsumerVerificationRecipe
{
    /// <summary>Reads and binds every recipe step before any command executes.</summary>
    /// <exception cref="ArgumentException">The recipe path, size, command format, or named inputs are invalid.</exception>
    public static async Task<IReadOnlyList<(string[] Arguments, bool HasInputs)>> Read(
        string root, string clone, string recipe, IReadOnlyList<string> bindings, CancellationToken ct)
    {
        var relative = Path.GetRelativePath(root, Path.GetFullPath(recipe, Path.GetFullPath(root))).Replace('\\', '/');
        if (!RepositoryPath.TryParse(relative, out var path))
        {
            throw new ArgumentException("--recipe must name a file inside the consumer repository.");
        }

        var content = await BoundedReader.ReadFile(path.ResolveUnder(clone), JsonConfigurationFile.MaxBytes, ct).ConfigureAwait(false)
            ?? throw new ArgumentException("The consumer verification recipe exceeds the 1 MiB configuration size limit.");
        using var document = JsonDocument.Parse(content);
        var array = document.RootElement;
        if (array.ValueKind != JsonValueKind.Array || array.GetArrayLength() == 0)
        {
            throw new ArgumentException("The consumer verification recipe must be a nonempty array of commands.");
        }

        var inputs = ReadInputs(bindings);
        var used = new HashSet<string>(StringComparer.Ordinal);
        var commands = new List<(string[] Arguments, bool HasInputs)>(array.GetArrayLength());
        foreach (var command in array.EnumerateArray())
        {
            commands.Add(ReadCommand(command, commands.Count + 1, inputs, used));
        }

        foreach (var name in inputs.Keys)
        {
            if (!used.Contains(name))
            {
                throw new ArgumentException($"Unused recipe input '{name}'.");
            }
        }

        return commands;
    }

    private static Dictionary<string, string> ReadInputs(IReadOnlyList<string> bindings)
    {
        var inputs = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var binding in bindings)
        {
            var separator = binding?.IndexOf('=', StringComparison.Ordinal) ?? -1;
            if (binding is null || separator <= 0 || !IsInputName(binding[..separator]))
            {
                throw new ArgumentException("Recipe inputs must use name=value with a lowercase ASCII name.");
            }

            var name = binding[..separator];
            var value = binding[(separator + 1)..];
            if (value.Contains('\0', StringComparison.Ordinal))
            {
                throw new ArgumentException($"Recipe input '{name}' must not contain null characters.");
            }

            if (!inputs.TryAdd(name, value))
            {
                throw new ArgumentException($"Duplicate recipe input '{name}'.");
            }
        }

        return inputs;
    }

    private static (string[] Arguments, bool HasInputs) ReadCommand(
        JsonElement command, int step, Dictionary<string, string> inputs, HashSet<string> used)
    {
        if (command.ValueKind != JsonValueKind.Array || command.GetArrayLength() == 0)
        {
            throw new ArgumentException($"Recipe step {step} must be a nonempty array of strings or named input references.");
        }

        if (command[0].ValueKind == JsonValueKind.Object)
        {
            throw new ArgumentException($"Recipe step {step} must have a literal executable.");
        }

        var arguments = new string[command.GetArrayLength()];
        var index = 0;
        var hasInputs = false;
        foreach (var item in command.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.Object)
            {
                arguments[index++] = ReadInputReference(item, step, inputs, used);
                hasInputs = true;
                continue;
            }

            if (item.ValueKind != JsonValueKind.String || item.GetString() is not { } value || value.Contains('\0', StringComparison.Ordinal))
            {
                throw new ArgumentException($"Recipe step {step} must contain only strings without null characters or named input references.");
            }

            arguments[index++] = value;
        }

        if (string.IsNullOrWhiteSpace(arguments[0]))
        {
            throw new ArgumentException($"Recipe step {step} needs an executable name.");
        }

        return (arguments, hasInputs);
    }

    private static string ReadInputReference(JsonElement item, int step, Dictionary<string, string> inputs, HashSet<string> used)
    {
        if (!item.TryGetProperty("input", out var reference) || item.EnumerateObject().Count() != 1
            || reference.ValueKind != JsonValueKind.String || reference.GetString() is not { } name || !IsInputName(name))
        {
            throw new ArgumentException($"Recipe step {step} has an invalid input reference.");
        }

        if (!inputs.TryGetValue(name, out var value))
        {
            throw new ArgumentException($"Recipe step {step} is missing recipe input '{name}'.");
        }

        used.Add(name);
        return value;
    }

    private static bool IsInputName(string name) => name.Length > 0 && char.IsAsciiLetterLower(name[0])
        && name.All(static character => char.IsAsciiLetterLower(character) || char.IsAsciiDigit(character) || character == '-');
}
