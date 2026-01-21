using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace AIRoutine.CodeStyle.Analyzers;

/// <summary>
/// Analyzer that detects blocking calls on Tasks such as .Result, .Wait(), .GetAwaiter().GetResult().
/// These can cause deadlocks in UI applications and should be replaced with await.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class BlockingCallAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "ACS0010";

    private static readonly LocalizableString Title =
        "Blocking call on async operation detected";

    private static readonly LocalizableString MessageFormat =
        "Blocking call '{0}' can cause deadlocks. Use 'await' instead.";

    private static readonly LocalizableString Description =
        "Blocking calls like .Result, .Wait(), or .GetAwaiter().GetResult() on Tasks can cause deadlocks in UI applications. Use async/await pattern instead.";

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

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterSyntaxNodeAction(AnalyzeMemberAccess, SyntaxKind.SimpleMemberAccessExpression);
        context.RegisterSyntaxNodeAction(AnalyzeInvocation, SyntaxKind.InvocationExpression);
    }

    private static void AnalyzeMemberAccess(SyntaxNodeAnalysisContext context)
    {
        var memberAccess = (MemberAccessExpressionSyntax)context.Node;

        // Check for .Result access
        if (memberAccess.Name.Identifier.Text != "Result")
            return;

        // Verify it's accessing a Task's Result property
        var expressionType = context.SemanticModel.GetTypeInfo(memberAccess.Expression).Type;
        if (expressionType == null || !IsTaskType(expressionType))
            return;

        // Allow in static constructors and field initializers (where async is not possible)
        if (IsInStaticInitializer(memberAccess))
            return;

        // Allow in Main method without async (entry point)
        if (IsInSyncMainMethod(memberAccess, context.SemanticModel))
            return;

        // Allow in xUnit/NUnit test constructors (where async is not supported)
        if (IsInTestConstructor(memberAccess, context.SemanticModel))
            return;

        var diagnostic = Diagnostic.Create(Rule, memberAccess.Name.GetLocation(), ".Result");
        context.ReportDiagnostic(diagnostic);
    }

    private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;

        if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess)
            return;

        var methodName = memberAccess.Name.Identifier.Text;

        // Check for .Wait() call
        if (methodName == "Wait")
        {
            var expressionType = context.SemanticModel.GetTypeInfo(memberAccess.Expression).Type;
            if (expressionType != null && IsTaskType(expressionType))
            {
                if (IsInAllowedContext(invocation, context.SemanticModel))
                    return;

                var diagnostic = Diagnostic.Create(Rule, memberAccess.Name.GetLocation(), ".Wait()");
                context.ReportDiagnostic(diagnostic);
                return;
            }
        }

        // Check for .GetAwaiter().GetResult() pattern
        if (methodName == "GetResult")
        {
            if (memberAccess.Expression is InvocationExpressionSyntax innerInvocation &&
                innerInvocation.Expression is MemberAccessExpressionSyntax innerMemberAccess &&
                innerMemberAccess.Name.Identifier.Text == "GetAwaiter")
            {
                var expressionType = context.SemanticModel.GetTypeInfo(innerMemberAccess.Expression).Type;
                if (expressionType != null && IsTaskType(expressionType))
                {
                    if (IsInAllowedContext(invocation, context.SemanticModel))
                        return;

                    var diagnostic = Diagnostic.Create(Rule, memberAccess.GetLocation(), ".GetAwaiter().GetResult()");
                    context.ReportDiagnostic(diagnostic);
                    return;
                }
            }
        }

        // Check for Task.WaitAll() and Task.WaitAny()
        if (methodName == "WaitAll" || methodName == "WaitAny")
        {
            var symbolInfo = context.SemanticModel.GetSymbolInfo(invocation);
            if (symbolInfo.Symbol is IMethodSymbol method &&
                method.ContainingType?.Name == "Task" &&
                method.ContainingNamespace?.ToDisplayString() == "System.Threading.Tasks")
            {
                if (IsInAllowedContext(invocation, context.SemanticModel))
                    return;

                var diagnostic = Diagnostic.Create(Rule, memberAccess.Name.GetLocation(), $".{methodName}()");
                context.ReportDiagnostic(diagnostic);
            }
        }
    }

    private static bool IsTaskType(ITypeSymbol type)
    {
        // Check for Task, Task<T>, ValueTask, ValueTask<T>
        var typeName = type.Name;
        var namespaceName = type.ContainingNamespace?.ToDisplayString() ?? string.Empty;

        if (namespaceName == "System.Threading.Tasks")
        {
            if (typeName == "Task" || typeName == "ValueTask")
                return true;
        }

        // Check for Task<T> or ValueTask<T>
        if (type is INamedTypeSymbol namedType && namedType.IsGenericType)
        {
            var originalDefinition = namedType.OriginalDefinition;
            if (originalDefinition.Name == "Task" || originalDefinition.Name == "ValueTask")
            {
                if (originalDefinition.ContainingNamespace?.ToDisplayString() == "System.Threading.Tasks")
                    return true;
            }
        }

        return false;
    }

    private static bool IsInAllowedContext(SyntaxNode node, SemanticModel semanticModel)
    {
        return IsInStaticInitializer(node) ||
               IsInSyncMainMethod(node, semanticModel) ||
               IsInTestConstructor(node, semanticModel) ||
               IsInConfigureAwaitFalseContext(node) ||
               IsInConsoleApp(node, semanticModel);
    }

    private static bool IsInStaticInitializer(SyntaxNode node)
    {
        // Check if inside static constructor
        var staticConstructor = node.FirstAncestorOrSelf<ConstructorDeclarationSyntax>();
        if (staticConstructor != null && staticConstructor.Modifiers.Any(SyntaxKind.StaticKeyword))
            return true;

        // Check if inside static field initializer
        var fieldDeclaration = node.FirstAncestorOrSelf<FieldDeclarationSyntax>();
        if (fieldDeclaration != null && fieldDeclaration.Modifiers.Any(SyntaxKind.StaticKeyword))
            return true;

        // Check if inside static property initializer
        var propertyDeclaration = node.FirstAncestorOrSelf<PropertyDeclarationSyntax>();
        if (propertyDeclaration != null &&
            propertyDeclaration.Modifiers.Any(SyntaxKind.StaticKeyword) &&
            propertyDeclaration.Initializer != null)
        {
            if (node.SpanStart >= propertyDeclaration.Initializer.SpanStart)
                return true;
        }

        return false;
    }

    private static bool IsInSyncMainMethod(SyntaxNode node, SemanticModel semanticModel)
    {
        var method = node.FirstAncestorOrSelf<MethodDeclarationSyntax>();
        if (method == null)
            return false;

        if (method.Identifier.Text != "Main")
            return false;

        // Check if it's static and not async
        if (!method.Modifiers.Any(SyntaxKind.StaticKeyword))
            return false;

        if (method.Modifiers.Any(SyntaxKind.AsyncKeyword))
            return false;

        return true;
    }

    private static bool IsInTestConstructor(SyntaxNode node, SemanticModel semanticModel)
    {
        var constructor = node.FirstAncestorOrSelf<ConstructorDeclarationSyntax>();
        if (constructor == null)
            return false;

        // Check if containing class has test attributes
        var containingClass = constructor.FirstAncestorOrSelf<ClassDeclarationSyntax>();
        if (containingClass == null)
            return false;

        var classSymbol = semanticModel.GetDeclaredSymbol(containingClass);
        if (classSymbol == null)
            return false;

        // Check for common test framework attributes
        foreach (var attribute in classSymbol.GetAttributes())
        {
            var attrName = attribute.AttributeClass?.Name ?? string.Empty;
            if (attrName == "TestClass" || // MSTest
                attrName == "TestFixture" || // NUnit
                attrName.EndsWith("Collection")) // xUnit
                return true;
        }

        // Check if any methods have test attributes (xUnit doesn't require class attributes)
        foreach (var member in classSymbol.GetMembers().OfType<IMethodSymbol>())
        {
            foreach (var attribute in member.GetAttributes())
            {
                var attrName = attribute.AttributeClass?.Name ?? string.Empty;
                if (attrName == "Fact" || attrName == "Theory" || // xUnit
                    attrName == "Test" || attrName == "TestCase" || // NUnit
                    attrName == "TestMethod") // MSTest
                    return true;
            }
        }

        return false;
    }

    private static bool IsInConfigureAwaitFalseContext(SyntaxNode node)
    {
        // Check if we're in a context that already uses ConfigureAwait(false)
        // This indicates awareness of sync context issues, so blocking might be intentional
        var containingMethod = node.FirstAncestorOrSelf<MethodDeclarationSyntax>();
        if (containingMethod == null)
            return false;

        // Look for ConfigureAwait(false) calls in the same method
        var configureAwaitCalls = containingMethod.DescendantNodes()
            .OfType<InvocationExpressionSyntax>()
            .Where(inv =>
            {
                if (inv.Expression is MemberAccessExpressionSyntax ma &&
                    ma.Name.Identifier.Text == "ConfigureAwait")
                {
                    var args = inv.ArgumentList.Arguments;
                    if (args.Count == 1 &&
                        args[0].Expression is LiteralExpressionSyntax literal &&
                        literal.IsKind(SyntaxKind.FalseLiteralExpression))
                        return true;
                }
                return false;
            });

        return configureAwaitCalls.Any();
    }

    private static bool IsInConsoleApp(SyntaxNode node, SemanticModel semanticModel)
    {
        // Check if this appears to be a console application (no UI thread concerns)
        var compilation = semanticModel.Compilation;

        // Look for Main method that is synchronous - likely console app
        foreach (var syntaxTree in compilation.SyntaxTrees.Take(5)) // Check first few files
        {
            var root = syntaxTree.GetRoot();
            var mainMethods = root.DescendantNodes()
                .OfType<MethodDeclarationSyntax>()
                .Where(m => m.Identifier.Text == "Main" &&
                           m.Modifiers.Any(SyntaxKind.StaticKeyword) &&
                           !m.Modifiers.Any(SyntaxKind.AsyncKeyword));

            if (mainMethods.Any())
            {
                // Has sync Main - might be console app, but still warn as this is a general best practice
                // Return false to still report the diagnostic
            }
        }

        return false;
    }
}
