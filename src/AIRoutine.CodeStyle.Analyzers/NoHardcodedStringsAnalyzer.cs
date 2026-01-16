using System.Collections.Immutable;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace AIRoutine.CodeStyle.Analyzers;

/// <summary>
/// Analyzer that detects hardcoded strings in ViewModels, Services, and Handlers.
/// Enforces localization and maintainability by requiring strings to come from resources or constants.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class NoHardcodedStringsAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "ACS0001";

    private static readonly LocalizableString Title =
        "Hardcoded string detected";

    private static readonly LocalizableString MessageFormat =
        "Hardcoded string '{0}' should be replaced with a resource, constant, or localized string";

    private static readonly LocalizableString Description =
        "Strings in ViewModels, Services, and Handlers should not be hardcoded. Use resources, constants, or localization instead.";

    private const string Category = "Maintainability";

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

    // File name patterns to analyze
    private static readonly string[] TargetFileSuffixes =
    {
        "ViewModel.cs",
        "Service.cs",
        "Handler.cs"
    };

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterSyntaxNodeAction(AnalyzeStringLiteral, SyntaxKind.StringLiteralExpression);
    }

    private static void AnalyzeStringLiteral(SyntaxNodeAnalysisContext context)
    {
        // Check if file matches target patterns
        var filePath = context.Node.SyntaxTree.FilePath;
        if (string.IsNullOrEmpty(filePath))
            return;

        var fileName = Path.GetFileName(filePath);
        if (!TargetFileSuffixes.Any(suffix => fileName.EndsWith(suffix, System.StringComparison.OrdinalIgnoreCase)))
            return;

        var literal = (LiteralExpressionSyntax)context.Node;
        var stringValue = literal.Token.ValueText;

        // Check if this string is allowed
        if (IsAllowedString(stringValue, literal, context))
            return;

        // Truncate long strings for the diagnostic message
        var displayValue = stringValue.Length > 30
            ? stringValue.Substring(0, 27) + "..."
            : stringValue;

        var diagnostic = Diagnostic.Create(Rule, literal.GetLocation(), displayValue);
        context.ReportDiagnostic(diagnostic);
    }

    private static bool IsAllowedString(string value, LiteralExpressionSyntax literal, SyntaxNodeAnalysisContext context)
    {
        // 1. Empty strings are allowed
        if (string.IsNullOrEmpty(value))
            return true;

        // 2. Whitespace-only strings are allowed
        if (string.IsNullOrWhiteSpace(value))
            return true;

        // 3. Single characters are allowed (often used as separators)
        if (value.Length == 1)
            return true;

        // 4. Check if inside const declaration
        if (IsInConstDeclaration(literal))
            return true;

        // 5. Check if inside attribute
        if (IsInAttribute(literal))
            return true;

        // 6. Check if inside nameof expression
        if (IsInNameofExpression(literal))
            return true;

        // 7. Check if it's a logging format string with placeholders
        if (IsLoggingFormatString(value, literal))
            return true;

        // 8. Check if it's a technical string (URL, path, etc.)
        if (IsTechnicalString(value))
            return true;

        // 9. Check if inside resource accessor
        if (IsInResourceAccessor(literal))
            return true;

        // 10. Check if it's a dictionary/JSON key pattern (simple identifiers)
        if (IsSimpleIdentifier(value))
            return true;

        // 11. Check if inside pragma or preprocessor directive
        if (IsInPreprocessorDirective(literal))
            return true;

        // 12. Check if inside interpolated string (the literal parts)
        if (IsPartOfInterpolatedString(literal))
            return true;

        return false;
    }

    private static bool IsInConstDeclaration(SyntaxNode node)
    {
        var fieldDeclaration = node.FirstAncestorOrSelf<FieldDeclarationSyntax>();
        if (fieldDeclaration != null && fieldDeclaration.Modifiers.Any(SyntaxKind.ConstKeyword))
            return true;

        var localDeclaration = node.FirstAncestorOrSelf<LocalDeclarationStatementSyntax>();
        if (localDeclaration != null && localDeclaration.Modifiers.Any(SyntaxKind.ConstKeyword))
            return true;

        return false;
    }

    private static bool IsInAttribute(SyntaxNode node)
    {
        return node.FirstAncestorOrSelf<AttributeSyntax>() != null ||
               node.FirstAncestorOrSelf<AttributeArgumentSyntax>() != null;
    }

    private static bool IsInNameofExpression(SyntaxNode node)
    {
        var invocation = node.FirstAncestorOrSelf<InvocationExpressionSyntax>();
        if (invocation?.Expression is IdentifierNameSyntax identifier)
        {
            return identifier.Identifier.Text == "nameof";
        }
        return false;
    }

    private static bool IsLoggingFormatString(string value, SyntaxNode node)
    {
        // Check if string contains structured logging placeholders like {User}, {Id}, etc.
        if (value.Contains("{") && value.Contains("}"))
        {
            // Verify it's in a logging context
            var invocation = node.FirstAncestorOrSelf<InvocationExpressionSyntax>();
            if (invocation != null)
            {
                var methodName = GetMethodName(invocation);
                if (IsLoggingMethod(methodName))
                    return true;

                // Even without logging context, structured placeholders are likely intentional
                // Check for pattern like {SomeName} (not just {0})
                if (System.Text.RegularExpressions.Regex.IsMatch(value, @"\{[A-Za-z][A-Za-z0-9]*\}"))
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
            _ => null
        };
    }

    private static bool IsLoggingMethod(string? methodName)
    {
        if (methodName == null) return false;

        var loggingMethods = new[]
        {
            "Log", "LogTrace", "LogDebug", "LogInformation", "LogWarning", "LogError", "LogCritical",
            "Debug", "Info", "Warn", "Error", "Fatal", "Verbose", "Information", "Warning"
        };

        return loggingMethods.Contains(methodName);
    }

    private static bool IsTechnicalString(string value)
    {
        // URLs
        if (value.StartsWith("http://", System.StringComparison.OrdinalIgnoreCase) ||
            value.StartsWith("https://", System.StringComparison.OrdinalIgnoreCase) ||
            value.StartsWith("ws://", System.StringComparison.OrdinalIgnoreCase) ||
            value.StartsWith("wss://", System.StringComparison.OrdinalIgnoreCase))
            return true;

        // File paths (Unix or Windows style)
        if (value.StartsWith("/") || value.StartsWith("./") || value.StartsWith("../"))
            return true;
        if (value.Length >= 2 && char.IsLetter(value[0]) && value[1] == ':')
            return true;

        // API routes
        if (value.StartsWith("api/", System.StringComparison.OrdinalIgnoreCase))
            return true;

        // MIME types
        if (value.StartsWith("application/", System.StringComparison.OrdinalIgnoreCase) ||
            value.StartsWith("text/", System.StringComparison.OrdinalIgnoreCase) ||
            value.StartsWith("image/", System.StringComparison.OrdinalIgnoreCase))
            return true;

        // Connection strings pattern
        if (value.Contains("=") && (
            value.IndexOf("Server=", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
            value.IndexOf("Data Source=", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
            value.IndexOf("Host=", System.StringComparison.OrdinalIgnoreCase) >= 0))
            return true;

        // Date/time format strings
        if (IsDateTimeFormatString(value))
            return true;

        // Regex patterns (common indicators)
        if (value.StartsWith("^") || value.EndsWith("$") ||
            value.Contains("\\d") || value.Contains("\\w") || value.Contains("\\s"))
            return true;

        return false;
    }

    private static bool IsDateTimeFormatString(string value)
    {
        // Common date/time format patterns
        var formatPatterns = new[] { "yyyy", "MM", "dd", "HH", "mm", "ss", "fff" };
        var matchCount = formatPatterns.Count(p => value.Contains(p));
        return matchCount >= 2;
    }

    private static bool IsInResourceAccessor(SyntaxNode node)
    {
        var invocation = node.FirstAncestorOrSelf<InvocationExpressionSyntax>();
        if (invocation == null) return false;

        var methodName = GetMethodName(invocation);
        if (methodName == null) return false;

        // Common resource accessor methods
        var resourceMethods = new[]
        {
            "GetString", "GetLocalizedString", "Localize", "Translate", "T",
            "GetResource", "LoadResource"
        };

        return resourceMethods.Contains(methodName);
    }

    private static bool IsSimpleIdentifier(string value)
    {
        // Allow simple identifiers that look like keys/property names
        // Must be PascalCase or camelCase or UPPER_CASE, no spaces
        if (value.Contains(" "))
            return false;

        // Check if it's a valid identifier pattern
        if (System.Text.RegularExpressions.Regex.IsMatch(value, @"^[A-Za-z_][A-Za-z0-9_]*$"))
            return true;

        // Allow dot-separated identifiers (like "Settings.Theme")
        if (System.Text.RegularExpressions.Regex.IsMatch(value, @"^[A-Za-z_][A-Za-z0-9_.]*$"))
            return true;

        return false;
    }

    private static bool IsInPreprocessorDirective(SyntaxNode node)
    {
        return node.FirstAncestorOrSelf<DirectiveTriviaSyntax>() != null;
    }

    private static bool IsPartOfInterpolatedString(SyntaxNode node)
    {
        return node.Parent is InterpolatedStringTextSyntax ||
               node.FirstAncestorOrSelf<InterpolatedStringExpressionSyntax>() != null;
    }
}
