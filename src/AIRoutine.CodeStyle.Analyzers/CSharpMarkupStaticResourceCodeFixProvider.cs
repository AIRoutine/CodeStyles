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
/// Provides code fixes for CSharpMarkupStaticResourceAnalyzer (ACS0017).
/// Replaces hardcoded strings with constant references from StyleKeys/BrushKeys/etc.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(CSharpMarkupStaticResourceCodeFixProvider))]
[Shared]
public sealed class CSharpMarkupStaticResourceCodeFixProvider : CodeFixProvider
{
    public override ImmutableArray<string> FixableDiagnosticIds =>
        ImmutableArray.Create(CSharpMarkupStaticResourceAnalyzer.DiagnosticId);

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

        // Determine the appropriate Keys class and constant name
        var (keysClass, constantName) = DetermineKeysClassAndName(stringValue);

        context.RegisterCodeFix(
            CodeAction.Create(
                title: $"Use {keysClass}.{constantName}",
                createChangedDocument: c => ReplaceWithConstantAsync(context.Document, literal, keysClass, constantName, c),
                equivalenceKey: "UseStyleConstant"),
            diagnostic);
    }

    private static (string keysClass, string constantName) DetermineKeysClassAndName(string resourceKey)
    {
        // Determine the Keys class based on the resource key suffix
        string keysClass;
        string constantName;

        if (resourceKey.EndsWith("Style"))
        {
            keysClass = "StyleKeys";
            // Remove "Style" suffix for constant name: "BodyTextBlockStyle" -> "BodyTextBlock"
            constantName = resourceKey.Substring(0, resourceKey.Length - 5);
        }
        else if (resourceKey.EndsWith("Brush"))
        {
            keysClass = "BrushKeys";
            // Remove "Brush" suffix: "SurfaceBrush" -> "Surface"
            constantName = resourceKey.Substring(0, resourceKey.Length - 5);
        }
        else if (resourceKey.EndsWith("Color"))
        {
            keysClass = "ColorKeys";
            // Remove "Color" suffix: "PrimaryColor" -> "Primary"
            constantName = resourceKey.Substring(0, resourceKey.Length - 5);
        }
        else if (resourceKey.EndsWith("Theme"))
        {
            keysClass = "ThemeKeys";
            constantName = resourceKey.Substring(0, resourceKey.Length - 5);
        }
        else if (resourceKey.Contains("Brush") || resourceKey.Contains("Background") || resourceKey.Contains("Foreground"))
        {
            keysClass = "BrushKeys";
            constantName = resourceKey;
        }
        else if (resourceKey.Contains("Color"))
        {
            keysClass = "ColorKeys";
            constantName = resourceKey;
        }
        else
        {
            // Default to StyleKeys for everything else
            keysClass = "StyleKeys";
            constantName = resourceKey;
        }

        // Ensure constant name is valid C# identifier
        constantName = SanitizeIdentifier(constantName);

        return (keysClass, constantName);
    }

    private static string SanitizeIdentifier(string name)
    {
        if (string.IsNullOrEmpty(name))
            return "Key";

        // Remove invalid characters
        var chars = name.Where(c => char.IsLetterOrDigit(c) || c == '_').ToArray();
        var result = new string(chars);

        // Ensure it starts with a letter
        if (result.Length == 0 || !char.IsLetter(result[0]))
            result = "Key" + result;

        return result;
    }

    private static async Task<Document> ReplaceWithConstantAsync(
        Document document,
        LiteralExpressionSyntax literal,
        string keysClass,
        string constantName,
        CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root == null) return document;

        // Create the constant reference: StyleKeys.BodyTextBlock
        var constantReference = SyntaxFactory.MemberAccessExpression(
            SyntaxKind.SimpleMemberAccessExpression,
            SyntaxFactory.IdentifierName(keysClass),
            SyntaxFactory.IdentifierName(constantName));

        // Replace the string literal with the constant reference
        var newRoot = root.ReplaceNode(literal, constantReference);

        return document.WithSyntaxRoot(newRoot);
    }
}
