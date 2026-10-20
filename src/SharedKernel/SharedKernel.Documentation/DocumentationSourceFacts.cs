using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace SharedKernel.Documentation;

internal static class DocumentationSourceFacts
{
    public static string[] ReadSwitchCaseTypeNames(string sourcePath, string methodName, int parameterCount)
    {
        var syntaxRoot = ParseSource(File.ReadAllText(sourcePath), sourcePath);
        var method = ReadMethod(syntaxRoot, sourcePath, methodName, parameterCount);
        if (method.DescendantNodes().OfType<TypeOfExpressionSyntax>().Any())
        {
            throw new InvalidOperationException(
                $"Unsupported typeof-based event dispatch in '{sourcePath}'; update the documentation source-of-truth parser.");
        }

        return method.DescendantNodes()
            .SelectMany(node => node switch
            {
                CasePatternSwitchLabelSyntax switchLabel => ReadPatternTypeNames(switchLabel.Pattern, sourcePath),
                SwitchExpressionArmSyntax switchArm => ReadPatternTypeNames(switchArm.Pattern, sourcePath),
                _ => []
            })
            .Order(StringComparer.Ordinal)
            .ToArray();
    }

    public static string[] ReadTopLevelInvocationIdentifiers(string sourcePath)
    {
        var syntaxRoot = ParseSource(File.ReadAllText(sourcePath), sourcePath);
        var invocations = syntaxRoot.DescendantNodes()
            .OfType<GlobalStatementSyntax>()
            .SelectMany(statement => statement.DescendantNodes().OfType<InvocationExpressionSyntax>());

        return ReadInvocationIdentifiers(invocations);
    }

    public static string[] ReadTopLevelRegistrationIdentifiers(string sourcePath)
    {
        var syntaxRoot = ParseSource(File.ReadAllText(sourcePath), sourcePath);
        var invocations = syntaxRoot.DescendantNodes()
            .OfType<GlobalStatementSyntax>()
            .SelectMany(statement => statement.DescendantNodes().OfType<InvocationExpressionSyntax>());

        return ReadRegistrationIdentifiers(invocations);
    }

    public static string[] ReadMethodInvocationIdentifiers(
        string sourcePath,
        string methodName,
        int parameterCount)
    {
        var syntaxRoot = ParseSource(File.ReadAllText(sourcePath), sourcePath);
        var method = ReadMethod(syntaxRoot, sourcePath, methodName, parameterCount);

        return ReadInvocationIdentifiers(method.DescendantNodes().OfType<InvocationExpressionSyntax>());
    }

    public static string[] ReadMethodInvocationArguments(
        string sourcePath,
        string methodName,
        int parameterCount,
        string invokedMethodName)
    {
        var syntaxRoot = ParseSource(File.ReadAllText(sourcePath), sourcePath);
        var method = ReadMethod(syntaxRoot, sourcePath, methodName, parameterCount);

        return method.DescendantNodes()
            .OfType<InvocationExpressionSyntax>()
            .Where(invocation => invocation.Expression switch
            {
                MemberAccessExpressionSyntax memberAccess => memberAccess.Name.Identifier.ValueText == invokedMethodName,
                GenericNameSyntax genericName => genericName.Identifier.ValueText == invokedMethodName,
                IdentifierNameSyntax identifierName => identifierName.Identifier.ValueText == invokedMethodName,
                _ => false
            })
            .SelectMany(invocation => invocation.ArgumentList.Arguments)
            .Select(argument => argument.NormalizeWhitespace().ToString())
            .ToArray();
    }

    public static string[] ReadMethodRegistrationIdentifiers(
        string sourcePath,
        string methodName,
        int parameterCount)
    {
        var syntaxRoot = ParseSource(File.ReadAllText(sourcePath), sourcePath);
        var method = ReadMethod(syntaxRoot, sourcePath, methodName, parameterCount);

        return ReadRegistrationIdentifiers(method.DescendantNodes().OfType<InvocationExpressionSyntax>());
    }

