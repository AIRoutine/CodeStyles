using System;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace AIRoutine.CodeStyle.Analyzers;

/// <summary>
/// Analyzer that forbids manual ViewModel registration in the DI container.
/// ViewModels must be registered via Uno Extensions ViewMap instead of
/// services.AddTransient/AddScoped/AddSingleton.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ViewModelRegistrationAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "ACS0020";

    private static readonly LocalizableString Title =
        "Use Uno Extensions ViewMap instead of manual ViewModel registration";

    private static readonly LocalizableString MessageFormat =
        "{0} Register ViewModels via ViewMap<TView, TViewModel> in your route configuration instead";

    private static readonly LocalizableString Description =
        "Manual ViewModel registration in the DI container is forbidden. ViewModels must be registered through Uno Extensions' ViewMap system, which automatically handles ViewModel lifecycle, DataContext assignment, and navigation data flow.";

    private const string Category = "Design";

    private static readonly DiagnosticDescriptor Rule = new(
        id: DiagnosticId,
        title: Title,
        messageFormat: MessageFormat,
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: Description,
        helpLinkUri: "https://platform.uno/docs/articles/external/uno.extensions/doc/Reference/Navigation/ViewMap.html");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(Rule);

    private static readonly string[] ForbiddenDiMethods =
    {
        "AddTransient",
        "AddScoped",
        "AddSingleton",
        "AddKeyedTransient",
        "AddKeyedScoped",
        "AddKeyedSingleton"
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

        // Extract method name from member access (e.g., services.AddTransient<T>())
        string? methodName = null;
        if (invocation.Expression is MemberAccessExpressionSyntax memberAccess)
        {
            methodName = memberAccess.Name.Identifier.Text;
        }
        else if (invocation.Expression is GenericNameSyntax genericName)
        {
            methodName = genericName.Identifier.Text;
        }
        else if (invocation.Expression is MemberBindingExpressionSyntax memberBinding)
        {
            methodName = memberBinding.Name.Identifier.Text;
        }

        if (methodName == null || !ForbiddenDiMethods.Contains(methodName))
            return;

        // Verify this is actually a Microsoft.Extensions.DependencyInjection method
        var symbolInfo = context.SemanticModel.GetSymbolInfo(invocation);
        if (symbolInfo.Symbol is not IMethodSymbol methodSymbol)
            return;

        var containingNamespace = methodSymbol.ContainingType?.ContainingNamespace?.ToDisplayString();
        if (containingNamespace?.StartsWith("Microsoft.Extensions.DependencyInjection", StringComparison.Ordinal) != true)
            return;

        // Check generic type arguments for ViewModel types
        if (methodSymbol.TypeArguments.Length > 0)
        {
            foreach (var typeArg in methodSymbol.TypeArguments)
            {
                if (IsViewModelType(typeArg.Name))
                {
                    var diagnostic = Diagnostic.Create(
                        Rule,
                        invocation.GetLocation(),
                        $"Manual registration of '{typeArg.Name}' in the DI container is forbidden.");
                    context.ReportDiagnostic(diagnostic);
                    return;
                }
            }
        }

        // Check typeof() arguments for ViewModel types
        foreach (var argument in invocation.ArgumentList.Arguments)
        {
            if (argument.Expression is TypeOfExpressionSyntax typeOfExpression)
            {
                var typeInfo = context.SemanticModel.GetTypeInfo(typeOfExpression.Type);
                if (typeInfo.Type != null && IsViewModelType(typeInfo.Type.Name))
                {
                    var diagnostic = Diagnostic.Create(
                        Rule,
                        invocation.GetLocation(),
                        $"Manual registration of '{typeInfo.Type.Name}' in the DI container is forbidden.");
                    context.ReportDiagnostic(diagnostic);
                    return;
                }
            }
        }
    }

    private static bool IsViewModelType(string typeName)
        => typeName.EndsWith("ViewModel", StringComparison.Ordinal);
}
