using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace PandaBear.Style.Analyzers;

public sealed partial class SharedKernelStyleAnalyzer
{
    private static readonly DiagnosticDescriptor ExceptionDocumentationRule = new(
        StyleDiagnosticIds.UndocumentedThrownException,
        title: "Documented members should list the exceptions they throw",
        messageFormat: "{0} '{1}' throws {2} without documenting it with an <exception> tag",
        category: "Style",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "A non-private method, constructor, or operator that has an XML documentation comment must list every exception it throws directly, including exceptions thrown by static Exception.ThrowIf helpers such as ArgumentNullException.ThrowIfNull. An exception tag naming the thrown type or a base type satisfies the rule. Throws handled by an enclosing catch, rethrows, throws inside lambdas and local functions, overrides, explicit interface implementations, members documented with inheritdoc, and generated code are not checked. Members without any documentation comment are covered by CS1591 and SKSTYLE017.",
        helpLinkUri: "https://github.com/danigutsch/PandaBear.SharedKernel/blob/main/src/SharedKernel/SharedKernel.Style.Analyzers/README.md#skstyle018");

    private static void AnalyzeExceptionDocumentation(OperationBlockAnalysisContext context)
    {
        if (context.OwningSymbol is not IMethodSymbol { MethodKind: MethodKind.Ordinary or MethodKind.Constructor or MethodKind.UserDefinedOperator or MethodKind.Conversion, IsOverride: false, ExplicitInterfaceImplementations.Length: 0 } method
            || !IsNonPrivate(method)
            || method.Locations.FirstOrDefault(static location => location.IsInSource) is not { } location)
        {
            return;
        }

        var documentation = method.DeclaringSyntaxReferences
            .SelectMany(reference => reference.GetSyntax(context.CancellationToken).GetLeadingTrivia())
            .Select(static trivia => trivia.GetStructure())
            .OfType<DocumentationCommentTriviaSyntax>()
            .ToList();
        if (documentation.Count == 0 || documentation.Any(HasInheritDoc))
        {
            return;
        }

        var thrown = new List<INamedTypeSymbol>();
        foreach (var block in context.OperationBlocks)
        {
            CollectThrownTypes(block, thrown);
        }

        if (thrown.Count == 0)
        {
            return;
        }

        if (context.OperationBlocks[0].SemanticModel is not { } semanticModel)
        {
            return;
        }

        var documented = ResolveDocumentedExceptions(semanticModel, documentation);
        var reported = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
        foreach (var type in thrown)
        {
            if (documented.Any(candidate => DerivesFromOrEquals(type, candidate)) || !reported.Add(type))
            {
                continue;
            }

            var name = method.MethodKind == MethodKind.Constructor ? method.ContainingType.Name : method.Name;
            context.ReportDiagnostic(Diagnostic.Create(ExceptionDocumentationRule, location, DescribeKind(method), name, type.Name));
        }
    }

    private static bool HasInheritDoc(DocumentationCommentTriviaSyntax documentation)
    {
        return documentation.DescendantNodes().Any(static node => node is XmlEmptyElementSyntax { Name.LocalName.ValueText: "inheritdoc" }
            or XmlElementSyntax { StartTag.Name.LocalName.ValueText: "inheritdoc" });
    }

    private static List<INamedTypeSymbol> ResolveDocumentedExceptions(SemanticModel semanticModel, List<DocumentationCommentTriviaSyntax> documentation)
    {
        var documented = new List<INamedTypeSymbol>();
        foreach (var crefAttribute in documentation.SelectMany(static comment => comment.DescendantNodes()).OfType<XmlCrefAttributeSyntax>())
        {
            var isException = crefAttribute.Parent is XmlElementStartTagSyntax { Name.LocalName.ValueText: "exception" }
                or XmlEmptyElementSyntax { Name.LocalName.ValueText: "exception" };
            if (isException
                && crefAttribute.SyntaxTree == semanticModel.SyntaxTree
                && semanticModel.GetSymbolInfo(crefAttribute.Cref).Symbol is INamedTypeSymbol type)
            {
                documented.Add(type);
            }
        }

        return documented;
    }

