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
/// Provides code fixes for FireAndForgetAnalyzer (ACS0011).
/// Offers to await the task, assign to discard, or wrap with error handling.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(FireAndForgetCodeFixProvider))]
[Shared]
public sealed class FireAndForgetCodeFixProvider : CodeFixProvider
{
    public override ImmutableArray<string> FixableDiagnosticIds =>
        ImmutableArray.Create(FireAndForgetAnalyzer.DiagnosticId);

    public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root == null) return;

        var diagnostic = context.Diagnostics.First();
        var diagnosticSpan = diagnostic.Location.SourceSpan;

        var token = root.FindToken(diagnosticSpan.Start);
        var invocation = token.Parent?.AncestorsAndSelf()
            .OfType<InvocationExpressionSyntax>().FirstOrDefault();

        if (invocation == null) return;

        var expressionStatement = invocation.FirstAncestorOrSelf<ExpressionStatementSyntax>();
        if (expressionStatement == null) return;

        // Option 1: Add await
        context.RegisterCodeFix(
            CodeAction.Create(
                title: "Await the task",
                createChangedDocument: c => AddAwaitAsync(context.Document, expressionStatement, invocation, c),
                equivalenceKey: "AwaitTask"),
            diagnostic);

        // Option 2: Assign to discard with comment
        context.RegisterCodeFix(
            CodeAction.Create(
                title: "Assign to discard (_ = ...)",
                createChangedDocument: c => AssignToDiscardAsync(context.Document, expressionStatement, c),
                equivalenceKey: "AssignToDiscard"),
            diagnostic);

        // Option 3: Add ContinueWith for error handling
        context.RegisterCodeFix(
            CodeAction.Create(
                title: "Add error handling with ContinueWith",
                createChangedDocument: c => AddContinueWithAsync(context.Document, expressionStatement, invocation, c),
                equivalenceKey: "AddContinueWith"),
            diagnostic);
    }

    private static async Task<Document> AddAwaitAsync(
        Document document,
        ExpressionStatementSyntax expressionStatement,
        InvocationExpressionSyntax invocation,
        CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root == null) return document;

        // Create await expression
        var awaitExpression = SyntaxFactory.AwaitExpression(invocation.WithoutTrivia())
            .WithLeadingTrivia(expressionStatement.GetLeadingTrivia());

        var newStatement = SyntaxFactory.ExpressionStatement(awaitExpression)
            .WithTrailingTrivia(expressionStatement.GetTrailingTrivia());

        var newRoot = root.ReplaceNode(expressionStatement, newStatement);

        // Ensure containing method is async
        newRoot = EnsureMethodIsAsync(newRoot, newStatement);

        return document.WithSyntaxRoot(newRoot);
    }

    private static async Task<Document> AssignToDiscardAsync(
        Document document,
        ExpressionStatementSyntax expressionStatement,
        CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root == null) return document;

        // Create: _ = expression; // Fire and forget
        var discardAssignment = SyntaxFactory.AssignmentExpression(
            SyntaxKind.SimpleAssignmentExpression,
            SyntaxFactory.IdentifierName("_"),
            expressionStatement.Expression.WithoutTrivia());

        var comment = SyntaxFactory.Comment(" // Fire and forget - exceptions are intentionally ignored");

        var newStatement = SyntaxFactory.ExpressionStatement(discardAssignment)
            .WithLeadingTrivia(expressionStatement.GetLeadingTrivia())
            .WithTrailingTrivia(
                SyntaxFactory.TriviaList(comment)
                    .AddRange(expressionStatement.GetTrailingTrivia()));

        var newRoot = root.ReplaceNode(expressionStatement, newStatement);

        return document.WithSyntaxRoot(newRoot);
    }

    private static async Task<Document> AddContinueWithAsync(
        Document document,
        ExpressionStatementSyntax expressionStatement,
        InvocationExpressionSyntax invocation,
        CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root == null) return document;

        // Create: task.ContinueWith(t => { if (t.IsFaulted) Console.WriteLine(t.Exception); }, TaskContinuationOptions.OnlyOnFaulted);
        // Simpler version: _ = task.ContinueWith(t => { /* log t.Exception */ }, TaskContinuationOptions.OnlyOnFaulted);

        var continueWithCall = SyntaxFactory.InvocationExpression(
            SyntaxFactory.MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                invocation.WithoutTrivia(),
                SyntaxFactory.IdentifierName("ContinueWith")),
            SyntaxFactory.ArgumentList(
                SyntaxFactory.SeparatedList(new[]
                {
                    SyntaxFactory.Argument(
                        SyntaxFactory.SimpleLambdaExpression(
                            SyntaxFactory.Parameter(SyntaxFactory.Identifier("t")),
                            SyntaxFactory.Block(
                                SyntaxFactory.SingletonList<StatementSyntax>(
                                    SyntaxFactory.ExpressionStatement(
                                        SyntaxFactory.InvocationExpression(
                                            SyntaxFactory.MemberAccessExpression(
                                                SyntaxKind.SimpleMemberAccessExpression,
                                                SyntaxFactory.IdentifierName("System.Diagnostics.Debug"),
                                                SyntaxFactory.IdentifierName("WriteLine")),
                                            SyntaxFactory.ArgumentList(
                                                SyntaxFactory.SingletonSeparatedList(
                                                    SyntaxFactory.Argument(
                                                        SyntaxFactory.MemberAccessExpression(
                                                            SyntaxKind.SimpleMemberAccessExpression,
                                                            SyntaxFactory.IdentifierName("t"),
                                                            SyntaxFactory.IdentifierName("Exception"))))))))))),
                    SyntaxFactory.Argument(
                        SyntaxFactory.MemberAccessExpression(
                            SyntaxKind.SimpleMemberAccessExpression,
                            SyntaxFactory.IdentifierName("TaskContinuationOptions"),
                            SyntaxFactory.IdentifierName("OnlyOnFaulted")))
                })));

        // Wrap in discard assignment
        var discardAssignment = SyntaxFactory.AssignmentExpression(
            SyntaxKind.SimpleAssignmentExpression,
            SyntaxFactory.IdentifierName("_"),
            continueWithCall);

        var newStatement = SyntaxFactory.ExpressionStatement(discardAssignment)
            .WithLeadingTrivia(expressionStatement.GetLeadingTrivia())
            .WithTrailingTrivia(expressionStatement.GetTrailingTrivia());

        var newRoot = root.ReplaceNode(expressionStatement, newStatement);

        return document.WithSyntaxRoot(newRoot);
    }

    private static SyntaxNode EnsureMethodIsAsync(SyntaxNode root, SyntaxNode containingNode)
    {
        var method = containingNode.FirstAncestorOrSelf<MethodDeclarationSyntax>();
        if (method != null && !method.Modifiers.Any(SyntaxKind.AsyncKeyword))
        {
            var asyncModifier = SyntaxFactory.Token(SyntaxKind.AsyncKeyword)
                .WithTrailingTrivia(SyntaxFactory.Space);

            var newModifiers = method.Modifiers.Add(asyncModifier);

            TypeSyntax newReturnType;
            if (method.ReturnType is PredefinedTypeSyntax predefined &&
                predefined.Keyword.IsKind(SyntaxKind.VoidKeyword))
            {
                newReturnType = SyntaxFactory.IdentifierName("Task")
                    .WithTrailingTrivia(SyntaxFactory.Space);
            }
            else if (method.ReturnType.ToString() == "Task" ||
                     method.ReturnType.ToString().StartsWith("Task<"))
            {
                newReturnType = method.ReturnType;
            }
            else
            {
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

        return root;
    }
}
