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
/// Offers to create an interface and inject it via constructor, with interface generation.
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

        // Code fix 1: Simple injection (existing behavior)
        context.RegisterCodeFix(
            CodeAction.Create(
                title: $"Inject '{interfaceName}' via constructor",
                createChangedDocument: c => InjectDependencyAsync(
                    context.Document, invocation, memberAccess, typeName, methodName, fieldName, interfaceName, methodSymbol, c),
                equivalenceKey: "InjectDependency"),
            diagnostic);

        // Code fix 2: Generate interface and inject
        context.RegisterCodeFix(
            CodeAction.Create(
                title: $"Generate '{interfaceName}' interface and inject",
                createChangedSolution: c => GenerateInterfaceAndInjectAsync(
                    context.Document, invocation, memberAccess, typeName, methodName, fieldName, interfaceName, methodSymbol, c),
                equivalenceKey: "GenerateInterfaceAndInject"),
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
        IMethodSymbol methodSymbol,
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
            newRoot = AddFieldAndConstructorParameter(newRoot, containingType, typeName, fieldName, interfaceName);
        }

        return document.WithSyntaxRoot(newRoot);
    }

    private static async Task<Solution> GenerateInterfaceAndInjectAsync(
        Document document,
        InvocationExpressionSyntax invocation,
        MemberAccessExpressionSyntax memberAccess,
        string typeName,
        string methodName,
        string fieldName,
        string interfaceName,
        IMethodSymbol methodSymbol,
        CancellationToken cancellationToken)
    {
        var solution = document.Project.Solution;
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root == null) return solution;

        // Find the containing type
        var containingType = invocation.FirstAncestorOrSelf<ClassDeclarationSyntax>();
        if (containingType == null) return solution;

        // Get the namespace
        var namespaceDecl = containingType.FirstAncestorOrSelf<BaseNamespaceDeclarationSyntax>();
        var namespaceName = namespaceDecl?.Name.ToString() ?? "Generated";

        // 1. Generate interface content
        var interfaceContent = GenerateInterfaceCode(interfaceName, typeName, methodSymbol, namespaceName);

        // 2. Add the interface file to the project
        var interfaceFileName = $"{interfaceName}.cs";
        var newDocument = document.Project.AddDocument(interfaceFileName, interfaceContent);
        solution = newDocument.Project.Solution;

        // 3. Update the original document with injection
        var updatedDocument = solution.GetDocument(document.Id);
        if (updatedDocument == null) return solution;

        var updatedRoot = await updatedDocument.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (updatedRoot == null) return solution;

        // Find the invocation again in the updated tree
        var updatedInvocation = updatedRoot.FindToken(invocation.SpanStart).Parent?.AncestorsAndSelf()
            .OfType<InvocationExpressionSyntax>().FirstOrDefault();

        if (updatedInvocation == null) return solution;

        var updatedContainingType = updatedInvocation.FirstAncestorOrSelf<ClassDeclarationSyntax>();
        if (updatedContainingType == null) return solution;

        // Check if field already exists
        var existingField = updatedContainingType.Members
            .OfType<FieldDeclarationSyntax>()
            .Any(f => f.Declaration.Variables.Any(v => v.Identifier.Text == fieldName));

        // Replace the static call with instance call
        var newMemberAccess = SyntaxFactory.MemberAccessExpression(
            SyntaxKind.SimpleMemberAccessExpression,
            SyntaxFactory.IdentifierName(fieldName),
            SyntaxFactory.IdentifierName(methodName));

        var newInvocation = updatedInvocation.WithExpression(newMemberAccess);
        var newRoot = updatedRoot.ReplaceNode(updatedInvocation, newInvocation);

        if (!existingField)
        {
            newRoot = AddFieldAndConstructorParameter(newRoot, updatedContainingType, typeName, fieldName, interfaceName);
        }

        solution = solution.WithDocumentSyntaxRoot(document.Id, newRoot);

        return solution;
    }

    private static string GenerateInterfaceCode(string interfaceName, string typeName, IMethodSymbol methodSymbol, string namespaceName)
    {
        var returnType = methodSymbol.ReturnType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
        var parameters = string.Join(", ",
            methodSymbol.Parameters.Select(p =>
                $"{p.Type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)} {p.Name}"));

        return $@"namespace {namespaceName};

/// <summary>
/// Interface for {typeName} to enable dependency injection.
/// Generated by ACS0002 code fix.
/// </summary>
/// <remarks>
/// Register in DI container:
///   services.AddSingleton&lt;{interfaceName}, {typeName}&gt;();
/// or
///   services.AddScoped&lt;{interfaceName}, {typeName}&gt;();
/// </remarks>
public interface {interfaceName}
{{
    {returnType} {methodSymbol.Name}({parameters});
}}

/// <summary>
/// Default implementation wrapper for {typeName}.
/// Delegates to the original static methods.
/// </summary>
public sealed class {typeName}Wrapper : {interfaceName}
{{
    public {returnType} {methodSymbol.Name}({parameters})
    {{
        {(returnType == "void" ? "" : "return ")}{typeName}.{methodSymbol.Name}({string.Join(", ", methodSymbol.Parameters.Select(p => p.Name))});
    }}
}}
";
    }

    private static SyntaxNode AddFieldAndConstructorParameter(
        SyntaxNode root,
        ClassDeclarationSyntax originalContainingType,
        string typeName,
        string fieldName,
        string interfaceName)
    {
        // Find the class in the new tree
        var newContainingType = root.DescendantNodes()
            .OfType<ClassDeclarationSyntax>()
            .FirstOrDefault(c => c.Identifier.Text == originalContainingType.Identifier.Text);

        if (newContainingType == null) return root;

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
        var parameterName = char.ToLowerInvariant(typeName[0]) + typeName.Substring(1);

        if (constructor != null)
        {
            // Add parameter to existing constructor
            var newParameter = SyntaxFactory.Parameter(
                SyntaxFactory.Identifier(parameterName))
                .WithType(SyntaxFactory.IdentifierName(interfaceName));

            var newParameterList = constructor.ParameterList.AddParameters(newParameter);

            // Add assignment to constructor body
            var assignment = SyntaxFactory.ExpressionStatement(
                SyntaxFactory.AssignmentExpression(
                    SyntaxKind.SimpleAssignmentExpression,
                    SyntaxFactory.IdentifierName(fieldName),
                    SyntaxFactory.IdentifierName(parameterName)));

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

        return root.ReplaceNode(newContainingType, updatedType);
    }
}
