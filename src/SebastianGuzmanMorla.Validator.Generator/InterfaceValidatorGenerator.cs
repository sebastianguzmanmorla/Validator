using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace SebastianGuzmanMorla.Validator.Generator;

[Generator]
public sealed class InterfaceValidatorGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        IncrementalValueProvider<ImmutableArray<(ClassDeclarationSyntax Left, Compilation Right)>> candidates = context
            .SyntaxProvider
            .CreateSyntaxProvider(
                static (node, _) => node is ClassDeclarationSyntax { BaseList: not null },
                static (ctx, _) => (ClassDeclarationSyntax)ctx.Node)
            .Combine(context.CompilationProvider)
            .Where(tuple =>
            {
                (ClassDeclarationSyntax? declarationSyntax, Compilation? compilation) = tuple;
                SemanticModel semanticModel = compilation.GetSemanticModel(declarationSyntax.SyntaxTree);
                INamedTypeSymbol? typeSymbol = semanticModel.GetDeclaredSymbol(declarationSyntax);

                if (typeSymbol?.BaseType is null)
                {
                    return false;
                }

                INamedTypeSymbol validatorSymbol =
                    compilation.GetTypeByMetadataName("SebastianGuzmanMorla.Validator.Validator`1") ??
                    throw new Exception("SebastianGuzmanMorla.Validator.Validator`1");

                return SymbolEqualityComparer.Default.Equals(typeSymbol.BaseType.OriginalDefinition, validatorSymbol);
            })
            .Collect();

        context.RegisterSourceOutput(candidates, static (context, classes) =>
        {
            foreach ((ClassDeclarationSyntax syntax, Compilation compilation) in classes)
            {
                SemanticModel model = compilation.GetSemanticModel(syntax.SyntaxTree);
                INamedTypeSymbol? classSymbol = model.GetDeclaredSymbol(syntax);

                if (classSymbol?.BaseType is null)
                {
                    continue;
                }

                INamedTypeSymbol baseType = classSymbol.BaseType;
                INamedTypeSymbol entityType = (INamedTypeSymbol)baseType.TypeArguments[0];

                INamedTypeSymbol iEntityValidation =
                    compilation.GetTypeByMetadataName("SebastianGuzmanMorla.Validator.Interfaces.IEntityValidation") ??
                    throw new Exception("SebastianGuzmanMorla.Validator.Interfaces.IEntityValidation");

                List<INamedTypeSymbol> validations = entityType
                    .AllInterfaces
                    .Where(i => i.AllInterfaces.Contains(iEntityValidation, SymbolEqualityComparer.Default))
                    .ToList();

                if (validations.Count == 0)
                {
                    continue;
                }

                IEnumerable<string> items = validations.Select(i =>
                    $"        (serviceProvider, entity, cancellationToken) => serviceProvider.GetRequiredService<IValidator<{i.ToDisplayString()}>>().Validate(entity, serviceProvider, cancellationToken)"
                );

                StringBuilder sb = new();

                sb.AppendLine("using System;");
                sb.AppendLine("using System.Collections.Immutable;");
                sb.AppendLine("using System.Threading;");
                sb.AppendLine("using System.Threading.Tasks;");
                sb.AppendLine("using Microsoft.Extensions.DependencyInjection;");
                sb.AppendLine("using SebastianGuzmanMorla.Validator;");
                sb.AppendLine("using SebastianGuzmanMorla.Validator.Interfaces;");
                sb.AppendLine();
                sb.AppendLine($"namespace {classSymbol.ContainingNamespace.ToDisplayString()};");
                sb.AppendLine();
                sb.AppendLine($"public partial class {classSymbol.Name}");
                sb.AppendLine("{");
                sb.AppendLine(
                    $"    protected override ImmutableArray<Func<IServiceProvider, {entityType.ToDisplayString()}, CancellationToken, Task<ValidationResult>>> InterfaceValidations =>");
                sb.AppendLine("    [");
                sb.AppendLine(string.Join(",\n", items));
                sb.AppendLine("    ];");
                sb.AppendLine("}");

                context.AddSource($"{classSymbol.Name}.g.cs", sb.ToString());
            }
        });
    }
}