using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace AIRoutine.CodeStyle.Analyzers;

/// <summary>
/// Analyzer that detects hardcoded colors in C# code.
/// Colors should be defined in ResourceDictionary and accessed via resource lookup.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class NoHardcodedColorsAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "ACS0003";

    private static readonly LocalizableString Title =
        "Hardcoded color detected";

    private static readonly LocalizableString MessageFormat =
        "Hardcoded color '{0}' detected. Define colors in ResourceDictionary and use resource lookup.";

    private static readonly LocalizableString Description =
        "Colors should not be hardcoded in C# code. Use ResourceDictionary definitions and resource lookup for maintainability and theming support.";

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

        // Check member access (Colors.Red, Color.FromArgb)
        context.RegisterSyntaxNodeAction(AnalyzeMemberAccess, SyntaxKind.SimpleMemberAccessExpression);

        // Check object creation (new SolidColorBrush(...))
        context.RegisterSyntaxNodeAction(AnalyzeObjectCreation, SyntaxKind.ObjectCreationExpression);

        // Check string literals for hex colors
        context.RegisterSyntaxNodeAction(AnalyzeStringLiteral, SyntaxKind.StringLiteralExpression);
    }

    private static void AnalyzeMemberAccess(SyntaxNodeAnalysisContext context)
    {
        var memberAccess = (MemberAccessExpressionSyntax)context.Node;
        var symbolInfo = context.SemanticModel.GetSymbolInfo(memberAccess);

        if (symbolInfo.Symbol == null)
            return;

        // Check for Colors.* property access
        if (symbolInfo.Symbol is IPropertySymbol propertySymbol)
        {
            var containingType = propertySymbol.ContainingType;
            if (containingType != null && IsColorsType(containingType))
            {
                // Skip Transparent as it's often used legitimately
                if (propertySymbol.Name == "Transparent")
                    return;

                var diagnostic = Diagnostic.Create(Rule, memberAccess.GetLocation(), $"Colors.{propertySymbol.Name}");
                context.ReportDiagnostic(diagnostic);
                return;
            }
        }

        // Check for Color.FromArgb/FromRgb method calls
        if (symbolInfo.Symbol is IMethodSymbol methodSymbol)
        {
            var containingType = methodSymbol.ContainingType;
            if (containingType != null && IsColorType(containingType))
            {
                if (methodSymbol.Name == "FromArgb" || methodSymbol.Name == "FromRgb" ||
                    methodSymbol.Name == "Parse" || methodSymbol.Name == "FromHex")
                {
                    var diagnostic = Diagnostic.Create(Rule, memberAccess.GetLocation(), $"Color.{methodSymbol.Name}()");
                    context.ReportDiagnostic(diagnostic);
                }
            }

            // Check for ColorHelper.FromArgb
            if (containingType != null && containingType.Name == "ColorHelper")
            {
                if (methodSymbol.Name == "FromArgb" || methodSymbol.Name == "ToColor")
                {
                    var diagnostic = Diagnostic.Create(Rule, memberAccess.GetLocation(), $"ColorHelper.{methodSymbol.Name}()");
                    context.ReportDiagnostic(diagnostic);
                }
            }
        }
    }

    private static void AnalyzeObjectCreation(SyntaxNodeAnalysisContext context)
    {
        var objectCreation = (ObjectCreationExpressionSyntax)context.Node;
        var symbolInfo = context.SemanticModel.GetSymbolInfo(objectCreation);

        if (symbolInfo.Symbol is not IMethodSymbol constructorSymbol)
            return;

        var containingType = constructorSymbol.ContainingType;
        if (containingType == null)
            return;

        // Check for new SolidColorBrush(Colors.*)
        if (IsBrushType(containingType))
        {
            // Check if any argument references Colors.*
            if (objectCreation.ArgumentList != null)
            {
                foreach (var argument in objectCreation.ArgumentList.Arguments)
                {
                    if (ContainsHardcodedColor(argument.Expression, context.SemanticModel))
                    {
                        var diagnostic = Diagnostic.Create(Rule, objectCreation.GetLocation(), $"new {containingType.Name}(...)");
                        context.ReportDiagnostic(diagnostic);
                        return;
                    }
                }
            }
        }
    }

    private static void AnalyzeStringLiteral(SyntaxNodeAnalysisContext context)
    {
        var literal = (LiteralExpressionSyntax)context.Node;
        var value = literal.Token.ValueText;

        // Check for hex color strings
        if (IsHexColorString(value))
        {
            // Check if this is used in a color context
            var parent = literal.Parent;

            // Skip if it's just a string variable (could be any hex value)
            // Only report if it's clearly used as a color
            if (IsInColorContext(literal, context.SemanticModel))
            {
                var diagnostic = Diagnostic.Create(Rule, literal.GetLocation(), $"\"{value}\"");
                context.ReportDiagnostic(diagnostic);
            }
        }
    }

    private static bool IsColorsType(INamedTypeSymbol type)
    {
        // Check for Windows.UI.Colors, Microsoft.UI.Colors, System.Windows.Media.Colors
        if (type.Name != "Colors")
            return false;

        var ns = type.ContainingNamespace?.ToDisplayString() ?? "";
        return ns == "Windows.UI" ||
               ns == "Microsoft.UI" ||
               ns == "System.Windows.Media" ||
               ns.StartsWith("Windows.UI") ||
               ns.StartsWith("Microsoft.UI");
    }

    private static bool IsColorType(INamedTypeSymbol type)
    {
        if (type.Name != "Color")
            return false;

        var ns = type.ContainingNamespace?.ToDisplayString() ?? "";
        return ns == "Windows.UI" ||
               ns == "Microsoft.UI" ||
               ns == "System.Windows.Media" ||
               ns == "System.Drawing" ||
               ns.StartsWith("Windows.UI") ||
               ns.StartsWith("Microsoft.UI");
    }

    private static bool IsBrushType(INamedTypeSymbol type)
    {
        return type.Name == "SolidColorBrush" ||
               type.Name == "LinearGradientBrush" ||
               type.Name == "RadialGradientBrush";
    }

    private static bool ContainsHardcodedColor(ExpressionSyntax expression, SemanticModel semanticModel)
    {
        // Check if expression is Colors.* access
        if (expression is MemberAccessExpressionSyntax memberAccess)
        {
            var symbol = semanticModel.GetSymbolInfo(memberAccess).Symbol;
            if (symbol is IPropertySymbol prop && prop.ContainingType != null && IsColorsType(prop.ContainingType))
                return true;
        }

        // Check nested expressions
        foreach (var child in expression.DescendantNodes().OfType<MemberAccessExpressionSyntax>())
        {
            var symbol = semanticModel.GetSymbolInfo(child).Symbol;
            if (symbol is IPropertySymbol prop && prop.ContainingType != null && IsColorsType(prop.ContainingType))
                return true;
        }

        return false;
    }

    private static bool IsHexColorString(string value)
    {
        if (string.IsNullOrEmpty(value))
            return false;

        if (!value.StartsWith("#"))
            return false;

        var hex = value.Substring(1);

        // Valid color formats: #RGB, #ARGB, #RRGGBB, #AARRGGBB
        if (hex.Length != 3 && hex.Length != 4 && hex.Length != 6 && hex.Length != 8)
            return false;

        foreach (var c in hex)
        {
            if (!IsHexDigit(c))
                return false;
        }

        return true;
    }

    private static bool IsHexDigit(char c)
    {
        return (c >= '0' && c <= '9') ||
               (c >= 'A' && c <= 'F') ||
               (c >= 'a' && c <= 'f');
    }

    private static bool IsInColorContext(LiteralExpressionSyntax literal, SemanticModel semanticModel)
    {
        // Check if parent is a method call like Color.Parse("#...")
        var parent = literal.Parent;
        if (parent is ArgumentSyntax argument)
        {
            var invocation = argument.FirstAncestorOrSelf<InvocationExpressionSyntax>();
            if (invocation != null)
            {
                var symbol = semanticModel.GetSymbolInfo(invocation).Symbol;
                if (symbol is IMethodSymbol method)
                {
                    var typeName = method.ContainingType?.Name ?? "";
                    if (typeName == "Color" || typeName == "ColorHelper" || typeName == "ColorConverter")
                        return true;
                }
            }
        }

        // Check if assigned to a Color-typed variable or property
        var assignment = literal.FirstAncestorOrSelf<AssignmentExpressionSyntax>();
        if (assignment != null)
        {
            var typeInfo = semanticModel.GetTypeInfo(assignment.Left);
            if (typeInfo.Type?.Name == "Color")
                return true;
        }

        return false;
    }
}
