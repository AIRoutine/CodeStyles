using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace AIRoutine.CodeStyle.Analyzers;

/// <summary>
/// Analyzer that validates spacing values in C# Markup methods.
/// Detects .Padding(...), .Margin(...), .Spacing(...) calls with non-standard values.
///
/// Configuration (via .editorconfig or .globalconfig):
///   dotnet_diagnostic.ACS0014.allowed_spacing_values = 0,2,4,8,12,16,20,24,32,48,64
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class CSharpMarkupSpacingAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "ACS0014";
    public const string AllowedValuesConfigKey = "dotnet_diagnostic.ACS0014.allowed_spacing_values";

    // Default allowed spacing values (standard 4px scale)
    private static readonly int[] DefaultAllowedValues = { 0, 2, 4, 8, 12, 16, 20, 24, 32, 48, 64 };

    private static readonly LocalizableString Title =
        "Non-standard spacing value in C# Markup";

    private static readonly LocalizableString MessageFormat =
        "Spacing value '{0}' is not in the allowed spacing scale (allowed: {1})";

    private static readonly LocalizableString Description =
        "Spacing values (Padding, Margin, Spacing) should use consistent values from the design system spacing scale.";

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

    // Method names that represent spacing
    private static readonly HashSet<string> SpacingMethods = new(System.StringComparer.OrdinalIgnoreCase)
    {
        "Padding",
        "Margin",
        "Spacing"
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

        if (methodName == null || !SpacingMethods.Contains(methodName))
            return;

        // Get allowed values from configuration
        var allowedValues = GetAllowedValues(context);
        var allowedStr = string.Join(",", allowedValues.OrderBy(v => v));

        // Check arguments
        if (invocation.ArgumentList == null)
            return;

        var invalidValues = new List<int>();

        foreach (var argument in invocation.ArgumentList.Arguments)
        {
            CheckSpacingValue(argument.Expression, allowedValues, invalidValues, context.SemanticModel);
        }

        if (invalidValues.Count > 0)
        {
            var invalidStr = string.Join(", ", invalidValues.Distinct().OrderBy(v => v));
            var diagnostic = Diagnostic.Create(Rule, invocation.GetLocation(), invalidStr, allowedStr);
            context.ReportDiagnostic(diagnostic);
        }
    }

    private static void CheckSpacingValue(
        ExpressionSyntax expression,
        HashSet<int> allowedValues,
        List<int> invalidValues,
        SemanticModel semanticModel)
    {
        // Handle numeric literals
        if (expression is LiteralExpressionSyntax literal)
        {
            if (literal.Token.Value is int intValue)
            {
                var absValue = System.Math.Abs(intValue);
                if (!allowedValues.Contains(absValue))
                {
                    invalidValues.Add(intValue);
                }
            }
            else if (literal.Token.Value is double doubleValue)
            {
                var absValue = (int)System.Math.Abs(doubleValue);
                if (!allowedValues.Contains(absValue))
                {
                    invalidValues.Add((int)doubleValue);
                }
            }
        }
        // Handle negative numbers (prefixed with -)
        else if (expression is PrefixUnaryExpressionSyntax prefixUnary &&
                 prefixUnary.OperatorToken.IsKind(SyntaxKind.MinusToken))
        {
            if (prefixUnary.Operand is LiteralExpressionSyntax innerLiteral)
            {
                if (innerLiteral.Token.Value is int intValue)
                {
                    var absValue = System.Math.Abs(intValue);
                    if (!allowedValues.Contains(absValue))
                    {
                        invalidValues.Add(-intValue);
                    }
                }
            }
        }
        // Handle Thickness or similar struct constructors
        else if (expression is ObjectCreationExpressionSyntax objectCreation)
        {
            if (objectCreation.ArgumentList != null)
            {
                foreach (var arg in objectCreation.ArgumentList.Arguments)
                {
                    CheckSpacingValue(arg.Expression, allowedValues, invalidValues, semanticModel);
                }
            }
        }
        // Handle implicit object creation (new(...))
        else if (expression is ImplicitObjectCreationExpressionSyntax implicitCreation)
        {
            if (implicitCreation.ArgumentList != null)
            {
                foreach (var arg in implicitCreation.ArgumentList.Arguments)
                {
                    CheckSpacingValue(arg.Expression, allowedValues, invalidValues, semanticModel);
                }
            }
        }
    }

    private static HashSet<int> GetAllowedValues(SyntaxNodeAnalysisContext context)
    {
        var options = context.Options.AnalyzerConfigOptionsProvider.GetOptions(context.Node.SyntaxTree);

        if (options.TryGetValue(AllowedValuesConfigKey, out var valuesStr) &&
            !string.IsNullOrWhiteSpace(valuesStr))
        {
            var values = new HashSet<int>();
            foreach (var part in valuesStr.Split(','))
            {
                if (int.TryParse(part.Trim(), out var value))
                {
                    values.Add(value);
                }
            }
            if (values.Count > 0)
                return values;
        }

        return new HashSet<int>(DefaultAllowedValues);
    }
}