    public static string[] ReadMarkedFactIdentifiers(
        string fullDocumentPath,
        string documentPath,
        string markerName,
        string factName)
    {
        var document = File.ReadAllText(fullDocumentPath);
        var startMarker = $"<!-- doc-fact:{markerName}:start -->";
        var endMarker = $"<!-- doc-fact:{markerName}:end -->";
        var startCount = CountOccurrences(document, startMarker);
        var endCount = CountOccurrences(document, endMarker);
        var startIndex = document.IndexOf(startMarker, StringComparison.Ordinal);
        var endIndex = document.IndexOf(endMarker, StringComparison.Ordinal);
        if (startCount != 1 || endCount != 1 || startIndex < 0 || endIndex <= startIndex)
        {
            throw new InvalidOperationException(
                $"Documentation fact section '{markerName}' is missing or malformed in '{documentPath}'.");
        }

        var factPrefix = $"- {factName}: `";
        var section = document[(startIndex + startMarker.Length)..endIndex];
        var factLines = section
            .Split('\n')
            .Select(line => line.Trim())
            .Where(line => line.Length > 0)
            .ToArray();
        if (factLines.Any(line =>
                !line.StartsWith(factPrefix, StringComparison.Ordinal)
                || !line.EndsWith('`')))
        {
            throw new InvalidOperationException(
                $"Documentation fact section '{markerName}' contains malformed '{factName}' content in '{documentPath}'.");
        }

        var identifiers = factLines
            .Select(line => line[factPrefix.Length..^1])
            .ToArray();
        if (identifiers.Length == 0 || identifiers.Distinct(StringComparer.Ordinal).Count() != identifiers.Length)
        {
            throw new InvalidOperationException(
                $"Documentation fact section '{markerName}' must contain unique '{factName}' identifiers in '{documentPath}'.");
        }

        return identifiers.Order(StringComparer.Ordinal).ToArray();
    }

    public static void ValidateMarkedContentBlock(
        string fullDocumentPath,
        string documentPath,
        string markerName)
    {
        var document = File.ReadAllText(fullDocumentPath);
        var startMarker = $"<!-- doc-content:{markerName}:start -->";
        var endMarker = $"<!-- doc-content:{markerName}:end -->";
        var startCount = CountOccurrences(document, startMarker);
        var endCount = CountOccurrences(document, endMarker);
        var startIndex = document.IndexOf(startMarker, StringComparison.Ordinal);
        var endIndex = document.IndexOf(endMarker, StringComparison.Ordinal);
        if (startCount != 1 || endCount != 1 || startIndex < 0 || endIndex <= startIndex)
        {
            throw new InvalidOperationException(
                $"Documentation content block '{markerName}' is missing or malformed in '{documentPath}'.");
        }

        var section = document[(startIndex + startMarker.Length)..endIndex];
        var hasMeaningfulContent = section
            .Split('\n')
            .Select(line => line.Trim())
            .Any(line =>
                line.Length > 0
                && !(line.StartsWith("<!--", StringComparison.Ordinal) && line.EndsWith("-->", StringComparison.Ordinal))
                && line.Any(char.IsLetterOrDigit));
        if (!hasMeaningfulContent)
        {
            throw new InvalidOperationException(
                $"Documentation content block '{markerName}' must contain meaningful non-marker content in '{documentPath}'.");
        }
    }

    private static MethodDeclarationSyntax ReadMethod(
        SyntaxNode syntaxRoot,
        string sourcePath,
        string methodName,
        int parameterCount)
    {
        var methods = syntaxRoot.DescendantNodes()
            .OfType<MethodDeclarationSyntax>()
            .Where(candidate =>
                candidate.Identifier.ValueText == methodName
                && candidate.ParameterList.Parameters.Count == parameterCount)
            .ToArray();
        if (methods.Length != 1)
        {
            throw new InvalidOperationException(
                $"Expected exactly one method '{methodName}' with {parameterCount} parameter(s) in '{sourcePath}', found {methods.Length}.");
        }

        return methods[0];
    }

