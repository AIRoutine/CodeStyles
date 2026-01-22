using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace AIRoutine.CodeStyle.Analyzers;

/// <summary>
/// Analyzer that detects async void methods which should be async Task instead.
/// Event handlers are excluded as they require async void signatures.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class AsyncVoidAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "ACS0009";

    private static readonly LocalizableString Title =
        "Async void method detected";

    private static readonly LocalizableString MessageFormat =
        "Method '{0}' is async void. Use async Task instead to enable proper error handling and awaiting.";

    private static readonly LocalizableString Description =
        "Async void methods cannot be awaited and exceptions thrown from them cannot be caught. Use async Task instead, except for event handlers.";

    private const string Category = "Reliability";

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

    // Common event handler parameter type patterns
    private static readonly string[] EventHandlerParameterTypes =
    {
        "EventArgs",
        "RoutedEventArgs",
        "PointerRoutedEventArgs",
        "TappedRoutedEventArgs",
        "DoubleTappedRoutedEventArgs",
        "RightTappedRoutedEventArgs",
        "KeyRoutedEventArgs",
        "ManipulationStartedRoutedEventArgs",
        "ManipulationDeltaRoutedEventArgs",
        "ManipulationCompletedRoutedEventArgs",
        "DragEventArgs",
        "TextChangedEventArgs",
        "SelectionChangedEventArgs",
        "ItemClickEventArgs",
        "NavigationEventArgs",
        "CancelEventArgs",
        "PropertyChangedEventArgs",
        "NotifyCollectionChangedEventArgs"
    };

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterSyntaxNodeAction(AnalyzeMethod, SyntaxKind.MethodDeclaration);
        context.RegisterSyntaxNodeAction(AnalyzeLocalFunction, SyntaxKind.LocalFunctionStatement);
        context.RegisterSyntaxNodeAction(AnalyzeLambda, SyntaxKind.ParenthesizedLambdaExpression);
        context.RegisterSyntaxNodeAction(AnalyzeLambda, SyntaxKind.SimpleLambdaExpression);
        context.RegisterSyntaxNodeAction(AnalyzeAnonymousMethod, SyntaxKind.AnonymousMethodExpression);
    }

    private static void AnalyzeMethod(SyntaxNodeAnalysisContext context)
    {
        var method = (MethodDeclarationSyntax)context.Node;

        // Check if method is async
        if (!method.Modifiers.Any(SyntaxKind.AsyncKeyword))
            return;

        // Check if return type is void
        if (method.ReturnType is not PredefinedTypeSyntax predefinedType ||
            !predefinedType.Keyword.IsKind(SyntaxKind.VoidKeyword))
            return;

        // Check if it's an event handler
        if (IsEventHandler(method, context.SemanticModel))
            return;

        // Check if it overrides an event handler from base class
        var methodSymbol = context.SemanticModel.GetDeclaredSymbol(method);
        if (methodSymbol != null && IsOverridingEventHandler(methodSymbol))
            return;

        var diagnostic = Diagnostic.Create(Rule, method.Identifier.GetLocation(), method.Identifier.Text);
        context.ReportDiagnostic(diagnostic);
    }

    private static void AnalyzeLocalFunction(SyntaxNodeAnalysisContext context)
    {
        var localFunction = (LocalFunctionStatementSyntax)context.Node;

        // Check if method is async
        if (!localFunction.Modifiers.Any(SyntaxKind.AsyncKeyword))
            return;

        // Check if return type is void
        if (localFunction.ReturnType is not PredefinedTypeSyntax predefinedType ||
            !predefinedType.Keyword.IsKind(SyntaxKind.VoidKeyword))
            return;

        // Local functions should never be async void - they can't be event handlers
        var diagnostic = Diagnostic.Create(Rule, localFunction.Identifier.GetLocation(), localFunction.Identifier.Text);
        context.ReportDiagnostic(diagnostic);
    }

    private static void AnalyzeLambda(SyntaxNodeAnalysisContext context)
    {
        var lambda = (LambdaExpressionSyntax)context.Node;

        // Check if lambda is async
        if (!lambda.Modifiers.Any(SyntaxKind.AsyncKeyword))
            return;

        // Get the type info for the lambda
        var typeInfo = context.SemanticModel.GetTypeInfo(lambda);
        var convertedType = typeInfo.ConvertedType as INamedTypeSymbol;

        if (convertedType == null)
            return;

        // Check if it's being converted to an Action (async void)
        if (IsActionDelegate(convertedType))
        {
            // Check if this is being used as an event handler
            if (IsLambdaUsedAsEventHandler(lambda, context.SemanticModel))
                return;

            var location = lambda.AsyncKeyword.GetLocation();
            var diagnostic = Diagnostic.Create(Rule, location, "lambda");
            context.ReportDiagnostic(diagnostic);
        }
    }

    private static void AnalyzeAnonymousMethod(SyntaxNodeAnalysisContext context)
    {
        var anonymousMethod = (AnonymousMethodExpressionSyntax)context.Node;

        // Check if method is async
        if (!anonymousMethod.Modifiers.Any(SyntaxKind.AsyncKeyword))
            return;

        // Get the type info
        var typeInfo = context.SemanticModel.GetTypeInfo(anonymousMethod);
        var convertedType = typeInfo.ConvertedType as INamedTypeSymbol;

        if (convertedType == null)
            return;

        // Check if it's being converted to an Action (async void)
        if (IsActionDelegate(convertedType))
        {
            // Check if this is being used as an event handler
            if (IsAnonymousMethodUsedAsEventHandler(anonymousMethod, context.SemanticModel))
                return;

            var location = anonymousMethod.DelegateKeyword.GetLocation();
            var diagnostic = Diagnostic.Create(Rule, location, "anonymous method");
            context.ReportDiagnostic(diagnostic);
        }
    }

    private static bool IsEventHandler(MethodDeclarationSyntax method, SemanticModel semanticModel)
    {
        var parameters = method.ParameterList.Parameters;

        // Classic event handler pattern: (object sender, EventArgs e)
        if (parameters.Count == 2)
        {
            var firstParam = parameters[0];
            var secondParam = parameters[1];

            // First parameter is typically 'object' or 'object?'
            var firstType = semanticModel.GetTypeInfo(firstParam.Type!).Type;
            if (firstType?.SpecialType == SpecialType.System_Object)
            {
                // Second parameter derives from EventArgs
                var secondType = semanticModel.GetTypeInfo(secondParam.Type!).Type;
                if (secondType != null && IsEventArgsType(secondType))
                    return true;
            }
        }

        // Check for common event handler naming patterns
        var methodName = method.Identifier.Text;
        if (methodName.EndsWith("_Click") ||
            methodName.EndsWith("_Tapped") ||
            methodName.EndsWith("_PointerPressed") ||
            methodName.EndsWith("_PointerReleased") ||
            methodName.EndsWith("_Loaded") ||
            methodName.EndsWith("_Unloaded") ||
            methodName.EndsWith("_Changed") ||
            methodName.EndsWith("_SelectionChanged") ||
            methodName.EndsWith("_TextChanged") ||
            methodName.EndsWith("_Checked") ||
            methodName.EndsWith("_Unchecked") ||
            methodName.EndsWith("_Toggled") ||
            methodName.StartsWith("On") && char.IsUpper(methodName[2]))
        {
            // If it matches naming pattern and has EventArgs-like parameter, allow it
            if (parameters.Count >= 1)
            {
                var lastParam = parameters.Last();
                var lastType = semanticModel.GetTypeInfo(lastParam.Type!).Type;
                if (lastType != null && IsEventArgsType(lastType))
                    return true;
            }
        }

        return false;
    }

    private static bool IsEventArgsType(ITypeSymbol type)
    {
        // Check if type name ends with EventArgs
        if (type.Name.EndsWith("EventArgs"))
            return true;

        // Check if it's one of the known event args types
        if (EventHandlerParameterTypes.Contains(type.Name))
            return true;

        // Check base types
        var baseType = type.BaseType;
        while (baseType != null)
        {
            if (baseType.Name == "EventArgs" ||
                baseType.Name.EndsWith("EventArgs"))
                return true;
            baseType = baseType.BaseType;
        }

        return false;
    }

    private static bool IsOverridingEventHandler(IMethodSymbol method)
    {
        // Check if this method overrides a base method that is an event handler
        var overriddenMethod = method.OverriddenMethod;
        while (overriddenMethod != null)
        {
            if (overriddenMethod.Parameters.Length == 2)
            {
                var secondParam = overriddenMethod.Parameters[1];
                if (IsEventArgsType(secondParam.Type))
                    return true;
            }
            overriddenMethod = overriddenMethod.OverriddenMethod;
        }

        // Check implemented interface methods
        foreach (var interfaceMethod in method.ContainingType.AllInterfaces
            .SelectMany(i => i.GetMembers().OfType<IMethodSymbol>()))
        {
            if (method.ContainingType.FindImplementationForInterfaceMember(interfaceMethod)?.Equals(method, SymbolEqualityComparer.Default) == true)
            {
                if (interfaceMethod.Parameters.Length == 2)
                {
                    var secondParam = interfaceMethod.Parameters[1];
                    if (IsEventArgsType(secondParam.Type))
                        return true;
                }
            }
        }

        return false;
    }

    private static bool IsActionDelegate(INamedTypeSymbol type)
    {
        // Check if it's System.Action or similar void-returning delegate
        if (type.Name == "Action" && type.ContainingNamespace?.ToDisplayString() == "System")
            return true;

        // Check if delegate returns void
        if (type.DelegateInvokeMethod?.ReturnsVoid == true)
            return true;

        return false;
    }

    private static bool IsLambdaUsedAsEventHandler(LambdaExpressionSyntax lambda, SemanticModel semanticModel)
    {
        // Check if lambda is being assigned to an event
        if (lambda.Parent is AssignmentExpressionSyntax assignment)
        {
            if (assignment.IsKind(SyntaxKind.AddAssignmentExpression))
            {
                var leftSymbol = semanticModel.GetSymbolInfo(assignment.Left).Symbol;
                if (leftSymbol is IEventSymbol)
                    return true;
            }
        }

        // Check if lambda is an argument to an event subscription method
        if (lambda.Parent is ArgumentSyntax argument)
        {
            var invocation = argument.FirstAncestorOrSelf<InvocationExpressionSyntax>();
            if (invocation != null)
            {
                var methodName = GetMethodName(invocation);
                if (methodName != null && (
                    methodName.Contains("Subscribe") ||
                    methodName.Contains("AddHandler") ||
                    methodName.Contains("Register")))
                    return true;
            }
        }

        return false;
    }

    private static bool IsAnonymousMethodUsedAsEventHandler(AnonymousMethodExpressionSyntax anonymousMethod, SemanticModel semanticModel)
    {
        // Same logic as lambda
        if (anonymousMethod.Parent is AssignmentExpressionSyntax assignment)
        {
            if (assignment.IsKind(SyntaxKind.AddAssignmentExpression))
            {
                var leftSymbol = semanticModel.GetSymbolInfo(assignment.Left).Symbol;
                if (leftSymbol is IEventSymbol)
                    return true;
            }
        }

        return false;
    }

    private static string? GetMethodName(InvocationExpressionSyntax invocation)
    {
        return invocation.Expression switch
        {
            MemberAccessExpressionSyntax memberAccess => memberAccess.Name.Identifier.Text,
            IdentifierNameSyntax identifier => identifier.Identifier.Text,
            _ => null
        };
    }
}
