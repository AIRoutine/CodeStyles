using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace AIRoutine.CodeStyle.Analyzers;

/// <summary>
/// Analyzer that ensures UI controls in C# Markup have explicit Style assignments.
/// Detects control instantiation without .Style(...) call in the fluent chain.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class CSharpMarkupStyleAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "ACS0015";

    private static readonly LocalizableString Title =
        "UI control missing Style in C# Markup";

    private static readonly LocalizableString MessageFormat =
        "Control '{0}' is missing a Style assignment. Add .Style(x => x.StaticResource(\"{0}Style\")) for consistent styling.";

    private static readonly LocalizableString Description =
        "UI controls in C# Markup should have explicit Style assignments for consistent theming and maintainability.";

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

    // UI Controls that must have Style
    private static readonly HashSet<string> ControlsRequiringStyle = new(System.StringComparer.OrdinalIgnoreCase)
    {
        // Buttons
        "Button", "RepeatButton", "ToggleButton", "HyperlinkButton", "DropDownButton",
        "SplitButton", "ToggleSplitButton", "AppBarButton", "AppBarToggleButton",

        // Text Controls
        "TextBlock", "TextBox", "PasswordBox", "RichEditBox", "RichTextBlock",
        "AutoSuggestBox", "NumberBox",

        // Selection Controls
        "ComboBox", "ListBox", "ListView", "GridView", "FlipView", "TreeView",
        "ItemsRepeater", "Selector",

        // Toggle Controls
        "CheckBox", "RadioButton", "ToggleSwitch",

        // Range Controls
        "Slider", "RatingControl", "ProgressBar", "ProgressRing",

        // Date/Time Controls
        "CalendarView", "CalendarDatePicker", "DatePicker", "TimePicker",

        // Media Controls
        "Image", "MediaPlayerElement", "PersonPicture", "BitmapIcon", "FontIcon",
        "SymbolIcon", "PathIcon", "ImageIcon",

        // Navigation Controls
        "NavigationView", "TabView", "Pivot", "PivotItem", "Hub", "HubSection",
        "MenuBar", "MenuBarItem", "CommandBar", "AppBar",

        // Dialog/Overlay Controls
        "ContentDialog", "Flyout", "MenuFlyout", "TeachingTip", "InfoBar", "Popup",

        // Container Controls
        "Border", "ContentPresenter", "ContentControl", "Frame",
        "ScrollViewer", "ScrollContentPresenter", "Viewbox",
        "Expander", "SplitView", "NavigationViewItem",

        // List Items
        "ListViewItem", "GridViewItem", "TreeViewItem", "ComboBoxItem", "ListBoxItem"
    };

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterSyntaxNodeAction(AnalyzeObjectCreation, SyntaxKind.ObjectCreationExpression);
    }

    private static void AnalyzeObjectCreation(SyntaxNodeAnalysisContext context)
    {
        var objectCreation = (ObjectCreationExpressionSyntax)context.Node;

        // Get the type name
        string? typeName = null;
        if (objectCreation.Type is IdentifierNameSyntax identifier)
        {
            typeName = identifier.Identifier.Text;
        }
        else if (objectCreation.Type is QualifiedNameSyntax qualified)
        {
            typeName = qualified.Right.Identifier.Text;
        }
        else if (objectCreation.Type is GenericNameSyntax generic)
        {
            typeName = generic.Identifier.Text;
        }

        if (typeName == null || !ControlsRequiringStyle.Contains(typeName))
            return;

        // Check if this is inside a ResourceDictionary or Style definition
        if (IsInsideResourceDefinition(objectCreation))
            return;

        // Check if the fluent chain contains .Style(...)
        if (HasStyleInFluentChain(objectCreation))
            return;

        // Report diagnostic
        var diagnostic = Diagnostic.Create(Rule, objectCreation.GetLocation(), typeName);
        context.ReportDiagnostic(diagnostic);
    }

    private static bool HasStyleInFluentChain(ObjectCreationExpressionSyntax objectCreation)
    {
        // Look for .Style(...) in the parent chain
        SyntaxNode? current = objectCreation.Parent;

        while (current != null)
        {
            // If we're inside an invocation, check if it's part of a fluent chain
            if (current is InvocationExpressionSyntax invocation)
            {
                if (invocation.Expression is MemberAccessExpressionSyntax memberAccess)
                {
                    if (memberAccess.Name.Identifier.Text == "Style")
                    {
                        return true;
                    }
                }
            }
            // If we're the expression in a member access, continue up
            else if (current is MemberAccessExpressionSyntax memberAccess)
            {
                if (memberAccess.Name.Identifier.Text == "Style")
                {
                    return true;
                }
            }
            // Check ancestor invocations
            else if (current is ArgumentSyntax)
            {
                // We're an argument to something else, break out
                break;
            }
            else if (current is ExpressionStatementSyntax ||
                     current is LocalDeclarationStatementSyntax ||
                     current is ReturnStatementSyntax ||
                     current is AssignmentExpressionSyntax)
            {
                break;
            }

            current = current.Parent;
        }

        // Also check if the fluent chain on the right side has Style
        return HasStyleInRightChain(objectCreation);
    }

    private static bool HasStyleInRightChain(SyntaxNode node)
    {
        // Walk up to find the full fluent chain expression
        var parent = node.Parent;
        while (parent != null)
        {
            if (parent is InvocationExpressionSyntax invocation)
            {
                // Check this invocation and all subsequent ones in the chain
                if (CheckInvocationChainForStyle(invocation))
                    return true;
            }
            else if (parent is MemberAccessExpressionSyntax)
            {
                // Continue walking up
            }
            else
            {
                // Reached a non-fluent-chain node
                break;
            }
            parent = parent.Parent;
        }
        return false;
    }

    private static bool CheckInvocationChainForStyle(InvocationExpressionSyntax startInvocation)
    {
        // Check if this or any parent invocation in chain is .Style(...)
        SyntaxNode? current = startInvocation;

        while (current != null)
        {
            if (current is InvocationExpressionSyntax invocation &&
                invocation.Expression is MemberAccessExpressionSyntax memberAccess)
            {
                if (memberAccess.Name.Identifier.Text == "Style")
                    return true;
            }

            // Move up the chain
            if (current.Parent is MemberAccessExpressionSyntax parentMemberAccess &&
                parentMemberAccess.Parent is InvocationExpressionSyntax parentInvocation)
            {
                current = parentInvocation;
            }
            else
            {
                break;
            }
        }

        return false;
    }

    private static bool IsInsideResourceDefinition(SyntaxNode node)
    {
        var current = node.Parent;
        while (current != null)
        {
            // Check if we're inside a method that looks like resource definition
            if (current is MethodDeclarationSyntax method)
            {
                var methodName = method.Identifier.Text;
                if (methodName.Contains("Resource") ||
                    methodName.Contains("Style") ||
                    methodName.Contains("Theme") ||
                    methodName.Contains("Template"))
                {
                    return true;
                }
            }

            // Check if we're in a class that's a ResourceDictionary
            if (current is ClassDeclarationSyntax classDecl)
            {
                var className = classDecl.Identifier.Text;
                if (className.Contains("Resource") ||
                    className.Contains("Theme") ||
                    className.Contains("Style") ||
                    className.Contains("Dictionary"))
                {
                    return true;
                }
            }

            current = current.Parent;
        }
        return false;
    }
}
