using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using RedShirt.Example.Api.Common.Analyzers.Database.Abstractions.Attributes;
using RedShirt.Example.Api.Common.Analyzers.Database.Generation.GenerationLogic;

namespace RedShirt.Example.Api.Common.Analyzers.Database.Generation;

[Generator]
public class ServiceGenerator : IIncrementalGenerator
{
    private static bool IsCandidate(SyntaxNode node)
    {
        return node is ClassDeclarationSyntax {AttributeLists.Count: > 0};
    }

    private static INamedTypeSymbol? GetSemanticTarget(GeneratorSyntaxContext context)
    {
        var classDecl = (ClassDeclarationSyntax) context.Node;
        var symbol = context.SemanticModel.GetDeclaredSymbol(classDecl);
        if (symbol == null)
        {
            return null;
        }

        var serializableAttr = context.SemanticModel.Compilation
            .GetTypeByMetadataName(typeof(DbTableAttribute).FullName!);

        if (serializableAttr is null)
        {
            return null;
        }

        // Only pick classes with [DbTable]
        return symbol.GetAttributes().Any(a =>
            SymbolEqualityComparer.Default.Equals(a.AttributeClass, serializableAttr))
            ? symbol
            : null;
    }

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Step 1: Find all class declarations with [DbTable]
        var classDeclarations = context.SyntaxProvider
            .CreateSyntaxProvider(
                static (s, _) => IsCandidate(s), // quick filter
                static (ctx, _) => GetSemanticTarget(ctx)) // get symbol
            .Where(static m => m is not null)!;

        // Step 2: Generate code for each matched class
        context.RegisterSourceOutput(classDeclarations,
            static (spc, classSymbol) => { CentralContentGenerator.Generate(spc, classSymbol!); });
    }
}