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
/// Provides code fixes for the NoStaticCallsAnalyzer (ACS0002).
/// Offers to create an interface and inject it via constructor.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(NoStaticCallsCodeFixProvider))]
[Shared]
public sealed class NoStaticCallsCodeFixProvider : CodeFixProvider
{
    public override ImmutableArray<string> FixableDiagnosticIds =>
        ImmutableArray.Create(NoStaticCallsAnalyzer.DiagnosticId);

    public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root == null) return;

        var diagnostic = context.Diagnostics.First();
        var diagnosticSpan = diagnostic.Location.SourceSpan;

        var invocation = root.FindToken(diagnosticSpan.Start).Parent?.AncestorsAndSelf()
            .OfType<InvocationExpressionSyntax>().FirstOrDefault();

        if (invocation?.Expression is not MemberAccessExpressionSyntax memberAccess)
            return;

        var semanticModel = await context.Document.GetSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);
        if (semanticModel == null) return;

        var symbolInfo = semanticModel.GetSymbolInfo(invocation);
        if (symbolInfo.Symbol is not IMethodSymbol methodSymbol)
            return;

        var typeName = methodSymbol.ContainingType?.Name ?? "Unknown";
        var methodName = methodSymbol.Name;
        var fieldName = "_" + char.ToLowerInvariant(typeName[0]) + typeName.Substring(1);
        var interfaceName = "I" + typeName;

        context.RegisterCodeFix(
            CodeAction.Create(
                title: $"Inject '{interfaceName}' via constructor",
                createChangedDocument: c => InjectDependencyAsync(
                    context.Document, invocation, memberAccess, typeName, methodName, fieldName, interfaceName, c),
                equivalenceKey: "InjectDependency"),
            diagnostic);
    }

    private static async Task<Document> InjectDependencyAsync(
        Document document,
        InvocationExpressionSyntax invocation,
        MemberAccessExpressionSyntax memberAccess,
        string typeName,
        string methodName,
        string fieldName,
        string interfaceName,
        CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root == null) return document;

        // Find the containing type
        var containingType = invocation.FirstAncestorOrSelf<ClassDeclarationSyntax>();
        if (containingType == null) return document;

        // Check if field already exists
        var existingField = containingType.Members
            .OfType<FieldDeclarationSyntax>()
            .Any(f => f.Declaration.Variables.Any(v => v.Identifier.Text == fieldName));

        // Replace the static call with instance call
        var newMemberAccess = SyntaxFactory.MemberAccessExpression(
            SyntaxKind.SimpleMemberAccessExpression,
            SyntaxFactory.IdentifierName(fieldName),
            SyntaxFactory.IdentifierName(methodName));

        var newInvocation = invocation.WithExpression(newMemberAccess);
        var newRoot = root.ReplaceNode(invocation, newInvocation);

        if (!existingField)
        {
            // Find the class in the new tree
            var newContainingType = newRoot.DescendantNodes()
                .OfType<ClassDeclarationSyntax>()
                .FirstOrDefault(c => c.Identifier.Text == containingType.Identifier.Text);

            if (newContainingType != null)
            {
                // Add field declaration
                var fieldDeclaration = SyntaxFactory.FieldDeclaration(
                    SyntaxFactory.VariableDeclaration(
                        SyntaxFactory.IdentifierName(interfaceName))
                    .WithVariables(
                        SyntaxFactory.SingletonSeparatedList(
                            SyntaxFactory.VariableDeclarator(SyntaxFactory.Identifier(fieldName)))))
                    .WithModifiers(
                        SyntaxFactory.TokenList(
                            SyntaxFactory.Token(SyntaxKind.PrivateKeyword),
                            SyntaxFactory.Token(SyntaxKind.ReadOnlyKeyword)))
                    .WithTrailingTrivia(SyntaxFactory.CarriageReturnLineFeed);

                // Find or create constructor
                var constructor = newContainingType.Members
                    .OfType<ConstructorDeclarationSyntax>()
                    .FirstOrDefault();

                TypeDeclarationSyntax updatedType;

                if (constructor != null)
                {
                    // Add parameter to existing constructor
                    var newParameter = SyntaxFactory.Parameter(
                        SyntaxFactory.Identifier(char.ToLowerInvariant(typeName[0]) + typeName.Substring(1)))
                        .WithType(SyntaxFactory.IdentifierName(interfaceName));

                    var newParameterList = constructor.ParameterList.AddParameters(newParameter);

                    // Add assignment to constructor body
                    var assignment = SyntaxFactory.ExpressionStatement(
                        SyntaxFactory.AssignmentExpression(
                            SyntaxKind.SimpleAssignmentExpression,
                            SyntaxFactory.IdentifierName(fieldName),
                            SyntaxFactory.IdentifierName(newParameter.Identifier.Text)));

                    var newBody = constructor.Body?.AddStatements(assignment) ??
                        SyntaxFactory.Block(assignment);

                    var newConstructor = constructor
                        .WithParameterList(newParameterList)
                        .WithBody(newBody);

                    updatedType = newContainingType
                        .ReplaceNode(constructor, newConstructor)
                        .WithMembers(newContainingType.Members.Insert(0, fieldDeclaration));
                }
                else
                {
                    // Create new constructor
                    var parameterName = char.ToLowerInvariant(typeName[0]) + typeName.Substring(1);
                    var newConstructor = SyntaxFactory.ConstructorDeclaration(newContainingType.Identifier)
                        .WithModifiers(SyntaxFactory.TokenList(SyntaxFactory.Token(SyntaxKind.PublicKeyword)))
                        .WithParameterList(
                            SyntaxFactory.ParameterList(
                                SyntaxFactory.SingletonSeparatedList(
                                    SyntaxFactory.Parameter(SyntaxFactory.Identifier(parameterName))
                                        .WithType(SyntaxFactory.IdentifierName(interfaceName)))))
                        .WithBody(
                            SyntaxFactory.Block(
                                SyntaxFactory.ExpressionStatement(
                                    SyntaxFactory.AssignmentExpression(
                                        SyntaxKind.SimpleAssignmentExpression,
                                        SyntaxFactory.IdentifierName(fieldName),
                                        SyntaxFactory.IdentifierName(parameterName)))))
                        .WithTrailingTrivia(SyntaxFactory.CarriageReturnLineFeed);

                    updatedType = newContainingType.WithMembers(
                        newContainingType.Members
                            .Insert(0, fieldDeclaration)
                            .Insert(1, newConstructor));
                }

                newRoot = newRoot.ReplaceNode(newContainingType, updatedType);
            }
        }

        return document.WithSyntaxRoot(newRoot);
    }
}
