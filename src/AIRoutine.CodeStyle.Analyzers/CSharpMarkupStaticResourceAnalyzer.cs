using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace AIRoutine.CodeStyle.Analyzers;

/// <summary>
/// Analyzer that forbids hardcoded strings in StaticResource/ThemeResource calls.
/// Resource keys must be constants from a *.Core.Styles project/namespace.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class CSharpMarkupStaticResourceAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "ACS0017";

    private static readonly LocalizableString Title =
        "StaticResource must use constant from Core.Styles";

    private static readonly LocalizableString MessageFormat =
        "Do not use hardcoded string '{0}' in StaticResource - use a constant from *.Core.Styles (e.g., StyleKeys.{1})";

    private static readonly LocalizableString Description =
        "StaticResource and ThemeResource keys must reference constants from a Core.Styles project for maintainability and compile-time safety.";

    private const string Category = "Maintainability";

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

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterSyntaxNodeAction(AnalyzeInvocation, SyntaxKind.InvocationExpression);
    }

    private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;

        // Get method name
        string? methodName = null;
        if (invocation.Expression is MemberAccessExpressionSyntax memberAccess)
        {
            methodName = memberAccess.Name.Identifier.Text;
        }
        else if (invocation.Expression is IdentifierNameSyntax identifier)
        {
            methodName = identifier.Identifier.Text;
        }

        // Check for StaticResource or ThemeResource
        if (methodName != "StaticResource" && methodName != "ThemeResource")
            return;

        // Check if we're inside a Style/Resource definition (allowed there)
        if (IsInsideStyleDefinition(invocation))
            return;

        // Check arguments
        if (invocation.ArgumentList == null || invocation.ArgumentList.Arguments.Count == 0)
            return;

        var firstArg = invocation.ArgumentList.Arguments[0];

        // If it's a string literal, that's forbidden
        if (firstArg.Expression is LiteralExpressionSyntax literal &&
            literal.IsKind(SyntaxKind.StringLiteralExpression))
        {
            var value = literal.Token.ValueText;
            var suggestedName = GetSuggestedConstantName(value);
            var diagnostic = Diagnostic.Create(Rule, literal.GetLocation(), value, suggestedName);
            context.ReportDiagnostic(diagnostic);
            return;
        }

        // If it's an interpolated string, that's also forbidden
        if (firstArg.Expression is InterpolatedStringExpressionSyntax interpolated)
        {
            var diagnostic = Diagnostic.Create(Rule, interpolated.GetLocation(), "(interpolated)", "YourKey");
            context.ReportDiagnostic(diagnostic);
            return;
        }

        // If it's a constant reference, check if it comes from *.Core.Styles or *Styles namespace
        if (firstArg.Expression is MemberAccessExpressionSyntax constantAccess)
        {
            var symbol = context.SemanticModel.GetSymbolInfo(constantAccess).Symbol;
            if (symbol is IFieldSymbol fieldSymbol && fieldSymbol.IsConst)
            {
                // Check if the containing type/namespace includes "Styles" or "Core.Styles"
                var containingType = fieldSymbol.ContainingType;
                var fullName = containingType?.ToDisplayString() ?? "";

                if (!IsFromStylesNamespace(fullName))
                {
                    var diagnostic = Diagnostic.Create(
                        Rule,
                        constantAccess.GetLocation(),
                        $"{containingType?.Name}.{fieldSymbol.Name}",
                        fieldSymbol.Name);
                    context.ReportDiagnostic(diagnostic);
                }
            }
        }
        else if (firstArg.Expression is IdentifierNameSyntax identifierArg)
        {
            // Simple identifier - check if it's a const from the right namespace
            var symbol = context.SemanticModel.GetSymbolInfo(identifierArg).Symbol;
            if (symbol is IFieldSymbol fieldSymbol && fieldSymbol.IsConst)
            {
                var containingType = fieldSymbol.ContainingType;
                var fullName = containingType?.ToDisplayString() ?? "";

                if (!IsFromStylesNamespace(fullName))
                {
                    var diagnostic = Diagnostic.Create(
                        Rule,
                        identifierArg.GetLocation(),
                        fieldSymbol.Name,
                        fieldSymbol.Name);
                    context.ReportDiagnostic(diagnostic);
                }
            }
            // If it's a local const or parameter, it's still forbidden (should be from Styles)
            else if (symbol is ILocalSymbol || symbol is IParameterSymbol)
            {
                var diagnostic = Diagnostic.Create(
                    Rule,
                    identifierArg.GetLocation(),
                    identifierArg.Identifier.Text,
                    identifierArg.Identifier.Text);
                context.ReportDiagnostic(diagnostic);
            }
        }
    }

    private static bool IsFromStylesNamespace(string fullTypeName)
    {
        // Allow constants from namespaces/types containing "Styles" or "Core.Styles"
        return fullTypeName.Contains("Styles") ||
               fullTypeName.Contains("StyleKeys") ||
               fullTypeName.Contains("ResourceKeys") ||
               fullTypeName.Contains("ThemeKeys") ||
               fullTypeName.Contains("BrushKeys") ||
               fullTypeName.Contains("ColorKeys");
    }

    private static string GetSuggestedConstantName(string resourceKey)
    {
        // Convert "MyButtonStyle" to "MyButtonStyle" (keep as-is for suggestion)
        // Remove common suffixes for cleaner suggestion
        var name = resourceKey;
        if (name.EndsWith("Style"))
            name = name.Substring(0, name.Length - 5) + "Style";
        else if (name.EndsWith("Brush"))
            name = name.Substring(0, name.Length - 5) + "Brush";

        return name;
    }

    private static bool IsInsideStyleDefinition(SyntaxNode node)
    {
        var current = node.Parent;
        while (current != null)
        {
            if (current is MethodDeclarationSyntax method)
            {
                var methodName = method.Identifier.Text;
                if (methodName.Contains("Style") ||
                    methodName.Contains("Resource") ||
                    methodName.Contains("Theme") ||
                    methodName.Contains("Template"))
                {
                    return true;
                }
            }

            if (current is ClassDeclarationSyntax classDecl)
            {
                var className = classDecl.Identifier.Text;
                if (className.Contains("Style") ||
                    className.Contains("Resource") ||
                    className.Contains("Theme") ||
                    className.Contains("Dictionary"))
                {
                    return true;
                }
            }

            current = current.Parent;
        }
        return false;
    }
}
