using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace AIRoutine.CodeStyle.Analyzers;

/// <summary>
/// Analyzer that enforces the use of Shiny Mediator HTTP extension instead of direct HttpClient usage.
/// All HTTP calls should go through the Mediator pattern using IHttpRequest&lt;T&gt;.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class HttpClientUsageAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "ACS0019";

    private static readonly LocalizableString Title =
        "Use Shiny Mediator HTTP instead of direct HttpClient";

    private static readonly LocalizableString MessageFormat =
        "{0} Use Shiny Mediator's IHttpRequest<T> pattern instead for consistent HTTP handling";

    private static readonly LocalizableString Description =
        "Direct HttpClient usage is forbidden. All HTTP requests should go through Shiny Mediator using IHttpRequest<T> contracts with [Http] attributes. This ensures consistent configuration, authentication, error handling, and testability.";

    private const string Category = "Design";

    private static readonly DiagnosticDescriptor Rule = new(
        id: DiagnosticId,
        title: Title,
        messageFormat: MessageFormat,
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: Description,
        helpLinkUri: "https://shinylib.net/mediator/extensions/http/");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(Rule);

    // Types that indicate direct HTTP client usage
    private static readonly string[] ForbiddenTypes =
    {
        "HttpClient",
        "IHttpClientFactory",
        "HttpMessageHandler",
        "DelegatingHandler"
    };

    // Methods on HttpClient that are forbidden
    private static readonly string[] ForbiddenHttpClientMethods =
    {
        "GetAsync",
        "PostAsync",
        "PutAsync",
        "DeleteAsync",
        "PatchAsync",
        "SendAsync",
        "GetStringAsync",
        "GetByteArrayAsync",
        "GetStreamAsync"
    };

    // DI registration methods that are forbidden
    private static readonly string[] ForbiddenDiMethods =
    {
        "AddHttpClient"
    };

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        // Detect: new HttpClient()
        context.RegisterSyntaxNodeAction(AnalyzeObjectCreation, SyntaxKind.ObjectCreationExpression);
        context.RegisterSyntaxNodeAction(AnalyzeImplicitObjectCreation, SyntaxKind.ImplicitObjectCreationExpression);

        // Detect: HttpClient as constructor parameter or field
        context.RegisterSyntaxNodeAction(AnalyzeParameter, SyntaxKind.Parameter);
        context.RegisterSyntaxNodeAction(AnalyzeField, SyntaxKind.FieldDeclaration);
        context.RegisterSyntaxNodeAction(AnalyzeProperty, SyntaxKind.PropertyDeclaration);

        // Detect: httpClient.GetAsync(), etc.
        context.RegisterSyntaxNodeAction(AnalyzeInvocation, SyntaxKind.InvocationExpression);
    }

    private static void AnalyzeObjectCreation(SyntaxNodeAnalysisContext context)
    {
        var objectCreation = (ObjectCreationExpressionSyntax)context.Node;
        var typeInfo = context.SemanticModel.GetTypeInfo(objectCreation);

        if (typeInfo.Type == null)
            return;

        var typeName = typeInfo.Type.Name;
        if (typeName == "HttpClient")
        {
            var diagnostic = Diagnostic.Create(
                Rule,
                objectCreation.GetLocation(),
                "Direct HttpClient instantiation is forbidden.");
            context.ReportDiagnostic(diagnostic);
        }
    }

    private static void AnalyzeImplicitObjectCreation(SyntaxNodeAnalysisContext context)
    {
        var objectCreation = (ImplicitObjectCreationExpressionSyntax)context.Node;
        var typeInfo = context.SemanticModel.GetTypeInfo(objectCreation);

        if (typeInfo.Type == null)
            return;

        var typeName = typeInfo.Type.Name;
        if (typeName == "HttpClient")
        {
            var diagnostic = Diagnostic.Create(
                Rule,
                objectCreation.GetLocation(),
                "Direct HttpClient instantiation is forbidden.");
            context.ReportDiagnostic(diagnostic);
        }
    }

    private static void AnalyzeParameter(SyntaxNodeAnalysisContext context)
    {
        var parameter = (ParameterSyntax)context.Node;

        if (parameter.Type == null)
            return;

        var typeInfo = context.SemanticModel.GetTypeInfo(parameter.Type);
        if (typeInfo.Type == null)
            return;

        var typeName = typeInfo.Type.Name;

        // Skip if this is in an IHttpRequestDecorator implementation (allowed)
        if (IsInHttpRequestDecoratorContext(parameter, context.SemanticModel))
            return;

        if (ForbiddenTypes.Contains(typeName))
        {
            var diagnostic = Diagnostic.Create(
                Rule,
                parameter.GetLocation(),
                $"Injecting {typeName} is forbidden.");
            context.ReportDiagnostic(diagnostic);
        }
    }

    private static void AnalyzeField(SyntaxNodeAnalysisContext context)
    {
        var fieldDeclaration = (FieldDeclarationSyntax)context.Node;
        var typeInfo = context.SemanticModel.GetTypeInfo(fieldDeclaration.Declaration.Type);

        if (typeInfo.Type == null)
            return;

        var typeName = typeInfo.Type.Name;

        // Skip if this is in an IHttpRequestDecorator implementation
        if (IsInHttpRequestDecoratorClass(fieldDeclaration, context.SemanticModel))
            return;

        if (ForbiddenTypes.Contains(typeName))
        {
            var diagnostic = Diagnostic.Create(
                Rule,
                fieldDeclaration.GetLocation(),
                $"Storing {typeName} as a field is forbidden.");
            context.ReportDiagnostic(diagnostic);
        }
    }

    private static void AnalyzeProperty(SyntaxNodeAnalysisContext context)
    {
        var propertyDeclaration = (PropertyDeclarationSyntax)context.Node;
        var typeInfo = context.SemanticModel.GetTypeInfo(propertyDeclaration.Type);

        if (typeInfo.Type == null)
            return;

        var typeName = typeInfo.Type.Name;

        // Skip if this is in an IHttpRequestDecorator implementation
        if (IsInHttpRequestDecoratorClass(propertyDeclaration, context.SemanticModel))
            return;

        if (ForbiddenTypes.Contains(typeName))
        {
            var diagnostic = Diagnostic.Create(
                Rule,
                propertyDeclaration.GetLocation(),
                $"Storing {typeName} as a property is forbidden.");
            context.ReportDiagnostic(diagnostic);
        }
    }

    private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;

        // Check for httpClient.GetAsync(), etc.
        if (invocation.Expression is MemberAccessExpressionSyntax memberAccess)
        {
            var methodName = memberAccess.Name.Identifier.Text;

            // Check if calling forbidden HttpClient methods
            if (ForbiddenHttpClientMethods.Contains(methodName))
            {
                var symbolInfo = context.SemanticModel.GetSymbolInfo(memberAccess);
                if (symbolInfo.Symbol is IMethodSymbol methodSymbol)
                {
                    var containingTypeName = methodSymbol.ContainingType?.Name;
                    if (containingTypeName == "HttpClient")
                    {
                        var diagnostic = Diagnostic.Create(
                            Rule,
                            invocation.GetLocation(),
                            $"Calling HttpClient.{methodName}() is forbidden.");
                        context.ReportDiagnostic(diagnostic);
                        return;
                    }
                }
            }

            // Check for services.AddHttpClient() DI registration
            if (ForbiddenDiMethods.Contains(methodName))
            {
                var symbolInfo = context.SemanticModel.GetSymbolInfo(memberAccess);
                if (symbolInfo.Symbol is IMethodSymbol methodSymbol)
                {
                    var containingNamespace = methodSymbol.ContainingType?.ContainingNamespace?.ToDisplayString();
                    if (containingNamespace?.Contains("Microsoft.Extensions") == true ||
                        methodSymbol.ContainingType?.Name == "HttpClientFactoryServiceCollectionExtensions")
                    {
                        var diagnostic = Diagnostic.Create(
                            Rule,
                            invocation.GetLocation(),
                            "Registering HttpClient via AddHttpClient() is forbidden.");
                        context.ReportDiagnostic(diagnostic);
                    }
                }
            }
        }
    }

    /// <summary>
    /// Check if the parameter is in a method of an IHttpRequestDecorator implementation.
    /// These are allowed to use HttpRequestMessage.
    /// </summary>
    private static bool IsInHttpRequestDecoratorContext(ParameterSyntax parameter, SemanticModel semanticModel)
    {
        var containingClass = parameter.FirstAncestorOrSelf<ClassDeclarationSyntax>();
        if (containingClass == null)
            return false;

        return IsHttpRequestDecoratorClass(containingClass, semanticModel);
    }

    private static bool IsInHttpRequestDecoratorClass(SyntaxNode node, SemanticModel semanticModel)
    {
        var containingClass = node.FirstAncestorOrSelf<ClassDeclarationSyntax>();
        if (containingClass == null)
            return false;

        return IsHttpRequestDecoratorClass(containingClass, semanticModel);
    }

    private static bool IsHttpRequestDecoratorClass(ClassDeclarationSyntax classDeclaration, SemanticModel semanticModel)
    {
        var classSymbol = semanticModel.GetDeclaredSymbol(classDeclaration);
        if (classSymbol == null)
            return false;

        // Check if implements IHttpRequestDecorator
        foreach (var iface in classSymbol.AllInterfaces)
        {
            if (iface.Name == "IHttpRequestDecorator")
                return true;
        }

        return false;
    }
}
