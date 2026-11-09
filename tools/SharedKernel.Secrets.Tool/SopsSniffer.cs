using System.Text.Json;

namespace PandaBear.Secrets.Tool;

/// <summary>Decides from a file's content whether it carries SOPS metadata, so a sops-encrypted file with an unexpected name is still judged.</summary>
internal static class SopsSniffer
{
    /// <summary>Returns whether a file with this name could be a SOPS document: a YAML, JSON, dotenv, or ini extension.</summary>
    public static bool IsCandidateExtension(string path)
    {
        var extension = Path.GetExtension(path);
        return extension.Equals(".yaml", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".yml", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".json", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".env", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".ini", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Returns the format whose metadata the library reads for <paramref name="path"/>, or <see langword="null"/> for any other extension.</summary>
    public static SopsFormat? FormatOf(string path)
    {
        var extension = Path.GetExtension(path);
        return extension.Equals(".yaml", StringComparison.OrdinalIgnoreCase) || extension.Equals(".yml", StringComparison.OrdinalIgnoreCase)
            ? SopsFormat.Yaml
            : extension.Equals(".json", StringComparison.OrdinalIgnoreCase) ? SopsFormat.Json : null;
    }

    /// <summary>Returns whether <paramref name="content"/> of the file at <paramref name="path"/> has a top-level <c>sops</c> key or its dotenv or ini equivalent.</summary>
    public static bool LooksLikeSops(string path, string content)
    {
        var text = content.TrimStart('﻿');
        var extension = Path.GetExtension(path);
        if (extension.Equals(".json", StringComparison.OrdinalIgnoreCase))
        {
            return JsonHasSops(text);
        }

        if (extension.Equals(".env", StringComparison.OrdinalIgnoreCase) || extension.Equals(".ini", StringComparison.OrdinalIgnoreCase))
        {
            return text.ReplaceLineEndings("\n").Split('\n').Any(static line => line.StartsWith("sops_", StringComparison.OrdinalIgnoreCase) || line.StartsWith("[sops", StringComparison.OrdinalIgnoreCase));
        }

        return text.ReplaceLineEndings("\n").Split('\n').Any(IsSopsKeyLine);
    }

    private static bool JsonHasSops(string text)
    {
        try
        {
            using var document = JsonDocument.Parse(text);
            return document.RootElement.ValueKind == JsonValueKind.Object
                && document.RootElement.EnumerateObject().Any(static p => string.Equals(p.Name, "sops", StringComparison.OrdinalIgnoreCase));
        }
        catch (JsonException)
        {
            // Not valid JSON, yet it names a sops key: let the reader judge it rather than pass it by.
            return text.Contains("\"sops\"", StringComparison.OrdinalIgnoreCase);
        }
    }

    private static bool IsSopsKeyLine(string line)
    {
        if (line.Length == 0 || line[0] is ' ' or '#')
        {
            return false;
        }

        var colon = line.IndexOf(':', StringComparison.Ordinal);
        return colon > 0 && string.Equals(line[..colon].Trim(' ', '\t', '"', '\''), "sops", StringComparison.OrdinalIgnoreCase);
    }
}