    private static void CollectThrownTypes(IOperation operation, List<INamedTypeSymbol> thrown)
    {
        if (operation is IAnonymousFunctionOperation or ILocalFunctionOperation)
        {
            return;
        }

        var type = operation switch
        {
            IThrowOperation { Exception: { } exception } => ThrownType(exception),
            IInvocationOperation { TargetMethod: { IsStatic: true, ContainingType: { } container } method } when method.Name.StartsWith("ThrowIf", StringComparison.Ordinal) && DerivesFromException(container) => container,
            _ => null,
        };

        if (type is not null && !IsCaughtInEnclosingTry(operation, type))
        {
            thrown.Add(type);
        }

        foreach (var child in operation.ChildOperations)
        {
            CollectThrownTypes(child, thrown);
        }
    }

    // A throw of a variable or call typed as plain Exception says nothing about the concrete type, so it is skipped.
    private static INamedTypeSymbol? ThrownType(IOperation exception)
    {
        while (exception is IConversionOperation conversion)
        {
            exception = conversion.Operand;
        }

        return exception.Type is INamedTypeSymbol type && (exception is IObjectCreationOperation || !IsSystemException(type))
            ? type
            : null;
    }

    private static bool IsCaughtInEnclosingTry(IOperation operation, INamedTypeSymbol thrown)
    {
        for (var child = operation; child.Parent is { } parent; child = parent)
        {
            if (parent is ITryOperation tryOperation
                && ReferenceEquals(tryOperation.Body, child)
                && tryOperation.Catches.Any(catchClause => CatchHandles(catchClause, thrown)))
            {
                return true;
            }
        }

        return false;
    }

    // A filter handles the throw only when it is a pure type test of the caught variable, for example when (e is IOException or JsonException).
    private static bool CatchHandles(ICatchClauseOperation catchClause, INamedTypeSymbol thrown)
    {
        if (catchClause.ExceptionType is { } caught && !DerivesFromOrEquals(thrown, caught))
        {
            return false;
        }

        return catchClause.Filter switch
        {
            null => true,
            IIsTypeOperation { IsNegated: false, ValueOperand: ILocalReferenceOperation local } isType => IsCatchLocal(catchClause, local) && DerivesFromOrEquals(thrown, isType.TypeOperand),
            IIsPatternOperation { Value: ILocalReferenceOperation local, Pattern: { } pattern } => IsCatchLocal(catchClause, local) && PatternMatches(pattern, thrown),
            _ => false,
        };
    }

    private static bool IsCatchLocal(ICatchClauseOperation catchClause, ILocalReferenceOperation local)
    {
        return catchClause.Locals.Contains(local.Local, SymbolEqualityComparer.Default);
    }

    private static bool PatternMatches(IPatternOperation pattern, INamedTypeSymbol thrown)
    {
        return pattern switch
        {
            IBinaryPatternOperation { OperatorKind: BinaryOperatorKind.Or } binary => PatternMatches(binary.LeftPattern, thrown) || PatternMatches(binary.RightPattern, thrown),
            ITypePatternOperation type => DerivesFromOrEquals(thrown, type.MatchedType),
            IDeclarationPatternOperation declaration => declaration.MatchedType is { } matched && DerivesFromOrEquals(thrown, matched),
            _ => false,
        };
    }

    private static bool IsSystemException(INamedTypeSymbol type)
    {
        return type is { Name: "Exception", ContainingNamespace: { Name: "System", ContainingNamespace.IsGlobalNamespace: true } };
    }

    private static bool DerivesFromException(INamedTypeSymbol type)
    {
        for (var current = type; current is not null; current = current.BaseType)
        {
            if (IsSystemException(current))
            {
                return true;
            }
        }

        return false;
    }

    private static bool DerivesFromOrEquals(INamedTypeSymbol type, ITypeSymbol baseType)
    {
        for (var current = type; current is not null; current = current.BaseType)
        {
            if (SymbolEqualityComparer.Default.Equals(current, baseType))
            {
                return true;
            }
        }

        return false;
    }
}
