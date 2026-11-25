using System.Text;
using System.Xml;
using System.Xml.Linq;
using SharedKernel.IO;

namespace SharedKernel.Repositories;

/// <summary>Edits only explicitly selected central declaration spans in a trial checkout.</summary>
internal static class NuGetTrialProps
{
    private const int MaximumBytes = 4 * 1024 * 1024;
    private static readonly UTF8Encoding Utf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    /// <summary>Parses each package and the exact candidate version requested by the caller.</summary>
    /// <exception cref="ArgumentException">The specification is invalid or repeats a package.</exception>
    internal static Dictionary<string, string> ParseUpdates(IReadOnlyList<string> specifications)
    {
        var updates = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var specification in specifications)
        {
            var separator = specification.IndexOf('@', StringComparison.Ordinal);
            if (separator <= 0 || separator == specification.Length - 1
                || !IsPackageId(specification[..separator])
                || !specification[(separator + 1)..].All(static c => char.IsAsciiLetterOrDigit(c) || c is '.' or '+' or '-')
                || !updates.TryAdd(specification[..separator], specification[(separator + 1)..]))
            {
                throw new ArgumentException("Specify each package update once in package@version form with an exact version.");
            }
        }

        return updates;
    }

    /// <summary>Checks the shape of a NuGet package identifier.</summary>
    internal static bool IsPackageId(string package) => package.Length is > 0 and <= 100
        && package[0] != '-'
        && package.All(static c => char.IsAsciiLetterOrDigit(c) || c is '.' or '_' or '-');

    /// <summary>Validates the shape of an exact version returned by the SDK. NuGet supplies the version semantics.</summary>
    internal static bool IsLiteralVersion(string version) => version.Length is > 0 and <= 128
        && char.IsAsciiDigit(version[0]) && version.All(static character => char.IsAsciiLetterOrDigit(character) || character is '.' or '+' or '-');

    /// <summary>Finds single literal owner declarations and records which already use exact range syntax.</summary>
    internal static async Task<HashSet<string>?> ExactDeclarations(string file, string[] owners, CancellationToken ct)
    {
        var document = Parse(await ReadBytes(file, ct).ConfigureAwait(false));
        var declarations = document.Descendants().Where(static node => node.Name.LocalName == "PackageVersion")
            .ToLookup(static node => (string?)node.Attribute("Include") ?? (string?)node.Attribute("Update"), StringComparer.OrdinalIgnoreCase);
        var exact = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var owner in owners)
        {
            var matches = declarations[owner].Take(2).ToArray();
            if (matches.Length != 1 || matches[0].Attribute("Version")?.Value is not { } version)
            {
                return null;
            }

            if (version.Length > 2 && version[0] == '[' && version[^1] == ']' && IsLiteralVersion(version[1..^1]))
            {
                exact.Add(owner);
            }
            else if (!IsLiteralVersion(version))
            {
                return null;
            }
        }

        return exact;
    }

    /// <summary>Checks whether a central declaration for the package is still present in the selected file.</summary>
    internal static async Task<bool> HasDeclaration(string file, string package, CancellationToken ct)
    {
        var bytes = await ReadBytes(file, ct).ConfigureAwait(false);
        return Parse(bytes).Descendants().Any(node => node.Name.LocalName == "PackageVersion"
            && string.Equals((string?)node.Attribute("Include") ?? (string?)node.Attribute("Update"), package, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Replaces the specified version attributes and removes the selected pin if present.</summary>
    /// <exception cref="InvalidOperationException">The selected props file or declaration cannot be edited unambiguously.</exception>
    internal static async Task Apply(string file, string? removePin, IReadOnlyDictionary<string, string> updates, CancellationToken ct)
    {
        var bytes = await ReadBytes(file, ct).ConfigureAwait(false);
        var raw = RepositoryFile.WithoutUtf8ByteOrderMark(bytes);
        var hasBom = raw.Length != bytes.Length;
        var text = Utf8.GetString(raw.Span);
        var document = XDocument.Parse(text, LoadOptions.PreserveWhitespace | LoadOptions.SetLineInfo);
        var declarations = document.Descendants().Where(static node => node.Name.LocalName == "PackageVersion")
            .ToLookup(static node => (string?)node.Attribute("Include") ?? (string?)node.Attribute("Update"), StringComparer.OrdinalIgnoreCase);

        var edits = updates.Select(update => Replace(text, declarations[update.Key], update.Value)).ToList();
        if (removePin is not null && declarations.Contains(removePin))
        {
            edits.Add(Remove(text, declarations[removePin]));
        }

        foreach (var (start, length, replacement) in edits.OrderByDescending(static edit => edit.Start))
        {
            text = text.Remove(start, length).Insert(start, replacement);
        }

        if (edits.Count != 0)
        {
            await File.WriteAllTextAsync(file, text, hasBom ? Encoding.UTF8 : Utf8, ct).ConfigureAwait(false);
        }
    }

    /// <summary>Reads the selected props file within the declaration size limit.</summary>
    /// <exception cref="InvalidOperationException">The file is missing or too large.</exception>
    internal static async Task<byte[]> ReadBytes(string file, CancellationToken ct) =>
        await BoundedReader.ReadFile(file, MaximumBytes, ct).ConfigureAwait(false)
            ?? throw new InvalidOperationException("The selected central props file is missing or too large.");

    /// <summary>Transfers the SDK's version choices onto the original central props formatting.</summary>
    /// <exception cref="InvalidOperationException">The SDK made changes other than replacing central versions.</exception>
    internal static async Task PreserveFormatting(string file, byte[] original, CancellationToken ct)
    {
        var changed = await ReadBytes(file, ct).ConfigureAwait(false);
        if (original.AsSpan().SequenceEqual(changed))
        {
            return;
        }

        var beforeDocument = Parse(original);
        var afterDocument = Parse(changed);
        var before = beforeDocument.Descendants("PackageVersion").ToArray();
        var after = afterDocument.Descendants("PackageVersion").ToArray();
        var replacements = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (originalItem, candidateItem) in before.Zip(after))
        {
            var previousVersion = originalItem.Attribute("Version");
            var proposedVersion = candidateItem.Attribute("Version");
            if (previousVersion is null || proposedVersion is null || previousVersion.Value == proposedVersion.Value)
            {
                continue;
            }

            var id = (string?)originalItem.Attribute("Include") ?? (string?)originalItem.Attribute("Update");
            if (id is null || !replacements.TryAdd(id, proposedVersion.Value))
            {
                throw new InvalidOperationException("The SDK proposal targets ambiguous central declarations.");
            }

            proposedVersion.Value = previousVersion.Value;
        }

        if (!XNode.DeepEquals(beforeDocument, afterDocument))
        {
            throw new InvalidOperationException("The SDK proposal changed more than central version values.");
        }

        await File.WriteAllBytesAsync(file, original, ct).ConfigureAwait(false);
        await Apply(file, null, replacements, ct).ConfigureAwait(false);
    }

    private static XDocument Parse(byte[] bytes) =>
        XDocument.Parse(Utf8.GetString(RepositoryFile.WithoutUtf8ByteOrderMark(bytes).Span));

    private static (int Start, int Length, string Text) Replace(string text, IEnumerable<XElement> declarations, string value)
    {
        var version = Single(declarations).Attribute("Version")
            ?? throw new InvalidOperationException("Select a central declaration with a Version attribute.");
        var start = Offset(text, (IXmlLineInfo)version);
        var equals = text.IndexOf('=', start, StringComparison.Ordinal);
        var quote = equals + 1;
        while (char.IsWhiteSpace(text[quote]))
        {
            quote++;
        }

        var end = text.IndexOf(text[quote], quote + 1, StringComparison.Ordinal);
        return (quote + 1, end - quote - 1, value);
    }

    private static (int Start, int Length, string Text) Remove(string text, IEnumerable<XElement> declarations)
    {
        var element = Single(declarations);
        var start = Offset(text, (IXmlLineInfo)element) - 1;
        var end = TagEnd(text, start);
        if (!element.IsEmpty)
        {
            end = TagEnd(text, text.IndexOf("</PackageVersion", end, StringComparison.Ordinal));
        }

        var lineStart = text.LastIndexOf('\n', start, StringComparison.Ordinal) + 1;
        var lineEnd = text.IndexOf('\n', end, StringComparison.Ordinal);
        if (lineEnd >= 0 && text.AsSpan(lineStart, start - lineStart).IsWhiteSpace()
            && text.AsSpan(end, lineEnd - end).IsWhiteSpace())
        {
            return (lineStart, lineEnd - lineStart + 1, string.Empty);
        }

        return (start, end - start, string.Empty);
    }

    private static XElement Single(IEnumerable<XElement> nodes)
    {
        var choices = nodes.Take(2).ToArray();
        return choices.Length == 1 ? choices[0] : throw new InvalidOperationException(
            "The selected central props file must declare each requested package exactly once.");
    }

    private static int Offset(string value, IXmlLineInfo location)
    {
        var start = 0;
        for (var line = 1; line < location.LineNumber; line++)
        {
            start = value.IndexOf('\n', start, StringComparison.Ordinal) + 1;
        }

        return start + location.LinePosition - 1;
    }

    private static int TagEnd(string text, int position)
    {
        if (position < 0 || text[position] != '<')
        {
            throw new InvalidOperationException("Could not locate the selected central declaration in the props file.");
        }

        for (var index = position + 1; index < text.Length; index++)
        {
            if (text[index] == '>')
            {
                return index + 1;
            }

            if (text[index] is '"' or '\'')
            {
                index = text.IndexOf(text[index], index + 1, StringComparison.Ordinal);
                if (index < 0)
                {
                    break;
                }
            }
        }

        throw new InvalidOperationException("Could not locate the closing delimiter of the central declaration.");
    }
}
