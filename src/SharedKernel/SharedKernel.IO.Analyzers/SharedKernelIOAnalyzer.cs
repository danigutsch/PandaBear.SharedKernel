using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace PandaBear.IO.Analyzers;

/// <summary>
/// Reports unbounded stream buffering and hand-rolled copy loops.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class SharedKernelIOAnalyzer : DiagnosticAnalyzer
{
    private const int MaxTraceDepth = 8;

    private static readonly DiagnosticDescriptor UnboundedBufferingRule = new(
        IODiagnosticIds.UnboundedBuffering,
        title: "Request and upload streams should not be buffered without a length bound",
        messageFormat: "'{0}' buffers received content in memory without an explicit length bound; use PandaBear.IO.SeekableContent.Create with a maximum length, or enforce a limit while reading",
        category: "IO",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Content-Length and Stream.Length are hints. Buffer request, form-file, and HTTP content streams through SeekableContent.Create, which counts received bytes, or enforce an explicit maximum while reading.");

    private static readonly DiagnosticDescriptor HandRolledCopyLoopRule = new(
        IODiagnosticIds.HandRolledCopyLoop,
        title: "Hand-rolled copy loops into a MemoryStream should use SeekableContent",
        messageFormat: "This loop copies a stream into a MemoryStream without a length bound; use PandaBear.IO.SeekableContent.Create or enforce a maximum on the received bytes",
        category: "IO",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "A read loop that writes every chunk into a MemoryStream retains the complete payload in managed memory. Use SeekableContent.Create, which bounds memory and spills to a temporary file, or compare the running total with a maximum inside the loop.");

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
    {
        get;
    } =
        ImmutableArray.Create(UnboundedBufferingRule, HandRolledCopyLoopRule);

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        if (context is null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(static start =>
        {
            var types = KnownTypes.Create(start.Compilation);
            if (types is null)
            {
                return;
            }

            start.RegisterOperationAction(ctx => AnalyzeInvocation(ctx, types), OperationKind.Invocation);
            start.RegisterOperationAction(ctx => AnalyzeLoop(ctx, types), OperationKind.Loop);
        });
    }

    private static void AnalyzeInvocation(OperationAnalysisContext context, KnownTypes types)
    {
        var invocation = (IInvocationOperation)context.Operation;
        var method = invocation.TargetMethod;
        var owner = method.ContainingType;
        var buffers = false;

        if (method.Name is "CopyTo" or "CopyToAsync" && Inherits(owner, types.Stream))
        {
            buffers = invocation.Arguments.Length > 0
                && Inherits(Unwrap(invocation.Arguments[0].Value).Type, types.MemoryStream)
                && invocation.Instance is { } source
                && IsIntake(GetRoot(invocation).SemanticModel, types, source, 0);
        }
        else if (method.Name == "ReadAsByteArrayAsync" && Inherits(owner, types.HttpContent))
        {
            buffers = true;
        }
        else if (method.Name is "ReadToEnd" or "ReadToEndAsync" && Inherits(owner, types.TextReader))
        {
            buffers = invocation.Instance is { } reader && IsIntake(GetRoot(invocation).SemanticModel, types, reader, 0);
        }

        if (buffers && !HasLengthBound(GetRoot(invocation)))
        {
            context.ReportDiagnostic(Diagnostic.Create(UnboundedBufferingRule, invocation.Syntax.GetLocation(), method.Name));
        }
    }

    private static void AnalyzeLoop(OperationAnalysisContext context, KnownTypes types)
    {
        var loop = (ILoopOperation)context.Operation;
        var reads = false;
        var writes = false;

        foreach (var invocation in loop.Descendants().OfType<IInvocationOperation>())
        {
            if (NearestLoop(invocation) != loop)
            {
                continue;
            }

            var method = invocation.TargetMethod;
            reads |= method.Name is "Read" or "ReadAsync" && Inherits(method.ContainingType, types.Stream);
            writes |= method.Name is "Write" or "WriteAsync" && Inherits(invocation.Instance?.Type, types.MemoryStream);
        }

        if (reads && writes && !HasLoopBound(loop))
        {
            context.ReportDiagnostic(Diagnostic.Create(HandRolledCopyLoopRule, GetLoopKeywordLocation(loop)));
        }
    }

    private static Location GetLoopKeywordLocation(ILoopOperation loop) =>
        loop.Syntax switch
        {
            WhileStatementSyntax statement => statement.WhileKeyword.GetLocation(),
            DoStatementSyntax statement => statement.DoKeyword.GetLocation(),
            ForStatementSyntax statement => statement.ForKeyword.GetLocation(),
            _ => loop.Syntax.GetLocation()
        };

    private static ILoopOperation? NearestLoop(IOperation operation)
    {
        for (var parent = operation.Parent; parent is not null; parent = parent.Parent)
        {
            if (parent is ILoopOperation loop)
            {
                return loop;
            }
        }

        return null;
    }

    private static IOperation GetRoot(IOperation operation)
    {
        var root = operation;
        while (root.Parent is { } parent)
        {
            root = parent;
        }

        return root;
    }

    /// <summary>A relational comparison against Length or ContentLength is the explicit bound recognized around a buffering call.</summary>
    private static bool HasLengthBound(IOperation root) =>
        root.DescendantsAndSelf().OfType<IBinaryOperation>().Any(static binary =>
            IsRelational(binary) && (MentionsLength(binary.LeftOperand) || MentionsLength(binary.RightOperand)));

    /// <summary>Inside a copy loop, any relational comparison other than one against zero (the end-of-stream test) is a bound.</summary>
    private static bool HasLoopBound(ILoopOperation loop) =>
        loop.DescendantsAndSelf().OfType<IBinaryOperation>().Any(static binary =>
            IsRelational(binary) && !IsZero(binary.LeftOperand) && !IsZero(binary.RightOperand));

    private static bool IsRelational(IBinaryOperation binary) =>
        binary.OperatorKind is BinaryOperatorKind.GreaterThan
            or BinaryOperatorKind.GreaterThanOrEqual
            or BinaryOperatorKind.LessThan
            or BinaryOperatorKind.LessThanOrEqual;

    private static bool IsZero(IOperation operation)
    {
        var value = Unwrap(operation).ConstantValue;
        return value.HasValue && value.Value is int or long or short or byte && Convert.ToInt64(value.Value, System.Globalization.CultureInfo.InvariantCulture) == 0;
    }

    private static bool MentionsLength(IOperation operation) =>
        operation.DescendantsAndSelf().Any(static node =>
            node is IPropertyReferenceOperation { Property.Name: "Length" or "ContentLength" }
                or IFieldReferenceOperation { Field.Name: "Length" or "ContentLength" });

    private static bool IsIntake(SemanticModel? model, KnownTypes types, IOperation operation, int depth)
    {
        if (depth > MaxTraceDepth)
        {
            return false;
        }

        return Unwrap(operation) switch
        {
            IPropertyReferenceOperation { Property.Name: "Body" } property => Inherits(property.Property.ContainingType, types.HttpRequest),
            IInvocationOperation { TargetMethod.Name: "OpenReadStream" } invocation => Inherits(invocation.TargetMethod.ContainingType, types.FormFile),
            IInvocationOperation { TargetMethod.Name: "ReadAsStream" or "ReadAsStreamAsync" } invocation => Inherits(invocation.TargetMethod.ContainingType, types.HttpContent),
            IObjectCreationOperation creation when Inherits(creation.Type, types.StreamReader) => creation.Arguments.Length > 0 && IsIntake(model, types, creation.Arguments[0].Value, depth + 1),
            ILocalReferenceOperation local => GetInitializer(model, local.Local) is { } initializer && IsIntake(model, types, initializer, depth + 1),
            _ => false,
        };
    }

    private static IOperation? GetInitializer(SemanticModel? model, ILocalSymbol local)
    {
        if (local.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax() is not VariableDeclaratorSyntax { Initializer.Value: { } value })
        {
            return null;
        }

        return model?.SyntaxTree == value.SyntaxTree ? model.GetOperation(value) : null;
    }

    private static IOperation Unwrap(IOperation operation)
    {
        while (true)
        {
            switch (operation)
            {
                case IConversionOperation conversion:
                    operation = conversion.Operand;
                    break;
                case IAwaitOperation await:
                    operation = await.Operation;
                    break;
                case IParenthesizedOperation parenthesized:
                    operation = parenthesized.Operand;
                    break;
                default:
                    return operation;
            }
        }
    }

    private static bool Inherits(ITypeSymbol? type, INamedTypeSymbol? target)
    {
        if (type is null || target is null)
        {
            return false;
        }

        for (var current = type; current is not null; current = current.BaseType)
        {
            if (SymbolEqualityComparer.Default.Equals(current.OriginalDefinition, target))
            {
                return true;
            }
        }

        return type.AllInterfaces.Any(candidate => SymbolEqualityComparer.Default.Equals(candidate, target));
    }

    private sealed class KnownTypes
    {
        private KnownTypes(Compilation compilation, INamedTypeSymbol stream)
        {
            Stream = stream;
            MemoryStream = compilation.GetTypeByMetadataName("System.IO.MemoryStream");
            StreamReader = compilation.GetTypeByMetadataName("System.IO.StreamReader");
            TextReader = compilation.GetTypeByMetadataName("System.IO.TextReader");
            HttpContent = compilation.GetTypeByMetadataName("System.Net.Http.HttpContent");
            FormFile = compilation.GetTypeByMetadataName("Microsoft.AspNetCore.Http.IFormFile");
            HttpRequest = compilation.GetTypeByMetadataName("Microsoft.AspNetCore.Http.HttpRequest");
        }

        public INamedTypeSymbol Stream
        {
            get;
        }

        public INamedTypeSymbol? MemoryStream
        {
            get;
        }

        public INamedTypeSymbol? StreamReader
        {
            get;
        }

        public INamedTypeSymbol? TextReader
        {
            get;
        }

        public INamedTypeSymbol? HttpContent
        {
            get;
        }

        public INamedTypeSymbol? FormFile
        {
            get;
        }

        public INamedTypeSymbol? HttpRequest
        {
            get;
        }

        public static KnownTypes? Create(Compilation compilation) =>
            compilation.GetTypeByMetadataName("System.IO.Stream") is { } stream ? new KnownTypes(compilation, stream) : null;
    }
}
