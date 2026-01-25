using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace AIRoutine.CodeStyle.Analyzers;

/// <summary>
/// Analyzer that detects classic extension methods using the 'this' parameter modifier
/// and suggests using the new C# 14 extension block syntax instead.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ClassicExtensionMethodAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "ACS0018";

    private static readonly LocalizableString Title =
        "Use extension block syntax instead of 'this' parameter";

    private static readonly LocalizableString MessageFormat =
        "Extension method '{0}' uses classic 'this' parameter syntax. Consider using C# 14 extension block syntax for better clarity and support for extension properties/operators.";

    private static readonly LocalizableString Description =
        "C# 14 introduces a new 'extension' block syntax that provides clearer semantics and supports extension properties and operators in addition to methods. Consider migrating classic extension methods to the new syntax.";

    private const string Category = "Style";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        Title,
        MessageFormat,
        Category,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: Description,
        helpLinkUri: "https://learn.microsoft.com/dotnet/csharp/programming-guide/classes-and-structs/extension-methods");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterSyntaxNodeAction(AnalyzeMethod, SyntaxKind.MethodDeclaration);
    }

    private static void AnalyzeMethod(SyntaxNodeAnalysisContext context)
    {
        var method = (MethodDeclarationSyntax)context.Node;

        // Extension methods must be static
        if (!method.Modifiers.Any(SyntaxKind.StaticKeyword))
            return;

        // Check if the method has parameters
        if (method.ParameterList.Parameters.Count == 0)
            return;

        var firstParameter = method.ParameterList.Parameters[0];

        // Check if the first parameter has the 'this' modifier
        if (!firstParameter.Modifiers.Any(SyntaxKind.ThisKeyword))
            return;

        // Verify this is in a static class (required for extension methods)
        var containingClass = method.FirstAncestorOrSelf<ClassDeclarationSyntax>();
        if (containingClass == null || !containingClass.Modifiers.Any(SyntaxKind.StaticKeyword))
            return;

        // This is a classic extension method - report diagnostic
        var diagnostic = Diagnostic.Create(
            Rule,
            firstParameter.Modifiers.First(m => m.IsKind(SyntaxKind.ThisKeyword)).GetLocation(),
            method.Identifier.Text);

        context.ReportDiagnostic(diagnostic);
    }
}
