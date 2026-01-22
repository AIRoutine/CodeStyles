using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace AIRoutine.CodeStyle.Analyzers;

/// <summary>
/// Analyzer that enforces dependency injection by forbidding static method calls
/// on non-framework types. System.* and Microsoft.* namespaces are allowed.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class NoStaticCallsAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "ACS0002";

    private static readonly LocalizableString Title =
        "Static method call not allowed";

    private static readonly LocalizableString MessageFormat =
        "Static call '{0}.{1}()' is not allowed. Use dependency injection with a service interface instead.";

    private static readonly LocalizableString Description =
        "Static method calls on non-framework types violate dependency injection principles. Only System.* and Microsoft.* static calls are permitted.";

    private const string Category = "Design";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        Title,
        MessageFormat,
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: Description);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(Rule);

    // Allowed namespace prefixes
    private static readonly string[] AllowedNamespacePrefixes =
    {
        "System",
        "Microsoft",
        "Windows",
        "Xunit",
        "NUnit",
        "Moq",
        "NSubstitute",
        "FakeItEasy",
        "FluentAssertions",
        "AutoFixture",
        "Bogus"
    };

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterSyntaxNodeAction(AnalyzeInvocation, SyntaxKind.InvocationExpression);
    }

    private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;

        // We only care about member access expressions (Type.Method())
        if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess)
            return;

        // Get the symbol for the method being called
        var symbolInfo = context.SemanticModel.GetSymbolInfo(invocation);
        if (symbolInfo.Symbol is not IMethodSymbol methodSymbol)
            return;

        // Only check static methods
        if (!methodSymbol.IsStatic)
            return;

        // Get the containing type
        var containingType = methodSymbol.ContainingType;
        if (containingType == null)
            return;

        // Check if this is an extension method call (these are OK)
        if (methodSymbol.IsExtensionMethod)
            return;

        // Check if the call looks like an extension method invocation
        // (instance.Method() where Method is static extension)
        if (IsExtensionMethodStyleCall(memberAccess, context.SemanticModel))
            return;

        // Get the full namespace
        var namespaceName = GetFullNamespace(containingType);

        // Allow framework namespaces
        if (IsAllowedNamespace(namespaceName))
            return;

        // Allow enum member access (not really a static "call")
        if (containingType.TypeKind == TypeKind.Enum)
            return;

        // Allow const and static readonly field access disguised as method call
        // (This shouldn't happen for methods, but be safe)
        if (methodSymbol.MethodKind == MethodKind.BuiltinOperator ||
            methodSymbol.MethodKind == MethodKind.Conversion)
            return;

        // Allow calls inside attribute arguments
        if (IsInsideAttribute(invocation))
            return;

        // Allow calls in static constructors or static field initializers of the same type
        if (IsInStaticInitializerOfSameType(invocation, containingType, context.SemanticModel))
            return;

        // Allow static calls in Page classes (UI layer exception)
        if (IsInsidePageClass(invocation, context.SemanticModel))
            return;

        // This is a violation
        var typeName = containingType.Name;
        var methodName = methodSymbol.Name;

        var diagnostic = Diagnostic.Create(Rule, memberAccess.GetLocation(), typeName, methodName);
        context.ReportDiagnostic(diagnostic);
    }

    private static bool IsExtensionMethodStyleCall(MemberAccessExpressionSyntax memberAccess, SemanticModel semanticModel)
    {
        // If the left side of the member access is not a type name, it might be an extension method call
        var leftSymbol = semanticModel.GetSymbolInfo(memberAccess.Expression).Symbol;

        // If left side is a local, parameter, field, property, etc., this could be extension method style
        if (leftSymbol is ILocalSymbol or IParameterSymbol or IFieldSymbol or IPropertySymbol)
            return true;

        // If left side is 'this' or another instance expression
        if (memberAccess.Expression is ThisExpressionSyntax or BaseExpressionSyntax)
            return true;

        return false;
    }

    private static string GetFullNamespace(INamedTypeSymbol type)
    {
        var ns = type.ContainingNamespace;
        if (ns == null || ns.IsGlobalNamespace)
            return string.Empty;

        return ns.ToDisplayString();
    }

    private static bool IsAllowedNamespace(string namespaceName)
    {
        if (string.IsNullOrEmpty(namespaceName))
            return false;

        foreach (var prefix in AllowedNamespacePrefixes)
        {
            if (namespaceName.Equals(prefix, System.StringComparison.Ordinal) ||
                namespaceName.StartsWith(prefix + ".", System.StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsInsideAttribute(SyntaxNode node)
    {
        return node.FirstAncestorOrSelf<AttributeSyntax>() != null ||
               node.FirstAncestorOrSelf<AttributeArgumentSyntax>() != null;
    }

    private static bool IsInStaticInitializerOfSameType(
        InvocationExpressionSyntax invocation,
        INamedTypeSymbol calledType,
        SemanticModel semanticModel)
    {
        // Find the containing type declaration
        var containingTypeDecl = invocation.FirstAncestorOrSelf<TypeDeclarationSyntax>();
        if (containingTypeDecl == null)
            return false;

        var containingTypeSymbol = semanticModel.GetDeclaredSymbol(containingTypeDecl);
        if (containingTypeSymbol == null)
            return false;

        // Allow static calls to the same type (factory methods, etc.)
        if (SymbolEqualityComparer.Default.Equals(containingTypeSymbol, calledType))
            return true;

        return false;
    }

    private static bool IsInsidePageClass(SyntaxNode node, SemanticModel semanticModel)
    {
        // Find the containing type declaration
        var containingTypeDecl = node.FirstAncestorOrSelf<TypeDeclarationSyntax>();
        if (containingTypeDecl == null)
            return false;

        var containingTypeSymbol = semanticModel.GetDeclaredSymbol(containingTypeDecl);
        if (containingTypeSymbol == null)
            return false;

        // Check if the class name ends with "Page"
        return containingTypeSymbol.Name.EndsWith("Page", System.StringComparison.Ordinal);
    }
}
