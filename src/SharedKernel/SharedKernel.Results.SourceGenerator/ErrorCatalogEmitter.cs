using System.Text;

namespace SharedKernel.Results.SourceGenerator;

internal static class ErrorCatalogEmitter
{
    public static string Emit(IReadOnlyList<ErrorCatalogEntryModel> entries)
    {
        var builder = new StringBuilder();

        builder.AppendLine("#nullable enable");
        builder.AppendLine("[assembly: SharedKernel.Results.ResultErrorCatalogProviderAttribute(typeof(SharedKernel.Results.GeneratedResultErrorCatalogProvider))]");
        builder.AppendLine();
        builder.AppendLine("namespace SharedKernel.Results;");
        builder.AppendLine();
        builder.AppendLine("internal static class ResultErrorCatalogGenerated");
        builder.AppendLine("{");
        builder.AppendLine("    internal static readonly ResultErrorCatalogEntry[] generatedEntries =");
        builder.AppendLine("    [");

        foreach (var entry in entries)
        {
            builder.Append("        new ResultErrorCatalogEntry(")
                .Append(ToLiteral(entry.Identifier)).Append(", ")
                .Append(ToLiteral(entry.DocumentationPath)).Append(", ")
                .Append(ToLiteral(entry.ProviderType)).Append(", ")
                .Append(ToLiteral(entry.MemberName)).Append(", ")
                .Append("ResultStatus.").Append(entry.Status).Append(", ")
                .Append(entry.HttpStatusCode.ToString(System.Globalization.CultureInfo.InvariantCulture)).Append(", ")
                .Append(ToLiteral(entry.Code)).Append(", ")
                .Append(ToLiteral(entry.DetailTemplate)).Append(", ")
                .Append(entry.Summary is null ? "null" : ToLiteral(entry.Summary))
                .AppendLine("),");
        }

        builder.AppendLine("    ];");
        builder.AppendLine("}");
        builder.AppendLine();
        builder.AppendLine("internal sealed class GeneratedResultErrorCatalogProvider");
        builder.AppendLine("{");
        builder.AppendLine("    public IReadOnlyList<ResultErrorCatalogEntry> Entries => ResultErrorCatalogGenerated.generatedEntries;");
        builder.AppendLine("}");

        return builder.ToString();
    }

    private static string ToLiteral(string value)
    {
        return "\""
            + value
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\r", "\\r")
                .Replace("\n", "\\n")
            + "\"";
    }
}
