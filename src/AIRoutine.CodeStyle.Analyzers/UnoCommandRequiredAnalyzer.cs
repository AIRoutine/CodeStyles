using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace AIRoutine.CodeStyle.Analyzers;

/// <summary>
/// Analyzer that enforces the use of [UnoCommand] attribute for commands in ViewModels/Models.
/// Forbids direct ICommand properties and other command attributes like [RelayCommand].
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class UnoCommandRequiredAnalyzer : DiagnosticAnalyzer
{
    public const string ICommandDiagnosticId = "ACS0012";
    public const string ForbiddenAttributeDiagnosticId = "ACS0013";

    private static readonly LocalizableString ICommandTitle =
        "ICommand property not allowed";

    private static readonly LocalizableString ICommandMessageFormat =
        "ICommand property '{0}' is not allowed. Use [UnoCommand] attribute on a method instead.";

    private static readonly LocalizableString ICommandDescription =
        "Direct ICommand properties are forbidden in ViewModels/Models. Use [UnoCommand] attribute on command methods.";

    private static readonly LocalizableString ForbiddenAttributeTitle =
        "Command attribute not allowed";

    private static readonly LocalizableString ForbiddenAttributeMessageFormat =
        "[{0}] attribute is not allowed. Use [UnoCommand] instead.";

    private static readonly LocalizableString ForbiddenAttributeDescription =
        "Only [UnoCommand] attribute is allowed for defining commands. Other command attributes are forbidden.";

    private const string Category = "Design";

    private static readonly DiagnosticDescriptor ICommandRule = new(
        ICommandDiagnosticId,
        ICommandTitle,
        ICommandMessageFormat,
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: ICommandDescription);

    private static readonly DiagnosticDescriptor ForbiddenAttributeRule = new(
        ForbiddenAttributeDiagnosticId,
        ForbiddenAttributeTitle,
        ForbiddenAttributeMessageFormat,
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: ForbiddenAttributeDescription);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(ICommandRule, ForbiddenAttributeRule);

    // Class name suffixes that indicate ViewModel/Model classes
    private static readonly string[] ViewModelSuffixes =
    {
        "ViewModel",
        "Model"
    };

    // Forbidden command attribute names (without "Attribute" suffix)
    private static readonly string[] ForbiddenCommandAttributes =
    {
        "RelayCommand",
        "AsyncRelayCommand",
        "Command",
        "AsyncCommand",
        "DelegateCommand",
        "ReactiveCommand"
    };

    // ICommand interface names to detect
    private static readonly string[] CommandInterfaceNames =
    {
        "ICommand",
        "IRelayCommand",
        "IAsyncRelayCommand",
        "RelayCommand",
        "AsyncRelayCommand"
    };

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        // Check for ICommand properties
        context.RegisterSyntaxNodeAction(AnalyzeProperty, SyntaxKind.PropertyDeclaration);

        // Check for forbidden command attributes
        context.RegisterSyntaxNodeAction(AnalyzeAttribute, SyntaxKind.Attribute);
    }

    private static void AnalyzeProperty(SyntaxNodeAnalysisContext context)
    {
        var property = (PropertyDeclarationSyntax)context.Node;

        // Only check in ViewModel/Model classes
        if (!IsInViewModelClass(property))
            return;

        // Get the property type symbol
        var typeInfo = context.SemanticModel.GetTypeInfo(property.Type);
        var typeSymbol = typeInfo.Type;

        if (typeSymbol == null)
            return;

        // Check if property type is ICommand or implements ICommand
        if (IsCommandType(typeSymbol))
        {
            var diagnostic = Diagnostic.Create(
                ICommandRule,
                property.Identifier.GetLocation(),
                property.Identifier.Text);
            context.ReportDiagnostic(diagnostic);
        }
    }

    private static void AnalyzeAttribute(SyntaxNodeAnalysisContext context)
    {
        var attribute = (AttributeSyntax)context.Node;

        // Get the attribute name
        var attributeName = GetAttributeName(attribute);
        if (attributeName == null)
            return;

        // Check if it's a forbidden command attribute
        foreach (var forbidden in ForbiddenCommandAttributes)
        {
            if (attributeName.Equals(forbidden, System.StringComparison.Ordinal) ||
                attributeName.Equals(forbidden + "Attribute", System.StringComparison.Ordinal))
            {
                var diagnostic = Diagnostic.Create(
                    ForbiddenAttributeRule,
                    attribute.GetLocation(),
                    forbidden);
                context.ReportDiagnostic(diagnostic);
                return;
            }
        }
    }

    private static bool IsInViewModelClass(SyntaxNode node)
    {
        var classDeclaration = node.FirstAncestorOrSelf<ClassDeclarationSyntax>();
        if (classDeclaration == null)
            return false;

        var className = classDeclaration.Identifier.Text;

        foreach (var suffix in ViewModelSuffixes)
        {
            if (className.EndsWith(suffix, System.StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    private static bool IsCommandType(ITypeSymbol typeSymbol)
    {
        // Check direct type name
        foreach (var commandName in CommandInterfaceNames)
        {
            if (typeSymbol.Name.Equals(commandName, System.StringComparison.Ordinal))
                return true;
        }

        // Check if it implements ICommand
        if (typeSymbol.AllInterfaces.Any(i =>
            i.Name.Equals("ICommand", System.StringComparison.Ordinal) &&
            i.ContainingNamespace?.ToDisplayString() == "System.Windows.Input"))
        {
            return true;
        }

        // Check the type itself for System.Windows.Input.ICommand
        if (typeSymbol.Name.Equals("ICommand", System.StringComparison.Ordinal))
        {
            var ns = typeSymbol.ContainingNamespace?.ToDisplayString();
            if (ns == "System.Windows.Input")
                return true;
        }

        return false;
    }

    private static string? GetAttributeName(AttributeSyntax attribute)
    {
        return attribute.Name switch
        {
            IdentifierNameSyntax identifierName => identifierName.Identifier.Text,
            QualifiedNameSyntax qualifiedName => qualifiedName.Right.Identifier.Text,
            _ => null
        };
    }
}
