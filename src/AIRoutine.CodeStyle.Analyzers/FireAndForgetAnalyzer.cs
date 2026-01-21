using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace AIRoutine.CodeStyle.Analyzers;

/// <summary>
/// Analyzer that detects fire-and-forget async calls without proper error handling.
/// Unawaited Tasks can silently swallow exceptions, making debugging difficult.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class FireAndForgetAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "ACS0011";

    private static readonly LocalizableString Title =
        "Fire-and-forget async call without error handling";

    private static readonly LocalizableString MessageFormat =
        "Task returned by '{0}' is not awaited. Exceptions will be silently swallowed. Use 'await', '.ContinueWith()', or a safe fire-and-forget helper.";

    private static readonly LocalizableString Description =
        "Async methods that return Tasks should be awaited or have their exceptions handled. Unawaited Tasks silently swallow exceptions.";

    private const string Category = "Reliability";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        Title,
        MessageFormat,
        Category,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: Description);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(Rule);

    // Methods that are commonly used for safe fire-and-forget
    private static readonly string[] SafeFireAndForgetMethods =
    {
        "SafeFireAndForget",
        "FireAndForget",
        "Forget",
        "HandleExceptions",
        "ObserveException",
        "ContinueWith",
        "ConfigureAwait"
    };

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterSyntaxNodeAction(AnalyzeExpressionStatement, SyntaxKind.ExpressionStatement);
    }

    private static void AnalyzeExpressionStatement(SyntaxNodeAnalysisContext context)
    {
        var expressionStatement = (ExpressionStatementSyntax)context.Node;
        var expression = expressionStatement.Expression;

        // Skip if expression is an assignment (result is captured)
        if (expression is AssignmentExpressionSyntax)
            return;

        // Skip if prefixed with discard (_ = task)
        if (expression is AssignmentExpressionSyntax assignment &&
            assignment.Left is IdentifierNameSyntax identifier &&
            identifier.Identifier.Text == "_")
            return;

        // Get the type of the expression
        var typeInfo = context.SemanticModel.GetTypeInfo(expression);
        var type = typeInfo.Type;

        if (type == null)
            return;

        // Check if it returns a Task/ValueTask
        if (!IsTaskType(type))
            return;

        // Check if this is an invocation
        var invocation = expression as InvocationExpressionSyntax;
        if (invocation == null)
        {
            // Could be a conditional access like obj?.MethodAsync()
            if (expression is ConditionalAccessExpressionSyntax conditionalAccess &&
                conditionalAccess.WhenNotNull is InvocationExpressionSyntax conditionalInvocation)
            {
                invocation = conditionalInvocation;
            }
            else
            {
                return;
            }
        }

        // Get the method name
        var methodName = GetMethodName(invocation);
        if (methodName == null)
            return;

        // Check if using a safe fire-and-forget pattern
        if (IsUsingSafeFireAndForget(invocation))
            return;

        // Check if inside a try-catch block
        if (IsInsideTryCatch(expressionStatement))
            return;

        // Check if explicitly suppressed with pragma
        if (HasSuppressingComment(expressionStatement))
            return;

        // Check for common async patterns that are OK
        if (IsAllowedAsyncPattern(methodName))
            return;

        var diagnostic = Diagnostic.Create(Rule, invocation.GetLocation(), methodName);
        context.ReportDiagnostic(diagnostic);
    }

    private static bool IsTaskType(ITypeSymbol type)
    {
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

    private static string? GetMethodName(InvocationExpressionSyntax invocation)
    {
        return invocation.Expression switch
        {
            MemberAccessExpressionSyntax memberAccess => memberAccess.Name.Identifier.Text,
            IdentifierNameSyntax identifier => identifier.Identifier.Text,
            GenericNameSyntax generic => generic.Identifier.Text,
            MemberBindingExpressionSyntax memberBinding => memberBinding.Name.Identifier.Text,
            _ => null
        };
    }

    private static bool IsUsingSafeFireAndForget(InvocationExpressionSyntax invocation)
    {
        // Check if the call is chained with a safe fire-and-forget method
        // e.g., DoWorkAsync().SafeFireAndForget()
        var parent = invocation.Parent;

        // Check for method chain
        if (parent is MemberAccessExpressionSyntax parentMemberAccess &&
            parentMemberAccess.Parent is InvocationExpressionSyntax parentInvocation)
        {
            var parentMethodName = GetMethodName(parentInvocation);
            if (parentMethodName != null && SafeFireAndForgetMethods.Contains(parentMethodName))
                return true;
        }

        // Check if this IS the safe fire-and-forget call
        var methodName = GetMethodName(invocation);
        if (methodName != null)
        {
            // Check if calling ContinueWith or ConfigureAwait (indicates awareness)
            if (methodName == "ContinueWith")
            {
                // Check if ContinueWith has error handling
                var args = invocation.ArgumentList.Arguments;
                if (args.Count > 0)
                {
                    var argText = args[0].ToString();
                    // If continuation accesses .Exception or .IsFaulted, it's handling errors
                    if (argText.Contains("Exception") || argText.Contains("IsFaulted"))
                        return true;
                }
            }
        }

        // Check if wrapped in try-catch or has error handling delegate
        if (invocation.ArgumentList.Arguments.Any(arg =>
        {
            var argText = arg.ToString().ToLowerInvariant();
            return argText.Contains("exception") || argText.Contains("error") || argText.Contains("catch");
        }))
        {
            return true;
        }

        return false;
    }

    private static bool IsInsideTryCatch(SyntaxNode node)
    {
        // Check if inside a try block
        var tryStatement = node.FirstAncestorOrSelf<TryStatementSyntax>();
        if (tryStatement != null)
        {
            // Verify the node is in the try block, not the catch/finally
            if (tryStatement.Block.Contains(node))
                return true;
        }

        return false;
    }

    private static bool HasSuppressingComment(ExpressionStatementSyntax statement)
    {
        // Check for comments like "// fire and forget" or "// intentionally not awaited"
        var trivia = statement.GetLeadingTrivia()
            .Concat(statement.GetTrailingTrivia());

        foreach (var t in trivia)
        {
            if (t.IsKind(SyntaxKind.SingleLineCommentTrivia) ||
                t.IsKind(SyntaxKind.MultiLineCommentTrivia))
            {
                var commentText = t.ToString().ToLowerInvariant();
                if (commentText.Contains("fire and forget") ||
                    commentText.Contains("fire-and-forget") ||
                    commentText.Contains("intentionally") ||
                    commentText.Contains("deliberately") ||
                    commentText.Contains("ignore") ||
                    commentText.Contains("don't await") ||
                    commentText.Contains("not awaited"))
                    return true;
            }
        }

        // Also check the line before
        var parent = statement.Parent;
        if (parent != null)
        {
            var siblings = parent.ChildNodes().ToList();
            var index = siblings.IndexOf(statement);
            if (index > 0)
            {
                var previousSibling = siblings[index - 1];
                var prevTrivia = previousSibling.GetTrailingTrivia();
                foreach (var t in prevTrivia)
                {
                    if (t.IsKind(SyntaxKind.SingleLineCommentTrivia))
                    {
                        var commentText = t.ToString().ToLowerInvariant();
                        if (commentText.Contains("fire and forget") ||
                            commentText.Contains("fire-and-forget"))
                            return true;
                    }
                }
            }
        }

        return false;
    }

    private static bool IsAllowedAsyncPattern(string methodName)
    {
        // Some methods are commonly fire-and-forget by design
        var allowedPatterns = new[]
        {
            "Dispatcher", // UI dispatcher operations
            "BeginInvoke", // Async invocation
            "Post", // SynchronizationContext.Post
            "Send", // SynchronizationContext.Send (though this is sync)
            "Enqueue", // Queue operations
            "Schedule" // Scheduler operations
        };

        return allowedPatterns.Any(p => methodName.Contains(p));
    }
}
