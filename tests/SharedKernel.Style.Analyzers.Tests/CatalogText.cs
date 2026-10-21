using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace PandaBear.Style.Analyzers.Tests;

internal sealed class CatalogText(string content) : AdditionalText
{
    public override string Path => "GovernedLiterals.txt";
    public override SourceText GetText(CancellationToken cancellationToken = default) => SourceText.From(content);
}
