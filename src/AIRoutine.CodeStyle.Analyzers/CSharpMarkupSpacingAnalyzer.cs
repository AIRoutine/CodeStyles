using System.Collections.Generic;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace AIRoutine.CodeStyle.Analyzers;

/// <summary>
/// Analyzer that forbids inline visual property setters in C# Markup.
/// Visual properties like Padding, Margin, Background, Foreground should come from Style, not inline.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class CSharpMarkupSpacingAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "ACS0014";

    private static readonly LocalizableString Title =
        "Inline visual property not allowed in C# Markup";

    private static readonly LocalizableString MessageFormat =
        "Do not set '{0}' inline - visual properties should come from Style";

    private static readonly LocalizableString Description =
        "Visual properties (Padding, Margin, Background, Foreground, etc.) should be defined in Style, not set inline in C# Markup.";

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

    // Visual property methods that should come from Style, not inline
    private static readonly HashSet<string> ForbiddenInlineMethods = new(System.StringComparer.Ordinal)
    {
        // Spacing
        "Padding",
        "Margin",

        // Colors/Brushes
        "Background",
        "Foreground",
        "BorderBrush",
        "Fill",
        "Stroke",

        // Typography (should come from TextBlock style)
        "FontSize",
        "FontWeight",
        "FontFamily",

        // Sizing (should come from Style)
        "CornerRadius",
        "BorderThickness"
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

        // Get method name from fluent call like .Padding(...) or .Background(...)
        if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess)
            return;

        var methodName = memberAccess.Name.Identifier.Text;

        if (!ForbiddenInlineMethods.Contains(methodName))
            return;

        // Check if we're inside a Style/Resource definition (allowed there)
        if (IsInsideStyleDefinition(invocation))
            return;

        // Check if we're inside a Page class (UI layer - not allowed)
        // This is the main use case we want to catch
        var diagnostic = Diagnostic.Create(Rule, invocation.GetLocation(), methodName);
        context.ReportDiagnostic(diagnostic);
    }

    private static bool IsInsideStyleDefinition(SyntaxNode node)
    {
        var current = node.Parent;
        while (current != null)
        {
            // Check if we're inside a method that defines styles/resources
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

            // Check if we're in a class that's for styles/resources
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
