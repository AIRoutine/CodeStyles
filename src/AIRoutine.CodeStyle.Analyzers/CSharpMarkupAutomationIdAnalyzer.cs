using System.Collections.Immutable;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace AIRoutine.CodeStyle.Analyzers;

/// <summary>
/// Analyzer that validates AutomationId format in C# Markup.
/// AutomationId should follow the pattern: PageName.ControlType.Purpose or PageName.Purpose
/// Example: "LoginPage.Button.Submit", "SettingsPage.Username"
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class CSharpMarkupAutomationIdAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "ACS0016";

    private static readonly LocalizableString Title =
        "Invalid AutomationId format in C# Markup";

    private static readonly LocalizableString MessageFormat =
        "AutomationId '{0}' should follow the pattern 'PageName.ControlType.Purpose' or 'PageName.Purpose' (e.g., 'LoginPage.Button.Submit')";

    private static readonly LocalizableString Description =
        "AutomationId values should follow a consistent naming pattern for maintainability and test automation: PageName.ControlType.Purpose or PageName.Purpose.";

    private const string Category = "Naming";

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

    // Valid AutomationId pattern: PascalCase.PascalCase or PascalCase.PascalCase.PascalCase
    // Examples: MainPage.Root, LoginPage.Button.Submit, SettingsPage.TextBox.Username
    private static readonly Regex ValidAutomationIdPattern = new(
        @"^[A-Z][a-zA-Z0-9]*(\.[A-Z][a-zA-Z0-9]*){1,2}$",
        RegexOptions.Compiled);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterSyntaxNodeAction(AnalyzeInvocation, SyntaxKind.InvocationExpression);
    }

    private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;

        // Check for .AutomationId(...) call
        if (invocation.Expression is MemberAccessExpressionSyntax memberAccess)
        {
            var methodName = memberAccess.Name.Identifier.Text;

            if (methodName == "AutomationId")
            {
                CheckAutomationIdArgument(invocation, context);
            }
        }

        // Check for .AutomationProperties(ap => ap.AutomationId(...)) pattern
        if (invocation.Expression is MemberAccessExpressionSyntax memberAccess2 &&
            memberAccess2.Name.Identifier.Text == "AutomationProperties")
        {
            CheckAutomationPropertiesLambda(invocation, context);
        }
    }

    private static void CheckAutomationIdArgument(InvocationExpressionSyntax invocation, SyntaxNodeAnalysisContext context)
    {
        if (invocation.ArgumentList == null || invocation.ArgumentList.Arguments.Count == 0)
            return;

        var firstArg = invocation.ArgumentList.Arguments[0];
        if (firstArg.Expression is LiteralExpressionSyntax literal &&
            literal.IsKind(SyntaxKind.StringLiteralExpression))
        {
            var value = literal.Token.ValueText;
            ValidateAutomationId(value, literal.GetLocation(), context);
        }
        else if (firstArg.Expression is InterpolatedStringExpressionSyntax)
        {
            // Interpolated strings are OK if they follow the pattern
            // We can't fully validate at compile time, so we skip
        }
    }

    private static void CheckAutomationPropertiesLambda(InvocationExpressionSyntax invocation, SyntaxNodeAnalysisContext context)
    {
        if (invocation.ArgumentList == null || invocation.ArgumentList.Arguments.Count == 0)
            return;

        var firstArg = invocation.ArgumentList.Arguments[0];

        // Look for lambda: ap => ap.AutomationId("...")
        if (firstArg.Expression is SimpleLambdaExpressionSyntax lambda)
        {
            CheckLambdaBody(lambda.Body, context);
        }
        else if (firstArg.Expression is ParenthesizedLambdaExpressionSyntax parenLambda)
        {
            CheckLambdaBody(parenLambda.Body, context);
        }
    }

    private static void CheckLambdaBody(CSharpSyntaxNode body, SyntaxNodeAnalysisContext context)
    {
        // Find all AutomationId invocations in the lambda body
        var automationIdCalls = body.DescendantNodes()
            .OfType<InvocationExpressionSyntax>()
            .Where(inv => inv.Expression is MemberAccessExpressionSyntax ma &&
                          ma.Name.Identifier.Text == "AutomationId");

        foreach (var call in automationIdCalls)
        {
            if (call.ArgumentList != null && call.ArgumentList.Arguments.Count > 0)
            {
                var arg = call.ArgumentList.Arguments[0];
                if (arg.Expression is LiteralExpressionSyntax literal &&
                    literal.IsKind(SyntaxKind.StringLiteralExpression))
                {
                    var value = literal.Token.ValueText;
                    ValidateAutomationId(value, literal.GetLocation(), context);
                }
            }
        }
    }

    private static void ValidateAutomationId(string value, Location location, SyntaxNodeAnalysisContext context)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            var diagnostic = Diagnostic.Create(Rule, location, "(empty)");
            context.ReportDiagnostic(diagnostic);
            return;
        }

        // Check if it matches the valid pattern
        if (!ValidAutomationIdPattern.IsMatch(value))
        {
            var diagnostic = Diagnostic.Create(Rule, location, value);
            context.ReportDiagnostic(diagnostic);
        }
    }
}
