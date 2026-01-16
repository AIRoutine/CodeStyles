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
/// Provides code fixes for the NoHardcodedStringsAnalyzer (ACS0001).
/// Offers to extract hardcoded strings to constants.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(NoHardcodedStringsCodeFixProvider))]
[Shared]
public sealed class NoHardcodedStringsCodeFixProvider : CodeFixProvider
{
    public override ImmutableArray<string> FixableDiagnosticIds =>
        ImmutableArray.Create(NoHardcodedStringsAnalyzer.DiagnosticId);

    public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root == null) return;

        var diagnostic = context.Diagnostics.First();
        var diagnosticSpan = diagnostic.Location.SourceSpan;

        var literal = root.FindToken(diagnosticSpan.Start).Parent?.AncestorsAndSelf()
            .OfType<LiteralExpressionSyntax>().FirstOrDefault();

        if (literal == null) return;

        var stringValue = literal.Token.ValueText;

        // Generate a constant name from the string value
        var constantName = GenerateConstantName(stringValue);

        context.RegisterCodeFix(
            CodeAction.Create(
                title: $"Extract to constant '{constantName}'",
                createChangedDocument: c => ExtractToConstantAsync(context.Document, literal, constantName, c),
                equivalenceKey: "ExtractToConstant"),
            diagnostic);
    }

    private static string GenerateConstantName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "EmptyString";

        // Take first few words and convert to PascalCase
        var words = value
            .Split(' ', '-', '_', '.', ',', '!', '?')
            .Where(w => !string.IsNullOrWhiteSpace(w))
            .Take(4)
            .Select(w => char.ToUpperInvariant(w[0]) + (w.Length > 1 ? w.Substring(1).ToLowerInvariant() : ""));

        var name = string.Join("", words);

        // Ensure it starts with a letter
        if (name.Length == 0 || !char.IsLetter(name[0]))
            name = "Text" + name;

        // Limit length
        if (name.Length > 30)
            name = name.Substring(0, 30);

        return name;
    }

    private static async Task<Document> ExtractToConstantAsync(
        Document document,
        LiteralExpressionSyntax literal,
        string constantName,
        CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root == null) return document;

        // Find the containing type
        var containingType = literal.FirstAncestorOrSelf<TypeDeclarationSyntax>();
        if (containingType == null) return document;

        // Create the constant declaration
        var stringValue = literal.Token.ValueText;
        var constantDeclaration = SyntaxFactory.FieldDeclaration(
            SyntaxFactory.VariableDeclaration(
                SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.StringKeyword)))
            .WithVariables(
                SyntaxFactory.SingletonSeparatedList(
                    SyntaxFactory.VariableDeclarator(SyntaxFactory.Identifier(constantName))
                    .WithInitializer(
                        SyntaxFactory.EqualsValueClause(
                            SyntaxFactory.LiteralExpression(
                                SyntaxKind.StringLiteralExpression,
                                SyntaxFactory.Literal(stringValue)))))))
            .WithModifiers(
                SyntaxFactory.TokenList(
                    SyntaxFactory.Token(SyntaxKind.PrivateKeyword),
                    SyntaxFactory.Token(SyntaxKind.ConstKeyword)))
            .WithTrailingTrivia(SyntaxFactory.CarriageReturnLineFeed);

        // Replace the literal with a reference to the constant
        var constantReference = SyntaxFactory.IdentifierName(constantName);
        var newRoot = root.ReplaceNode(literal, constantReference);

        // Find the type declaration in the new root and add the constant
        var newContainingType = newRoot.DescendantNodes()
            .OfType<TypeDeclarationSyntax>()
            .FirstOrDefault(t => t.Identifier.Text == containingType.Identifier.Text);

        if (newContainingType != null)
        {
            // Insert the constant at the beginning of the type
            var newTypeWithConstant = InsertConstant(newContainingType, constantDeclaration);
            newRoot = newRoot.ReplaceNode(newContainingType, newTypeWithConstant);
        }

        return document.WithSyntaxRoot(newRoot);
    }

    private static TypeDeclarationSyntax InsertConstant(
        TypeDeclarationSyntax typeDeclaration,
        FieldDeclarationSyntax constantDeclaration)
    {
        // Find existing constants to insert after them, or insert at the beginning
        var existingConstants = typeDeclaration.Members
            .OfType<FieldDeclarationSyntax>()
            .Where(f => f.Modifiers.Any(SyntaxKind.ConstKeyword))
            .ToList();

        if (existingConstants.Any())
        {
            // Insert after the last constant
            var lastConstant = existingConstants.Last();
            var index = typeDeclaration.Members.IndexOf(lastConstant);
            return typeDeclaration.WithMembers(
                typeDeclaration.Members.Insert(index + 1, constantDeclaration));
        }
        else
        {
            // Insert at the beginning
            return typeDeclaration.WithMembers(
                typeDeclaration.Members.Insert(0, constantDeclaration));
        }
    }
}
