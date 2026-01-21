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
/// Offers to replace hardcoded colors with resource lookup and optionally generate resource entries.
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

        // Extract color info from the diagnostic
        var (colorName, colorValue, nodeToReplace) = ExtractColorInfo(node, context);

        if (colorName == null || nodeToReplace == null)
            return;

        var resourceKey = colorName + "Brush";

        // Code fix 1: Replace with resource lookup
        context.RegisterCodeFix(
            CodeAction.Create(
                title: $"Use resource '{resourceKey}'",
                createChangedDocument: c => ReplaceWithResourceLookupAsync(
                    context.Document, nodeToReplace, resourceKey, c),
                equivalenceKey: "UseResourceLookup"),
            diagnostic);

        // Code fix 2: Generate ThemeResource usage (for Uno/WinUI)
        context.RegisterCodeFix(
            CodeAction.Create(
                title: $"Use ThemeResource '{resourceKey}'",
                createChangedDocument: c => ReplaceWithThemeResourceAsync(
                    context.Document, nodeToReplace, resourceKey, c),
                equivalenceKey: "UseThemeResource"),
            diagnostic);

        // Code fix 3: Show ResourceDictionary snippet (information only)
        if (colorValue != null)
        {
            context.RegisterCodeFix(
                CodeAction.Create(
                    title: $"Copy ResourceDictionary entry for '{resourceKey}'",
                    createChangedDocument: c => AddResourceDictionaryCommentAsync(
                        context.Document, nodeToReplace, resourceKey, colorValue, colorName, c),
                    equivalenceKey: "AddResourceComment"),
                diagnostic);
        }
    }

    private static (string? colorName, string? colorValue, ExpressionSyntax? nodeToReplace) ExtractColorInfo(
        SyntaxNode node, CodeFixContext context)
    {
        string? colorName = null;
        string? colorValue = null;
        ExpressionSyntax? nodeToReplace = null;

        var memberAccess = node.AncestorsAndSelf().OfType<MemberAccessExpressionSyntax>().FirstOrDefault();
        var invocation = node.AncestorsAndSelf().OfType<InvocationExpressionSyntax>().FirstOrDefault();

        if (memberAccess != null)
        {
            // Colors.Red case
            if (memberAccess.Expression is IdentifierNameSyntax identifier &&
                identifier.Identifier.Text == "Colors")
            {
                colorName = memberAccess.Name.Identifier.Text;
                colorValue = GetColorHexValue(colorName);
                nodeToReplace = memberAccess;
            }
            // Color.FromArgb case
            else if (memberAccess.Name.Identifier.Text == "FromArgb" ||
                     memberAccess.Name.Identifier.Text == "FromRgb")
            {
                colorName = "Custom";
                if (invocation != null)
                {
                    colorValue = ExtractColorFromArguments(invocation);
                    nodeToReplace = invocation;
                }
                else
                {
                    nodeToReplace = memberAccess;
                }
            }
        }

        if (colorName == null)
        {
            // Try string literal (hex color)
            var literal = node.AncestorsAndSelf().OfType<LiteralExpressionSyntax>().FirstOrDefault();
            if (literal != null && literal.Token.ValueText.StartsWith("#"))
            {
                colorValue = literal.Token.ValueText;
                colorName = GenerateColorNameFromHex(colorValue);
                nodeToReplace = (ExpressionSyntax?)invocation ?? literal;
            }
        }

        return (colorName, colorValue, nodeToReplace);
    }

    private static string? GetColorHexValue(string colorName)
    {
        // Common color mappings
        return colorName switch
        {
            "Red" => "#FFFF0000",
            "Green" => "#FF00FF00",
            "Blue" => "#FF0000FF",
            "White" => "#FFFFFFFF",
            "Black" => "#FF000000",
            "Yellow" => "#FFFFFF00",
            "Orange" => "#FFFFA500",
            "Purple" => "#FF800080",
            "Gray" => "#FF808080",
            "Pink" => "#FFFFC0CB",
            "Cyan" => "#FF00FFFF",
            "Magenta" => "#FFFF00FF",
            _ => null
        };
    }

    private static string? ExtractColorFromArguments(InvocationExpressionSyntax invocation)
    {
        var args = invocation.ArgumentList.Arguments;
        if (args.Count >= 3)
        {
            // Try to extract literal values
            var values = args.Select(a =>
            {
                if (a.Expression is LiteralExpressionSyntax literal &&
                    literal.Token.Value is int intVal)
                {
                    return intVal;
                }
                if (a.Expression is LiteralExpressionSyntax byteLiteral &&
                    byteLiteral.Token.Value is byte byteVal)
                {
                    return (int)byteVal;
                }
                return -1;
            }).ToArray();

            if (values.All(v => v >= 0 && v <= 255))
            {
                if (args.Count == 4) // ARGB
                {
                    return $"#{values[0]:X2}{values[1]:X2}{values[2]:X2}{values[3]:X2}";
                }
                else if (args.Count == 3) // RGB
                {
                    return $"#FF{values[0]:X2}{values[1]:X2}{values[2]:X2}";
                }
            }
        }
        return null;
    }

    private static string GenerateColorNameFromHex(string hexColor)
    {
        // Generate a readable name from hex color
        var normalized = hexColor.TrimStart('#').ToUpperInvariant();
        return normalized.Length switch
        {
            6 => $"Color{normalized}",
            8 => $"Color{normalized.Substring(2)}", // Skip alpha
            _ => "CustomColor"
        };
    }

    private static async Task<Document> ReplaceWithResourceLookupAsync(
        Document document,
        ExpressionSyntax nodeToReplace,
        string resourceKey,
        CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root == null) return document;

        // Determine context - do we need Color or Brush?
        var needsBrush = IsInBrushContext(nodeToReplace);
        var castType = needsBrush ? "SolidColorBrush" : "Color";

        // Build: (Color)Application.Current.Resources["key"]
        var resourceAccess = BuildResourceAccess(resourceKey);
        var castExpression = SyntaxFactory.CastExpression(
            SyntaxFactory.IdentifierName(castType),
            resourceAccess);

        // Handle brush constructor case
        var replacement = HandleBrushConstructorReplacement(nodeToReplace, castExpression, resourceKey);

        var newRoot = root.ReplaceNode(nodeToReplace, replacement);
        return document.WithSyntaxRoot(newRoot);
    }

    private static async Task<Document> ReplaceWithThemeResourceAsync(
        Document document,
        ExpressionSyntax nodeToReplace,
        string resourceKey,
        CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root == null) return document;

        // Build: (SolidColorBrush)this.Resources["key"]
        // or for static context: (SolidColorBrush)Application.Current.Resources["key"]
        var needsBrush = IsInBrushContext(nodeToReplace);
        var castType = needsBrush ? "SolidColorBrush" : "Color";

        // Try to use this.Resources first if in a FrameworkElement context
        var containingClass = nodeToReplace.FirstAncestorOrSelf<ClassDeclarationSyntax>();
        var useThis = containingClass != null &&
            (containingClass.Identifier.Text.EndsWith("Page") ||
             containingClass.Identifier.Text.EndsWith("View") ||
             containingClass.Identifier.Text.EndsWith("Control") ||
             containingClass.Identifier.Text.EndsWith("Window"));

        ExpressionSyntax resourceAccess;
        if (useThis)
        {
            // this.Resources["key"]
            resourceAccess = SyntaxFactory.ElementAccessExpression(
                SyntaxFactory.MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    SyntaxFactory.ThisExpression(),
                    SyntaxFactory.IdentifierName("Resources")))
                .WithArgumentList(
                    SyntaxFactory.BracketedArgumentList(
                        SyntaxFactory.SingletonSeparatedList(
                            SyntaxFactory.Argument(
                                SyntaxFactory.LiteralExpression(
                                    SyntaxKind.StringLiteralExpression,
                                    SyntaxFactory.Literal(resourceKey))))));
        }
        else
        {
            resourceAccess = BuildResourceAccess(resourceKey);
        }

        var castExpression = SyntaxFactory.CastExpression(
            SyntaxFactory.IdentifierName(castType),
            resourceAccess);

        var replacement = HandleBrushConstructorReplacement(nodeToReplace, castExpression, resourceKey);

        var newRoot = root.ReplaceNode(nodeToReplace, replacement);
        return document.WithSyntaxRoot(newRoot);
    }

    private static async Task<Document> AddResourceDictionaryCommentAsync(
        Document document,
        ExpressionSyntax nodeToReplace,
        string resourceKey,
        string colorValue,
        string colorName,
        CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root == null) return document;

        // Generate the XAML snippet as a comment
        var xamlSnippet = GenerateResourceDictionaryXaml(resourceKey, colorValue, colorName);

        // Find the containing statement and add a comment before it
        var containingStatement = nodeToReplace.FirstAncestorOrSelf<StatementSyntax>();
        if (containingStatement == null) return document;

        var comment = SyntaxFactory.Comment($"/* Add to ResourceDictionary:\n{xamlSnippet}*/\n");
        var newStatement = containingStatement.WithLeadingTrivia(
            containingStatement.GetLeadingTrivia().Add(comment));

        // Also replace with resource lookup
        var needsBrush = IsInBrushContext(nodeToReplace);
        var castType = needsBrush ? "SolidColorBrush" : "Color";
        var resourceAccess = BuildResourceAccess(resourceKey);
        var castExpression = SyntaxFactory.CastExpression(
            SyntaxFactory.IdentifierName(castType),
            resourceAccess);

        var newRoot = root.ReplaceNode(containingStatement, newStatement);

        // Now replace the color expression in the new tree
        var newNodeToReplace = newRoot.FindNode(nodeToReplace.Span);
        if (newNodeToReplace is ExpressionSyntax newExpr)
        {
            var replacement = HandleBrushConstructorReplacement(newExpr, castExpression, resourceKey);
            newRoot = newRoot.ReplaceNode(newExpr, replacement);
        }

        return document.WithSyntaxRoot(newRoot);
    }

    private static string GenerateResourceDictionaryXaml(string resourceKey, string colorValue, string colorName)
    {
        var colorKey = colorName + "Color";
        var brushKey = resourceKey;

        return $@"    <!-- Color Definition -->
    <Color x:Key=""{colorKey}"">{colorValue}</Color>

    <!-- Brush using the color -->
    <SolidColorBrush x:Key=""{brushKey}"" Color=""{{StaticResource {colorKey}}}"" />
";
    }

    private static bool IsInBrushContext(ExpressionSyntax node)
    {
        var parent = node.Parent;

        // Inside SolidColorBrush constructor
        if (parent is ArgumentSyntax arg &&
            arg.Parent?.Parent is ObjectCreationExpressionSyntax creation &&
            creation.Type.ToString().Contains("Brush"))
        {
            return true;
        }

        // Assigning to a Brush property
        if (parent is AssignmentExpressionSyntax assignment)
        {
            var propName = assignment.Left.ToString();
            if (propName.EndsWith("Brush") ||
                propName.Contains("Background") ||
                propName.Contains("Foreground") ||
                propName.Contains("Fill") ||
                propName.Contains("Stroke"))
            {
                return true;
            }
        }

        return false;
    }

    private static ElementAccessExpressionSyntax BuildResourceAccess(string resourceKey)
    {
        return SyntaxFactory.ElementAccessExpression(
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
    }

    private static ExpressionSyntax HandleBrushConstructorReplacement(
        ExpressionSyntax nodeToReplace,
        CastExpressionSyntax castExpression,
        string resourceKey)
    {
        var parent = nodeToReplace.Parent;

        // If inside SolidColorBrush constructor, replace the whole constructor
        if (parent is ArgumentSyntax arg &&
            arg.Parent?.Parent is ObjectCreationExpressionSyntax brushCreation &&
            brushCreation.Type.ToString().Contains("Brush"))
        {
            // Return a brush resource lookup instead
            return SyntaxFactory.CastExpression(
                SyntaxFactory.IdentifierName("SolidColorBrush"),
                BuildResourceAccess(resourceKey))
                .WithLeadingTrivia(nodeToReplace.GetLeadingTrivia())
                .WithTrailingTrivia(nodeToReplace.GetTrailingTrivia());
        }

        return castExpression
            .WithLeadingTrivia(nodeToReplace.GetLeadingTrivia())
            .WithTrailingTrivia(nodeToReplace.GetTrailingTrivia());
    }
}