    private static string[] ReadInvocationIdentifiers(IEnumerable<InvocationExpressionSyntax> invocations) =>
        invocations
            .SelectMany(invocation => invocation.Expression switch
            {
                MemberAccessExpressionSyntax { Name: GenericNameSyntax genericName } =>
                    genericName.TypeArgumentList.Arguments
                        .Select(argument => argument.ToString())
                        .Prepend(genericName.Identifier.ValueText),
                MemberAccessExpressionSyntax memberAccess => [memberAccess.Name.Identifier.ValueText],
                GenericNameSyntax genericName =>
                    genericName.TypeArgumentList.Arguments
                        .Select(argument => argument.ToString())
                        .Prepend(genericName.Identifier.ValueText),
                IdentifierNameSyntax identifierName => [identifierName.Identifier.ValueText],
                _ => Enumerable.Empty<string>()
            })
            .Order(StringComparer.Ordinal)
            .ToArray();

    private static string[] ReadRegistrationIdentifiers(IEnumerable<InvocationExpressionSyntax> invocations) =>
        invocations
            .SelectMany(invocation =>
            {
                SimpleNameSyntax? name = invocation.Expression switch
                {
                    MemberAccessExpressionSyntax memberAccess => memberAccess.Name,
                    GenericNameSyntax genericName => genericName,
                    IdentifierNameSyntax identifierName => identifierName,
                    _ => null
                };
                if (name is null || !name.Identifier.ValueText.StartsWith("Add", StringComparison.Ordinal))
                {
                    return Enumerable.Empty<string>();
                }

                if (name is GenericNameSyntax hostedService
                    && hostedService.Identifier.ValueText == "AddHostedService")
                {
                    return hostedService.TypeArgumentList.Arguments.Select(argument => argument.ToString());
                }

                return [name.Identifier.ValueText];
            })
            .Order(StringComparer.Ordinal)
            .ToArray();

    private static SyntaxNode ParseSource(string source, string sourcePath)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(source);
        var errors = syntaxTree.GetDiagnostics()
            .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .Select(diagnostic => diagnostic.ToString())
            .ToArray();
        if (errors.Length > 0)
        {
            throw new InvalidOperationException(
                $"Could not parse source '{sourcePath}':{Environment.NewLine}{string.Join(Environment.NewLine, errors)}");
        }

        return syntaxTree.GetRoot();
    }

    private static IEnumerable<string> ReadPatternTypeNames(PatternSyntax pattern, string sourcePath) => pattern switch
    {
        DeclarationPatternSyntax declarationPattern => [declarationPattern.Type.ToString()],
        TypePatternSyntax typePattern => [typePattern.Type.ToString()],
        RecursivePatternSyntax { Type: not null } recursivePattern => [recursivePattern.Type.ToString()],
        ParenthesizedPatternSyntax parenthesizedPattern => ReadPatternTypeNames(parenthesizedPattern.Pattern, sourcePath),
        BinaryPatternSyntax binaryPattern => ReadPatternTypeNames(binaryPattern.Left, sourcePath)
            .Concat(ReadPatternTypeNames(binaryPattern.Right, sourcePath)),
        DiscardPatternSyntax or VarPatternSyntax => [],
        ConstantPatternSyntax { Expression: LiteralExpressionSyntax expression }
            when expression.IsKind(SyntaxKind.NullLiteralExpression) => [],
        _ => throw new InvalidOperationException(
            $"Unsupported C# event switch pattern '{pattern.Kind()}' in '{sourcePath}'; update the documentation source-of-truth parser.")
    };

    private static int CountOccurrences(string text, string value)
    {
        var count = 0;
        var offset = 0;
        while (offset < text.Length)
        {
            var index = text.IndexOf(value, offset, StringComparison.Ordinal);
            if (index < 0)
            {
                return count;
            }

            count++;
            offset = index + value.Length;
        }

        return count;
    }
}
