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
/// Provides code fixes for AsyncVoidAnalyzer (ACS0009).
/// Changes async void methods to async Task.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(AsyncVoidCodeFixProvider))]
[Shared]
public sealed class AsyncVoidCodeFixProvider : CodeFixProvider
{
    public override ImmutableArray<string> FixableDiagnosticIds =>
        ImmutableArray.Create(AsyncVoidAnalyzer.DiagnosticId);

    public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root == null) return;

        var diagnostic = context.Diagnostics.First();
        var diagnosticSpan = diagnostic.Location.SourceSpan;

        // Find the method/local function declaration
        var token = root.FindToken(diagnosticSpan.Start);
        var node = token.Parent;

        // Try to find method declaration
        var method = node?.AncestorsAndSelf().OfType<MethodDeclarationSyntax>().FirstOrDefault();
        if (method != null)
        {
            context.RegisterCodeFix(
                CodeAction.Create(
                    title: "Change to async Task",
                    createChangedDocument: c => ChangeMethodToTaskAsync(context.Document, method, c),
                    equivalenceKey: "ChangeAsyncVoidToTask"),
                diagnostic);
            return;
        }

        // Try to find local function
        var localFunction = node?.AncestorsAndSelf().OfType<LocalFunctionStatementSyntax>().FirstOrDefault();
        if (localFunction != null)
        {
            context.RegisterCodeFix(
                CodeAction.Create(
                    title: "Change to async Task",
                    createChangedDocument: c => ChangeLocalFunctionToTaskAsync(context.Document, localFunction, c),
                    equivalenceKey: "ChangeAsyncVoidToTask"),
                diagnostic);
            return;
        }

        // Try to find lambda (at async keyword location)
        var lambda = token.Parent?.AncestorsAndSelf().OfType<LambdaExpressionSyntax>().FirstOrDefault();
        if (lambda != null)
        {
            // For lambdas, we can suggest wrapping in a Func<Task> or adding await
            context.RegisterCodeFix(
                CodeAction.Create(
                    title: "Add await (if caller is async)",
                    createChangedDocument: c => AddAwaitToLambdaCallerAsync(context.Document, lambda, c),
                    equivalenceKey: "AddAwaitToLambda"),
                diagnostic);
        }
    }

    private static async Task<Document> ChangeMethodToTaskAsync(
        Document document,
        MethodDeclarationSyntax method,
        CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root == null) return document;

        // Create new return type: Task
        var taskType = SyntaxFactory.IdentifierName("Task")
            .WithTrailingTrivia(SyntaxFactory.Space);

        // Replace void with Task
        var newMethod = method.WithReturnType(taskType);

        // Ensure we have the using directive
        var newRoot = root.ReplaceNode(method, newMethod);
        newRoot = EnsureUsingDirective(newRoot, "System.Threading.Tasks");

        return document.WithSyntaxRoot(newRoot);
    }

    private static async Task<Document> ChangeLocalFunctionToTaskAsync(
        Document document,
        LocalFunctionStatementSyntax localFunction,
        CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root == null) return document;

        // Create new return type: Task
        var taskType = SyntaxFactory.IdentifierName("Task")
            .WithTrailingTrivia(SyntaxFactory.Space);

        // Replace void with Task
        var newLocalFunction = localFunction.WithReturnType(taskType);

        var newRoot = root.ReplaceNode(localFunction, newLocalFunction);
        newRoot = EnsureUsingDirective(newRoot, "System.Threading.Tasks");

        return document.WithSyntaxRoot(newRoot);
    }

    private static async Task<Document> AddAwaitToLambdaCallerAsync(
        Document document,
        LambdaExpressionSyntax lambda,
        CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root == null) return document;

        // Find the containing expression statement
        var expressionStatement = lambda.FirstAncestorOrSelf<ExpressionStatementSyntax>();
        if (expressionStatement == null) return document;

        // Wrap the expression with await
        var awaitExpression = SyntaxFactory.AwaitExpression(expressionStatement.Expression)
            .WithLeadingTrivia(expressionStatement.Expression.GetLeadingTrivia());

        var newStatement = expressionStatement.WithExpression(awaitExpression);

        var newRoot = root.ReplaceNode(expressionStatement, newStatement);

        return document.WithSyntaxRoot(newRoot);
    }

    private static SyntaxNode EnsureUsingDirective(SyntaxNode root, string namespaceName)
    {
        if (root is not CompilationUnitSyntax compilationUnit)
            return root;

        // Check if using already exists
        var hasUsing = compilationUnit.Usings.Any(u =>
            u.Name?.ToString() == namespaceName);

        if (hasUsing)
            return root;

        // Add the using directive
        var usingDirective = SyntaxFactory.UsingDirective(
            SyntaxFactory.ParseName(namespaceName))
            .WithTrailingTrivia(SyntaxFactory.CarriageReturnLineFeed);

        return compilationUnit.AddUsings(usingDirective);
    }
}
