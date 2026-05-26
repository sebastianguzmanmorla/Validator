using System;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace SebastianGuzmanMorla.Validator.Generator;

[Generator]
public sealed class ConfigureServicesGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        IncrementalValuesProvider<ClassDeclarationSyntax> classDeclarations =
            context.SyntaxProvider.CreateSyntaxProvider(
                static (node, _) => node is ClassDeclarationSyntax,
                static (ctx, _) => (ClassDeclarationSyntax)ctx.Node
            );

        IncrementalValueProvider<ImmutableArray<ClassDeclarationSyntax>> configureServicesClass = classDeclarations
            .Where(static c =>
                c.Identifier.Text == "ConfigureServices" &&
                c.Modifiers.Any(m => m.IsKind(SyntaxKind.PartialKeyword)))
            .Collect();

        IncrementalValueProvider<ImmutableArray<ClassDeclarationSyntax>> candidates = classDeclarations
            .Where(static c => c.BaseList is not null)
            .Collect();

        context.RegisterSourceOutput(
            context.CompilationProvider.Combine(candidates).Combine(configureServicesClass),
            static (context, tuple) =>
            {
                ((Compilation? compilation, ImmutableArray<ClassDeclarationSyntax> candidates),
                    ImmutableArray<ClassDeclarationSyntax> configureClasses) = tuple;

                if (configureClasses.Length == 0)
                {
                    return;
                }

                string? targetNamespace = GetNamespace(configureClasses.First());

                if (targetNamespace is null)
                {
                    return;
                }

                INamedTypeSymbol validatorSymbol =
                    compilation.GetTypeByMetadataName("SebastianGuzmanMorla.Validator.Interfaces.IValidator`1") ??
                    throw new Exception("SebastianGuzmanMorla.Validator.Interfaces.IValidator`1");

                StringBuilder sourceBuilder = new();

                sourceBuilder.AppendLine("using Microsoft.Extensions.DependencyInjection;");
                sourceBuilder.AppendLine();
                sourceBuilder.AppendLine($"namespace {targetNamespace};");
                sourceBuilder.AppendLine();
                sourceBuilder.AppendLine("public static partial class ConfigureServices");
                sourceBuilder.AppendLine("{");
                sourceBuilder.AppendLine(
                    "    private static partial void RegisterValidators(IServiceCollection services)");
                sourceBuilder.AppendLine("    {");

                foreach (ClassDeclarationSyntax? declaration in candidates)
                {
                    SemanticModel semanticModel = compilation.GetSemanticModel(declaration.SyntaxTree);

                    if (ModelExtensions.GetDeclaredSymbol(semanticModel, declaration) is not INamedTypeSymbol
                        namedTypeSymbol)
                    {
                        continue;
                    }

                    if (namedTypeSymbol.IsAbstract)
                    {
                        continue;
                    }

                    foreach (INamedTypeSymbol typeSymbol in namedTypeSymbol.AllInterfaces.Where(typeSymbol =>
                                 SymbolEqualityComparer.Default.Equals(typeSymbol.OriginalDefinition, validatorSymbol)))
                        sourceBuilder.AppendLine(
                            $"        services.AddSingleton(typeof({typeSymbol.ToDisplayString()}), typeof({namedTypeSymbol.ToDisplayString()}));");
                }

                sourceBuilder.AppendLine("    }");
                sourceBuilder.AppendLine("}");

                context.AddSource("ConfigureServices.RegisterValidators.g.cs", sourceBuilder.ToString());
            });
    }

    private static string? GetNamespace(ClassDeclarationSyntax classDeclaration)
    {
        SyntaxNode? parent = classDeclaration.Parent;

        while (parent != null)
        {
            switch (parent)
            {
                case NamespaceDeclarationSyntax nds: return nds.Name.ToString();
                case FileScopedNamespaceDeclarationSyntax fnds: return fnds.Name.ToString();
            }

            parent = parent.Parent;
        }

        return null;
    }
}