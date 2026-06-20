using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace SharedKernel.Results.SourceGenerator;

internal static class ErrorCatalogModelBuilder
{
    public static ImmutableArray<ErrorCatalogEntryModel> Build(Compilation compilation, CancellationToken cancellationToken)
    {
        var entries = ImmutableArray.CreateBuilder<ErrorCatalogEntryModel>();

        foreach (var syntaxTree in compilation.SyntaxTrees)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var semanticModel = compilation.GetSemanticModel(syntaxTree);
            var root = syntaxTree.GetRoot(cancellationToken);

            foreach (var classDeclaration in root.DescendantNodes().OfType<ClassDeclarationSyntax>())
            {
                if (!classDeclaration.Identifier.ValueText.EndsWith("Errors", StringComparison.Ordinal))
                {
                    continue;
                }

                if (semanticModel.GetDeclaredSymbol(classDeclaration, cancellationToken) is not INamedTypeSymbol typeSymbol || !typeSymbol.IsStatic)
                {
                    continue;
                }

                foreach (var member in classDeclaration.Members)
                {
                    if (TryBuildEntry(typeSymbol, member, semanticModel, cancellationToken) is { } entry)
                    {
                        entries.Add(entry);
                    }
                }
            }
        }

        return [.. entries.OrderBy(static entry => entry.Identifier, StringComparer.Ordinal)];
    }

    private static ErrorCatalogEntryModel? TryBuildEntry(INamedTypeSymbol providerType, MemberDeclarationSyntax member, SemanticModel semanticModel, CancellationToken cancellationToken)
    {
        return member switch
        {
            MethodDeclarationSyntax method => BuildFromMethod(providerType, method, semanticModel, cancellationToken),
            PropertyDeclarationSyntax property => BuildFromProperty(providerType, property, semanticModel, cancellationToken),
            _ => null,
        };
    }

    private static ErrorCatalogEntryModel? BuildFromMethod(INamedTypeSymbol providerType, MethodDeclarationSyntax method, SemanticModel semanticModel, CancellationToken cancellationToken)
    {
        if (semanticModel.GetDeclaredSymbol(method, cancellationToken) is not IMethodSymbol methodSymbol
            || !methodSymbol.IsStatic
            || !ReturnsResult(methodSymbol.ReturnType))
        {
            return null;
        }

        var expression = method.ExpressionBody?.Expression;
        if (expression is null && method.Body?.Statements.Count == 1 && method.Body.Statements[0] is ReturnStatementSyntax returnStatement)
        {
            expression = returnStatement.Expression;
        }

        return BuildEntry(providerType, methodSymbol, expression, semanticModel, cancellationToken);
    }

    private static ErrorCatalogEntryModel? BuildFromProperty(INamedTypeSymbol providerType, PropertyDeclarationSyntax property, SemanticModel semanticModel, CancellationToken cancellationToken)
    {
        if (semanticModel.GetDeclaredSymbol(property, cancellationToken) is not IPropertySymbol propertySymbol
            || !propertySymbol.IsStatic
            || !ReturnsResult(propertySymbol.Type))
        {
            return null;
        }

        return BuildEntry(providerType, propertySymbol, property.ExpressionBody?.Expression, semanticModel, cancellationToken);
    }

    private static ErrorCatalogEntryModel? BuildEntry(INamedTypeSymbol providerType, ISymbol memberSymbol, ExpressionSyntax? expression, SemanticModel semanticModel, CancellationToken cancellationToken)
    {
        if (expression is not InvocationExpressionSyntax invocation
            || semanticModel.GetSymbolInfo(invocation, cancellationToken).Symbol is not IMethodSymbol invokedMethod
            || !IsResultFactory(invokedMethod, out var status, out var code, out var httpStatusCode))
        {
            return null;
        }

        var detailExpression = FindArgumentExpression(invocation, invokedMethod, "detail");
        if (detailExpression is null)
        {
            return null;
        }

        var providerTypeName = providerType.ToDisplayString();
        var identifier = CreateIdentifier(providerTypeName, memberSymbol.Name);

        return new ErrorCatalogEntryModel(
            identifier,
            "docs/errors/README.md",
            providerTypeName,
            memberSymbol.Name,
            status,
            httpStatusCode,
            code,
            RenderTemplate(detailExpression),
            NormalizeSummary(memberSymbol.GetDocumentationCommentXml(cancellationToken: cancellationToken) ?? string.Empty));
    }

    private static bool ReturnsResult(ITypeSymbol type)
    {
        return type.ToDisplayString() switch
        {
            "SharedKernel.Results.Result" => true,
            var displayName when displayName.StartsWith("SharedKernel.Results.Result<", StringComparison.Ordinal) => true,
            _ => false,
        };
    }

    private static bool IsResultFactory(IMethodSymbol invokedMethod, out string status, out string code, out int httpStatusCode)
    {
        status = string.Empty;
        code = string.Empty;
        httpStatusCode = 0;

        var containingType = invokedMethod.ContainingType.ToDisplayString();
        var containingOriginalType = invokedMethod.ContainingType.OriginalDefinition.ToDisplayString();
        if (!string.Equals(containingType, "SharedKernel.Results.Result", StringComparison.Ordinal)
            && !string.Equals(containingOriginalType, "SharedKernel.Results.Result<T>", StringComparison.Ordinal))
        {
            return false;
        }

        switch (invokedMethod.Name)
        {
            case "Invalid":
                status = "Invalid";
                code = "invalid";
                httpStatusCode = 400;
                return true;
            case "NotFound":
                status = "NotFound";
                code = "not_found";
                httpStatusCode = 404;
                return true;
            case "Unauthorized":
                status = "Unauthorized";
                code = "unauthorized";
                httpStatusCode = 401;
                return true;
            case "Forbidden":
                status = "Forbidden";
                code = "forbidden";
                httpStatusCode = 403;
                return true;
            case "Conflict":
                status = "Conflict";
                code = "conflict";
                httpStatusCode = 409;
                return true;
            case "Error":
                status = "Error";
                code = "error";
                httpStatusCode = 422;
                return true;
            case "CriticalError":
                status = "CriticalError";
                code = "critical_error";
                httpStatusCode = 500;
                return true;
            case "Unavailable":
                status = "Unavailable";
                code = "unavailable";
                httpStatusCode = 503;
                return true;
            default:
                return false;
        }
    }

    private static ExpressionSyntax? FindArgumentExpression(InvocationExpressionSyntax invocation, IMethodSymbol invokedMethod, string parameterName)
    {
        for (var index = 0; index < invocation.ArgumentList.Arguments.Count; index++)
        {
            var argument = invocation.ArgumentList.Arguments[index];
            if (argument.NameColon?.Name.Identifier.ValueText == parameterName)
            {
                return argument.Expression;
            }

            if (index < invokedMethod.Parameters.Length && string.Equals(invokedMethod.Parameters[index].Name, parameterName, StringComparison.Ordinal))
            {
                return argument.Expression;
            }
        }

        return null;
    }

    private static string RenderTemplate(ExpressionSyntax expression)
    {
        return expression switch
        {
            LiteralExpressionSyntax literal when literal.IsKind(SyntaxKind.StringLiteralExpression) => literal.Token.ValueText,
            InterpolatedStringExpressionSyntax interpolated => RenderInterpolatedTemplate(interpolated),
            _ => expression.ToString(),
        };
    }

    private static string RenderInterpolatedTemplate(InterpolatedStringExpressionSyntax interpolated)
    {
        var builder = new System.Text.StringBuilder();

        foreach (var content in interpolated.Contents)
        {
            switch (content)
            {
                case InterpolatedStringTextSyntax text:
                    builder.Append(text.TextToken.ValueText);
                    break;
                case InterpolationSyntax interpolation:
                    builder.Append('{').Append(interpolation.Expression.ToString());
                    if (interpolation.FormatClause is not null)
                    {
                        builder.Append(':').Append(interpolation.FormatClause.FormatStringToken.ValueText);
                    }
                    builder.Append('}');
                    break;
            }
        }

        return builder.ToString();
    }

    private static string CreateIdentifier(string providerTypeName, string memberName)
    {
        var raw = providerTypeName + "." + memberName;
        var builder = new System.Text.StringBuilder(raw.Length);
        var previousWasSeparator = false;

        foreach (var character in raw)
        {
            if (char.IsLetterOrDigit(character))
            {
                builder.Append(char.ToLowerInvariant(character));
                previousWasSeparator = false;
            }
            else if (!previousWasSeparator)
            {
                builder.Append('-');
                previousWasSeparator = true;
            }
        }

        return builder.ToString().Trim('-');
    }

    private static string? NormalizeSummary(string xmlDocumentation)
    {
        if (string.IsNullOrWhiteSpace(xmlDocumentation))
        {
            return null;
        }

        const string summaryStart = "<summary>";
        const string summaryEnd = "</summary>";

        var startIndex = xmlDocumentation.IndexOf(summaryStart, StringComparison.Ordinal);
        var endIndex = xmlDocumentation.IndexOf(summaryEnd, StringComparison.Ordinal);
        if (startIndex < 0 || endIndex <= startIndex)
        {
            return null;
        }

        var summary = xmlDocumentation.Substring(startIndex + summaryStart.Length, endIndex - (startIndex + summaryStart.Length));
        var normalized = string.Join(" ", summary.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }
}
