using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace PandaBear.Style.Analyzers.Tests;

/// <summary>
/// Additional text that presents catalog content as a file named GovernedLiterals.txt.
/// </summary>
/// <param name="content">The catalog text.</param>
internal sealed class CatalogText(string content) : AdditionalText
{
    public override string Path => "GovernedLiterals.txt";
    public override SourceText GetText(CancellationToken cancellationToken = default) => SourceText.From(content);
}
