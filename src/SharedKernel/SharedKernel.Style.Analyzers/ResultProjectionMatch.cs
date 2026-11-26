using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;

namespace SharedKernel.Style.Analyzers;

/// <summary>Matches an initialized Result guard whose success branch can be replaced by Map.</summary>
/// <param name="guard">The verified failure guard.</param>
/// <param name="success">The terminal success return.</param>
/// <param name="map">The verified replacement invocation.</param>
internal sealed class ResultProjectionMatch(
    IfStatementSyntax guard,
    ReturnStatementSyntax success,
    InvocationExpressionSyntax map)
{
    private static readonly ImmutableHashSet<string> FactoryNames = ImmutableHashSet.Create(
        StringComparer.Ordinal,
        "Ok", "Created", "Accepted", "Invalid", "Unauthorized", "Forbidden", "NotFound",
        "Conflict", "Error", "CriticalError", "Unavailable");

    private static readonly ImmutableHashSet<SpecialType> NumericTypes = ImmutableHashSet.Create(
        SpecialType.System_SByte, SpecialType.System_Byte, SpecialType.System_Int16, SpecialType.System_UInt16,
        SpecialType.System_Int32, SpecialType.System_UInt32, SpecialType.System_Int64, SpecialType.System_UInt64,
        SpecialType.System_Char, SpecialType.System_Single, SpecialType.System_Double, SpecialType.System_Decimal);

    private static readonly ImmutableHashSet<BinaryOperatorKind> ArithmeticOperators = ImmutableHashSet.Create(
        BinaryOperatorKind.Add, BinaryOperatorKind.Subtract, BinaryOperatorKind.Multiply,
        BinaryOperatorKind.Divide, BinaryOperatorKind.Remainder);

    private static readonly ImmutableHashSet<TypeKind> UncapturableTypes = ImmutableHashSet.Create(
        TypeKind.Dynamic, TypeKind.Error, TypeKind.Pointer, TypeKind.FunctionPointer);

    /// <summary>Gets the verified failure guard.</summary>
    internal IfStatementSyntax Guard { get; } = guard;

    /// <summary>Gets the terminal success return.</summary>
    internal ReturnStatementSyntax Success { get; } = success;

    /// <summary>Gets the verified replacement invocation.</summary>
    internal InvocationExpressionSyntax Map { get; } = map;

    /// <summary>Finds a safe projection and binds its replacement to the synchronous Result.Map overload.</summary>
    internal static ResultProjectionMatch? Find(IfStatementSyntax guard, SemanticModel model, CancellationToken ct)
    {
        if (guard is not
            {
                Else: null,
                Parent: BlockSyntax block,
                Condition: MemberAccessExpressionSyntax { Name.Identifier.ValueText: "IsFailure" } condition,
            }
            || model.GetOperation(condition, ct) is not IPropertyReferenceOperation
            {
                Instance: ILocalReferenceOperation { Local: { RefKind: RefKind.None } source },
            } property)
        {
            return null;
        }

        var result = model.Compilation.GetTypeByMetadataName("SharedKernel.Results.Result`1");
        if (result is null
            || !Same(property.Property.OriginalDefinition.ContainingType, result)
            || result.ContainingAssembly.Identity.Name != "SharedKernel.Results"
            || Same(result.ContainingAssembly, model.Compilation.Assembly))
        {
            return null;
        }

        var runtime = result.ContainingAssembly;
        var factory = runtime.GetTypeByMetadataName("SharedKernel.Results.Result");
        var extensions = runtime.GetTypeByMetadataName("SharedKernel.Results.ResultExtensions");
        var index = block.Statements.IndexOf(guard);
        var tailLength = block.Statements.Count - index;
        if (index < 1 || tailLength is not (2 or 3)
            || block.Statements[index - 1] is not LocalDeclarationStatementSyntax { Declaration.Variables.Count: 1 } declaration
            || declaration.Declaration.Variables[0] is not { Initializer.Value: { } initializer } variable
            || !Same(model.GetDeclaredSymbol(variable, ct), source)
            || !IsInitialized(model.GetOperation(initializer, ct), factory, source.Type))
        {
            return null;
        }

        var failure = guard.Statement is BlockSyntax { Statements.Count: 1 } failureBlock
            ? failureBlock.Statements[0]
            : guard.Statement;
        if (failure is not ReturnStatementSyntax
            {
                Expression: InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax access } conversion,
            }
            || !Same(model.GetSymbolInfo(access.Expression, ct).Symbol, source)
            || model.GetSymbolInfo(conversion, ct).Symbol is not IMethodSymbol
            {
                ReducedFrom: { Name: "ConvertError", Arity: 2, Parameters.Length: 1 } extension,
            } conversionMethod
            || !Same(extension.ContainingType, extensions))
        {
            return null;
        }

        var alias = tailLength == 3 ? FindAlias(block.Statements[index + 1], model, source, result, ct) : null;
        if (tailLength == 3 && alias is null)
        {
            return null;
        }

        if (block.Statements[block.Statements.Count - 1] is not ReturnStatementSyntax
            {
                Expression: InvocationExpressionSyntax { ArgumentList.Arguments.Count: 1 } ok,
            } success
            || model.GetSymbolInfo(ok, ct).Symbol is not IMethodSymbol { Name: "Ok" } okMethod
            || !Same(okMethod.ContainingType, factory)
            || !model.Compilation.ClassifyCommonConversion(okMethod.ReturnType, conversionMethod.ReturnType).IsIdentity
            || HasCommentsOrDirectives(guard)
            || HasCommentsOrDirectives(success))
        {
            return null;
        }

        var projection = ok.ArgumentList.Arguments[0].Expression;
        var reads = new List<ExpressionSyntax>();
        if (!CollectProjectionReads(model.GetOperation(projection, ct), source, result, alias, reads)
            || reads.Count == 0
            || BuildMap(guard, condition.Expression, projection, reads, model, result, okMethod.ReturnType) is not { } map)
        {
            return null;
        }

        return new(guard, success, map);
    }

    private static ILocalSymbol? FindAlias(
        StatementSyntax statement, SemanticModel model, ILocalSymbol source, INamedTypeSymbol result, CancellationToken ct) =>
        statement is LocalDeclarationStatementSyntax { Declaration.Variables.Count: 1, Declaration.Type.IsVar: true } declaration
        && declaration.UsingKeyword.IsKind(SyntaxKind.None)
        && declaration.Declaration.Variables[0] is { Initializer.Value: { } value } variable
        && !HasCommentsOrDirectives(declaration)
        && IsValue(model.GetOperation(value, ct), source, result)
        && model.GetDeclaredSymbol(variable, ct) is ILocalSymbol { RefKind: RefKind.None } local
            ? local
            : null;

    private static bool IsInitialized(IOperation? operation, INamedTypeSymbol? factory, ITypeSymbol result) => operation switch
    {
        IConversionOperation { IsImplicit: true, Conversion.IsIdentity: true } conversion =>
            IsInitialized(conversion.Operand, factory, result),
        IConditionalOperation conditional =>
            IsInitialized(conditional.WhenTrue, factory, result) && IsInitialized(conditional.WhenFalse, factory, result),
        IInvocationOperation { TargetMethod: var method } =>
            Same(method.ContainingType, factory) && Same(method.ReturnType, result) && FactoryNames.Contains(method.Name),
        _ => false,
    };

    private static bool IsValue(IOperation? operation, ILocalSymbol source, INamedTypeSymbol result) =>
        operation is IPropertyReferenceOperation
        {
            Instance: ILocalReferenceOperation instance,
            Property: { Name: "Value" } property,
        }
        && Same(instance.Local, source)
        && Same(property.OriginalDefinition.ContainingType, result);

    private static bool CollectProjectionReads(
        IOperation? operation,
        ILocalSymbol source,
        INamedTypeSymbol result,
        ILocalSymbol? alias,
        List<ExpressionSyntax> reads)
    {
        if (operation?.Syntax is ExpressionSyntax read
            && (alias is null
                ? IsValue(operation, source, result)
                : operation is ILocalReferenceOperation aliasReference && Same(aliasReference.Local, alias)))
        {
            reads.Add(read);
            return true;
        }

        return operation switch
        {
            ILiteralOperation => true,
            ILocalReferenceOperation local => !Same(local.Local, source) && CanCapture(local.Local.Type, local.Local.RefKind),
            IParameterReferenceOperation parameter => CanCapture(parameter.Parameter.Type, parameter.Parameter.RefKind),
            ITupleOperation tuple => tuple.Elements.All(element => CollectProjectionReads(element, source, result, alias, reads)),
            IConversionOperation { OperatorMethod: null } conversion
                when conversion.Conversion.IsIdentity || conversion.Conversion.IsNumeric || conversion.Conversion.IsReference =>
                CollectProjectionReads(conversion.Operand, source, result, alias, reads),
            IBinaryOperation { OperatorMethod: null } binary
                when ArithmeticOperators.Contains(binary.OperatorKind)
                    && IsNumeric(binary.Type) && IsNumeric(binary.LeftOperand.Type) && IsNumeric(binary.RightOperand.Type) =>
                CollectProjectionReads(binary.LeftOperand, source, result, alias, reads)
                && CollectProjectionReads(binary.RightOperand, source, result, alias, reads),
            _ => false,
        };
    }

    private static bool CanCapture(ITypeSymbol type, RefKind refKind) =>
        refKind == RefKind.None
        && !type.IsRefLikeType
        && type is not ITypeParameterSymbol { AllowsRefLikeType: true }
        && !UncapturableTypes.Contains(type.TypeKind);

    private static bool IsNumeric(ITypeSymbol? type) => NumericTypes.Contains(type?.SpecialType ?? SpecialType.None);

    private static InvocationExpressionSyntax? BuildMap(
        IfStatementSyntax guard,
        ExpressionSyntax receiver,
        ExpressionSyntax projection,
        List<ExpressionSyntax> reads,
        SemanticModel model,
        INamedTypeSymbol result,
        ITypeSymbol output)
    {
        var member = guard.FirstAncestorOrSelf<MemberDeclarationSyntax>();
        if (member is null or GlobalStatementSyntax)
        {
            return null;
        }

        var identifiers = new HashSet<string>(member.DescendantTokens()
            .Where(static token => token.IsKind(SyntaxKind.IdentifierToken))
            .Select(static token => token.ValueText), StringComparer.Ordinal);
        const string baseName = "mappedValue";
        var name = baseName;
        for (var suffix = 2; identifiers.Contains(name); suffix++)
        {
            name = baseName + suffix.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        var rewritten = projection.ReplaceNodes(reads, (_, _) => SyntaxFactory.IdentifierName(name)).WithoutTrivia();
        var lambda = SyntaxFactory.SimpleLambdaExpression(SyntaxFactory.Parameter(SyntaxFactory.Identifier(name)), rewritten);
        var access = SyntaxFactory.MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
            receiver.WithoutTrivia(), SyntaxFactory.IdentifierName("Map"));
        var map = SyntaxFactory.InvocationExpression(access,
            SyntaxFactory.ArgumentList(SyntaxFactory.SingletonSeparatedList(SyntaxFactory.Argument(lambda))));
        return model.GetSpeculativeSymbolInfo(guard.SpanStart, map, SpeculativeBindingOption.BindAsExpression).Symbol
                is IMethodSymbol method
            && Same(method.OriginalDefinition.ContainingType, result)
            && model.Compilation.ClassifyCommonConversion(method.ReturnType, output).IsIdentity
            ? map
            : null;
    }

    private static bool HasCommentsOrDirectives(SyntaxNode node) =>
        node.DescendantTrivia(descendIntoTrivia: true).Any(static trivia =>
            !trivia.IsKind(SyntaxKind.WhitespaceTrivia) && !trivia.IsKind(SyntaxKind.EndOfLineTrivia));

    private static bool Same(ISymbol? first, ISymbol? second) => SymbolEqualityComparer.Default.Equals(first, second);
}
