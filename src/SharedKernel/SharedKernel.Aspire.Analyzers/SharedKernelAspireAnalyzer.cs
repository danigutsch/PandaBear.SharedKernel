using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace SharedKernel.Aspire.Analyzers;

/// <summary>
/// Reports diagnostics for SharedKernel Aspire conventions.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class SharedKernelAspireAnalyzer : DiagnosticAnalyzer
{
    private const string WithImageTagMethodName = "WithImageTag";
    private const string WithImageSha256MethodName = "WithImageSHA256";
    private const string Sha256Prefix = "sha256:";

    private static readonly DiagnosticDescriptor ImageTagAndDigestRule = new(
        AspireDiagnosticIds.ImageTagAndDigest,
        title: "Aspire container images should pin tag and digest together",
        messageFormat: "Aspire container image call '{0}' should pair WithImageTag with WithImageSHA256 using a verified bare 64-character digest without the sha256: prefix",
        category: "Aspire",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Repository Aspire resources must pin both a human-readable image tag and a verified SHA-256 digest. Do not commit placeholder digests or include the sha256: prefix in WithImageSHA256.");

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
        ImmutableArray.Create(ImageTagAndDigestRule);

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        if (context is null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeAspireImageInvocation, SyntaxKind.InvocationExpression);
    }

    private static void AnalyzeAspireImageInvocation(SyntaxNodeAnalysisContext context)
    {
        if (context.Node is not InvocationExpressionSyntax invocation)
        {
            return;
        }

        var methodName = GetInvocationName(invocation);
        if (!IsImagePinMethod(methodName))
        {
            return;
        }

        if (string.Equals(methodName, WithImageSha256MethodName, StringComparison.Ordinal)
            && HasInvalidDigest(context, invocation))
        {
            context.ReportDiagnostic(
                Diagnostic.Create(
                    ImageTagAndDigestRule,
                    invocation.ArgumentList.Arguments.First().GetLocation(),
                    methodName));
            return;
        }

        var chain = GetImageInvocationChain(GetOutermostInvocation(invocation)).ToArray();
        var hasImageTag = chain.Any(static candidate =>
            string.Equals(GetInvocationName(candidate), WithImageTagMethodName, StringComparison.Ordinal));
        var hasImageSha256 = chain.Any(static candidate =>
            string.Equals(GetInvocationName(candidate), WithImageSha256MethodName, StringComparison.Ordinal));

        if (hasImageTag && hasImageSha256)
        {
            return;
        }

        context.ReportDiagnostic(
            Diagnostic.Create(
                ImageTagAndDigestRule,
                invocation.GetLocation(),
                methodName));
    }

    private static bool IsImagePinMethod(string? methodName)
    {
        return string.Equals(methodName, WithImageTagMethodName, StringComparison.Ordinal)
            || string.Equals(methodName, WithImageSha256MethodName, StringComparison.Ordinal);
    }

    private static bool HasInvalidDigest(SyntaxNodeAnalysisContext context, InvocationExpressionSyntax invocation)
    {
        var expression = invocation.ArgumentList.Arguments.FirstOrDefault()?.Expression;
        if (expression is null)
        {
            return false;
        }

        var constantValue = context.SemanticModel.GetConstantValue(expression, context.CancellationToken);
        return constantValue.HasValue
            && constantValue.Value is string digest
            && !IsBareSha256Digest(digest);
    }

    private static bool IsBareSha256Digest(string digest)
    {
        return digest.Length == 64
            && !digest.StartsWith(Sha256Prefix, StringComparison.Ordinal)
            && digest.All(static character =>
                character is (>= '0' and <= '9')
                    or (>= 'a' and <= 'f')
                    or (>= 'A' and <= 'F'));
    }

    private static InvocationExpressionSyntax GetOutermostInvocation(InvocationExpressionSyntax invocation)
    {
        var current = invocation;
        while (current.Parent is MemberAccessExpressionSyntax memberAccess
            && ReferenceEquals(memberAccess.Expression, current)
            && memberAccess.Parent is InvocationExpressionSyntax parentInvocation)
        {
            current = parentInvocation;
        }

        return current;
    }

    private static IEnumerable<InvocationExpressionSyntax> GetImageInvocationChain(InvocationExpressionSyntax invocation)
    {
        if (invocation.Expression is MemberAccessExpressionSyntax memberAccess
            && memberAccess.Expression is InvocationExpressionSyntax receiverInvocation)
        {
            foreach (var candidate in GetImageInvocationChain(receiverInvocation))
            {
                yield return candidate;
            }
        }

        yield return invocation;
    }

    private static string? GetInvocationName(InvocationExpressionSyntax invocation)
    {
        return invocation.Expression switch
        {
            MemberAccessExpressionSyntax memberAccess => memberAccess.Name.Identifier.ValueText,
            MemberBindingExpressionSyntax memberBinding => memberBinding.Name.Identifier.ValueText,
            IdentifierNameSyntax identifierName => identifierName.Identifier.ValueText,
            _ => null
        };
    }
}
