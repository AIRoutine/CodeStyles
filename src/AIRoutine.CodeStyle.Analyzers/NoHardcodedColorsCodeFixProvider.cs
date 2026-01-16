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
/// Provides code fixes for the NoHardcodedColorsAnalyzer (ACS0003).
/// Offers to replace hardcoded colors with resource lookup.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(NoHardcodedColorsCodeFixProvider))]
[Shared]
public sealed class NoHardcodedColorsCodeFixProvider : CodeFixProvider
{
    public override ImmutableArray<string> FixableDiagnosticIds =>
        ImmutableArray.Create(NoHardcodedColorsAnalyzer.DiagnosticId);

    public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root == null) return;

        var diagnostic = context.Diagnostics.First();
        var diagnosticSpan = diagnostic.Location.SourceSpan;

        var node = root.FindNode(diagnosticSpan);

        // Try to find the color expression
        var memberAccess = node.AncestorsAndSelf().OfType<MemberAccessExpressionSyntax>().FirstOrDefault();
        var invocation = node.AncestorsAndSelf().OfType<InvocationExpressionSyntax>().FirstOrDefault();

        string? colorName = null;
        ExpressionSyntax? nodeToReplace = null;

        if (memberAccess != null)
        {
            // Colors.Red case
            if (memberAccess.Expression is IdentifierNameSyntax identifier &&
                identifier.Identifier.Text == "Colors")
            {
                colorName = memberAccess.Name.Identifier.Text;
                nodeToReplace = memberAccess;
            }
            // Color.FromArgb case
            else if (memberAccess.Name.Identifier.Text == "FromArgb" ||
                     memberAccess.Name.Identifier.Text == "FromRgb")
            {
                colorName = "CustomColor";
                nodeToReplace = invocation ?? (ExpressionSyntax)memberAccess;
            }
        }

        if (colorName == null || nodeToReplace == null)
        {
            // Try string literal (hex color)
            var literal = node.AncestorsAndSelf().OfType<LiteralExpressionSyntax>().FirstOrDefault();
            if (literal != null && literal.Token.ValueText.StartsWith("#"))
            {
                colorName = "CustomColor";
                nodeToReplace = (ExpressionSyntax?)invocation ?? literal;
            }
        }

        if (colorName == null || nodeToReplace == null) return;

        var resourceKey = colorName + "Brush";

        context.RegisterCodeFix(
            CodeAction.Create(
                title: $"Use resource '{resourceKey}'",
                createChangedDocument: c => ReplaceWithResourceLookupAsync(
                    context.Document, nodeToReplace, resourceKey, c),
                equivalenceKey: "UseResourceLookup"),
            diagnostic);
    }

    private static async Task<Document> ReplaceWithResourceLookupAsync(
        Document document,
        ExpressionSyntax nodeToReplace,
        string resourceKey,
        CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root == null) return document;

        // Create: (Color)Application.Current.Resources["ResourceKey"]
        // or for brush context: (SolidColorBrush)Application.Current.Resources["ResourceKey"]

        // Determine if we need Color or SolidColorBrush cast
        var parent = nodeToReplace.Parent;
        var needsBrushCast = parent is ObjectCreationExpressionSyntax creation &&
            creation.Type.ToString().Contains("Brush");

        var castType = needsBrushCast ? "SolidColorBrush" : "Color";

        // Build: Application.Current.Resources["key"]
        var resourceAccess = SyntaxFactory.ElementAccessExpression(
            SyntaxFactory.MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                SyntaxFactory.MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    SyntaxFactory.IdentifierName("Application"),
                    SyntaxFactory.IdentifierName("Current")),
                SyntaxFactory.IdentifierName("Resources")))
            .WithArgumentList(
                SyntaxFactory.BracketedArgumentList(
                    SyntaxFactory.SingletonSeparatedList(
                        SyntaxFactory.Argument(
                            SyntaxFactory.LiteralExpression(
                                SyntaxKind.StringLiteralExpression,
                                SyntaxFactory.Literal(resourceKey))))));

        // Add cast
        var castExpression = SyntaxFactory.CastExpression(
            SyntaxFactory.IdentifierName(castType),
            resourceAccess);

        ExpressionSyntax replacement = castExpression;

        // If the node to replace is inside a SolidColorBrush constructor, replace the whole constructor
        if (parent is ArgumentSyntax arg &&
            arg.Parent?.Parent is ObjectCreationExpressionSyntax brushCreation &&
            brushCreation.Type.ToString().Contains("Brush"))
        {
            // Replace the whole brush creation with resource lookup
            var brushResourceAccess = SyntaxFactory.CastExpression(
                SyntaxFactory.IdentifierName("SolidColorBrush"),
                SyntaxFactory.ElementAccessExpression(
                    SyntaxFactory.MemberAccessExpression(
                        SyntaxKind.SimpleMemberAccessExpression,
                        SyntaxFactory.MemberAccessExpression(
                            SyntaxKind.SimpleMemberAccessExpression,
                            SyntaxFactory.IdentifierName("Application"),
                            SyntaxFactory.IdentifierName("Current")),
                        SyntaxFactory.IdentifierName("Resources")))
                    .WithArgumentList(
                        SyntaxFactory.BracketedArgumentList(
                            SyntaxFactory.SingletonSeparatedList(
                                SyntaxFactory.Argument(
                                    SyntaxFactory.LiteralExpression(
                                        SyntaxKind.StringLiteralExpression,
                                        SyntaxFactory.Literal(resourceKey)))))));

            var newRoot = root.ReplaceNode(brushCreation, brushResourceAccess);
            return document.WithSyntaxRoot(newRoot);
        }

        var finalRoot = root.ReplaceNode(nodeToReplace, replacement);
        return document.WithSyntaxRoot(finalRoot);
    }
}
