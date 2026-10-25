using System.CommandLine.Parsing;
using System.Text.RegularExpressions;

namespace PandaBear.K6.Tool.Tests;

/// <summary>Finds the tool command lines that the tool README documents.</summary>
internal static partial class DocumentedCommandLines
{
    private const string ToolCommandName = "sharedkernel-k6";

    /// <summary>Reads the tool README from the repository that contains the test output directory.</summary>
    public static string ReadToolReadme()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "SharedKernel.slnx")))
            {
                return File.ReadAllText(Path.Combine(directory.FullName, "tools", "SharedKernel.K6.Tool", "README.md"));
            }
        }

        throw new InvalidOperationException("SharedKernel.slnx was not found above " + AppContext.BaseDirectory);
    }

    /// <summary>
    /// Returns every line and inline code span that runs the tool, directly or through <c>dotnet tool run</c>, without
    /// the command prefix and a trailing comment.
    /// </summary>
    public static string[] Find(string markdown) =>
    [
        .. markdown
            .Split('\n')
            .Select(static line => line.Trim())
            .SelectMany(static line => InlineCode().Matches(line).Select(static match => match.Groups["code"].Value).Prepend(line))
            .Select(static text => Prefix().Match(text))
            .Where(static match => match.Success)
            .Select(static match => TrailingComment().Replace(match.Groups["arguments"].Value, string.Empty)),
    ];

    /// <summary>Splits a documented command line.</summary>
    public static string[] Arguments(string commandLine) => [.. CommandLineParser.SplitCommandLine(commandLine)];

    [GeneratedRegex("`(?<code>[^`]+)`", RegexOptions.CultureInvariant)]
    private static partial Regex InlineCode();

    [GeneratedRegex(@"\A(?:[A-Z_]+=\S+\s+)*(?:dotnet tool run " + ToolCommandName + " -- |" + ToolCommandName + " )(?<arguments>.+)" + @"\z", RegexOptions.CultureInvariant)]
    private static partial Regex Prefix();

    [GeneratedRegex(@"\s+#.*$", RegexOptions.CultureInvariant)]
    private static partial Regex TrailingComment();
}
