using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace AIRoutine.CodeStyle.Analyzers;

/// <summary>
/// Provides code fixes for BlockingCallAnalyzer (ACS0010).
/// Replaces blocking calls like .Result, .Wait(), and .GetAwaiter().GetResult() with await.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(BlockingCallCodeFixProvider))]
[Shared]
public sealed class BlockingCallCodeFixProvider : CodeFixProvider
{
    public override ImmutableArray<string> FixableDiagnosticIds =>
        ImmutableArray.Create(BlockingCallAnalyzer.DiagnosticId);

    public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root == null) return;

        var diagnostic = context.Diagnostics.First();
        var diagnosticSpan = diagnostic.Location.SourceSpan;

        var token = root.FindToken(diagnosticSpan.Start);
        var node = token.Parent;

        // Determine the type of blocking call
        var memberAccess = node?.AncestorsAndSelf().OfType<MemberAccessExpressionSyntax>().FirstOrDefault();

        if (memberAccess == null) return;

        var memberName = memberAccess.Name.Identifier.Text;

        if (memberName == "Result")
        {
            context.RegisterCodeFix(
                CodeAction.Create(
                    title: "Use await instead of .Result",
                    createChangedDocument: c => ReplaceResultWithAwaitAsync(context.Document, memberAccess, c),
                    equivalenceKey: "ReplaceResultWithAwait"),
                diagnostic);
        }
        else if (memberName == "Wait" || memberName == "WaitAll" || memberName == "WaitAny")
        {
            var invocation = memberAccess.Parent as InvocationExpressionSyntax;
            if (invocation != null)
            {
                context.RegisterCodeFix(
                    CodeAction.Create(
                        title: "Use await instead of .Wait()",
                        createChangedDocument: c => ReplaceWaitWithAwaitAsync(context.Document, invocation, memberName, c),
                        equivalenceKey: "ReplaceWaitWithAwait"),
                    diagnostic);
            }
        }
        else if (memberName == "GetResult")
        {
            var invocation = memberAccess.Parent as InvocationExpressionSyntax;
            if (invocation != null)
            {
                context.RegisterCodeFix(
                    CodeAction.Create(
                        title: "Use await instead of .GetAwaiter().GetResult()",
                        createChangedDocument: c => ReplaceGetResultWithAwaitAsync(context.Document, invocation, c),
                        equivalenceKey: "ReplaceGetResultWithAwait"),
                    diagnostic);
            }
        }
    }

    private static async Task<Document> ReplaceResultWithAwaitAsync(
        Document document,
        MemberAccessExpressionSyntax memberAccess,
        CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root == null) return document;

        // Get the task expression (the part before .Result)
        var taskExpression = memberAccess.Expression;

        // Create await expression
        var awaitExpression = SyntaxFactory.AwaitExpression(
            SyntaxFactory.ParenthesizedExpression(taskExpression)
                .WithoutTrivia())
            .WithLeadingTrivia(memberAccess.GetLeadingTrivia())
            .WithTrailingTrivia(memberAccess.GetTrailingTrivia());

        // If task expression is simple (identifier or member access), no need for parentheses
        if (taskExpression is IdentifierNameSyntax or MemberAccessExpressionSyntax)
        {
            awaitExpression = SyntaxFactory.AwaitExpression(taskExpression.WithoutTrivia())
                .WithLeadingTrivia(memberAccess.GetLeadingTrivia())
                .WithTrailingTrivia(memberAccess.GetTrailingTrivia());
        }

        var newRoot = root.ReplaceNode(memberAccess, awaitExpression);

        // Ensure containing method is async
        newRoot = EnsureMethodIsAsync(newRoot, awaitExpression);

        return document.WithSyntaxRoot(newRoot);
    }

    private static async Task<Document> ReplaceWaitWithAwaitAsync(
        Document document,
        InvocationExpressionSyntax invocation,
        string methodName,
        CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root == null) return document;

        if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess)
            return document;

        ExpressionSyntax newExpression;

        if (methodName == "WaitAll")
        {
            // Task.WaitAll(tasks) -> await Task.WhenAll(tasks)
            var whenAllAccess = SyntaxFactory.MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                memberAccess.Expression,
                SyntaxFactory.IdentifierName("WhenAll"));

            var whenAllInvocation = SyntaxFactory.InvocationExpression(whenAllAccess, invocation.ArgumentList);

            newExpression = SyntaxFactory.AwaitExpression(whenAllInvocation)
                .WithLeadingTrivia(invocation.GetLeadingTrivia())
                .WithTrailingTrivia(invocation.GetTrailingTrivia());
        }
        else if (methodName == "WaitAny")
        {
            // Task.WaitAny(tasks) -> await Task.WhenAny(tasks)
            var whenAnyAccess = SyntaxFactory.MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                memberAccess.Expression,
                SyntaxFactory.IdentifierName("WhenAny"));

            var whenAnyInvocation = SyntaxFactory.InvocationExpression(whenAnyAccess, invocation.ArgumentList);

            newExpression = SyntaxFactory.AwaitExpression(whenAnyInvocation)
                .WithLeadingTrivia(invocation.GetLeadingTrivia())
                .WithTrailingTrivia(invocation.GetTrailingTrivia());
        }
        else
        {
            // task.Wait() -> await task
            var taskExpression = memberAccess.Expression;
            newExpression = SyntaxFactory.AwaitExpression(taskExpression.WithoutTrivia())
                .WithLeadingTrivia(invocation.GetLeadingTrivia())
                .WithTrailingTrivia(invocation.GetTrailingTrivia());
        }

        var newRoot = root.ReplaceNode(invocation, newExpression);
        newRoot = EnsureMethodIsAsync(newRoot, newExpression);

        return document.WithSyntaxRoot(newRoot);
    }

    private static async Task<Document> ReplaceGetResultWithAwaitAsync(
        Document document,
        InvocationExpressionSyntax invocation,
        CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root == null) return document;

        // Navigate to the original task: task.GetAwaiter().GetResult()
        // invocation is GetResult()
        // invocation.Expression is memberAccess for GetResult
        // memberAccess.Expression is GetAwaiter() invocation
        // GetAwaiter invocation's Expression is memberAccess for GetAwaiter
        // That memberAccess.Expression is the original task

        if (invocation.Expression is not MemberAccessExpressionSyntax getResultAccess)
            return document;

        if (getResultAccess.Expression is not InvocationExpressionSyntax getAwaiterInvocation)
            return document;

        if (getAwaiterInvocation.Expression is not MemberAccessExpressionSyntax getAwaiterAccess)
            return document;

        var taskExpression = getAwaiterAccess.Expression;

        var awaitExpression = SyntaxFactory.AwaitExpression(taskExpression.WithoutTrivia())
            .WithLeadingTrivia(invocation.GetLeadingTrivia())
            .WithTrailingTrivia(invocation.GetTrailingTrivia());

        var newRoot = root.ReplaceNode(invocation, awaitExpression);
        newRoot = EnsureMethodIsAsync(newRoot, awaitExpression);

        return document.WithSyntaxRoot(newRoot);
    }

    private static SyntaxNode EnsureMethodIsAsync(SyntaxNode root, SyntaxNode awaitExpression)
    {
        // Find the containing method
        var method = awaitExpression.FirstAncestorOrSelf<MethodDeclarationSyntax>();
        if (method != null && !method.Modifiers.Any(SyntaxKind.AsyncKeyword))
        {
            // Add async modifier
            var asyncModifier = SyntaxFactory.Token(SyntaxKind.AsyncKeyword)
                .WithTrailingTrivia(SyntaxFactory.Space);

            var newModifiers = method.Modifiers.Add(asyncModifier);

            // Update return type to Task or Task<T>
            TypeSyntax newReturnType;
            if (method.ReturnType is PredefinedTypeSyntax predefined &&
                predefined.Keyword.IsKind(SyntaxKind.VoidKeyword))
            {
                newReturnType = SyntaxFactory.IdentifierName("Task")
                    .WithTrailingTrivia(SyntaxFactory.Space);
            }
            else
            {
                // Wrap existing return type in Task<T>
                newReturnType = SyntaxFactory.GenericName(
                    SyntaxFactory.Identifier("Task"),
                    SyntaxFactory.TypeArgumentList(
                        SyntaxFactory.SingletonSeparatedList(method.ReturnType.WithoutTrivia())))
                    .WithTrailingTrivia(SyntaxFactory.Space);
            }

            var newMethod = method
                .WithModifiers(newModifiers)
                .WithReturnType(newReturnType);

            root = root.ReplaceNode(method, newMethod);
        }

        // Also check for local functions
        var localFunction = awaitExpression.FirstAncestorOrSelf<LocalFunctionStatementSyntax>();
        if (localFunction != null && !localFunction.Modifiers.Any(SyntaxKind.AsyncKeyword))
        {
            var asyncModifier = SyntaxFactory.Token(SyntaxKind.AsyncKeyword)
                .WithTrailingTrivia(SyntaxFactory.Space);

            var newModifiers = localFunction.Modifiers.Add(asyncModifier);

            TypeSyntax newReturnType;
            if (localFunction.ReturnType is PredefinedTypeSyntax predefined &&
                predefined.Keyword.IsKind(SyntaxKind.VoidKeyword))
            {
                newReturnType = SyntaxFactory.IdentifierName("Task")
                    .WithTrailingTrivia(SyntaxFactory.Space);
            }
            else
            {
                newReturnType = SyntaxFactory.GenericName(
                    SyntaxFactory.Identifier("Task"),
                    SyntaxFactory.TypeArgumentList(
                        SyntaxFactory.SingletonSeparatedList(localFunction.ReturnType.WithoutTrivia())))
                    .WithTrailingTrivia(SyntaxFactory.Space);
            }

            var newLocalFunction = localFunction
                .WithModifiers(newModifiers)
                .WithReturnType(newReturnType);

            root = root.ReplaceNode(localFunction, newLocalFunction);
        }

        return root;
    }
}
