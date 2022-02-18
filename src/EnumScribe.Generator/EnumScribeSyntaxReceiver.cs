using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Linq;
using System.Collections.Generic;

namespace EnumScribe.Generator
{
    /// <summary>
    /// Filters and records the <see cref="SyntaxNode"/>s relevant to EnumScribe generator.
    /// </summary>
    internal sealed class EnumScribeSyntaxReceiver : ISyntaxContextReceiver
    {
        public List<IFieldSymbol> NoScribeAttributeFieldSymbols { get; } = new();
        public List<IPropertySymbol> NoScribeAttributePropertySymbols { get; } = new();
        public List<INamedTypeSymbol> ScribeAttributeSymbols { get; } = new();

        public void OnVisitSyntaxNode(GeneratorSyntaxContext context)
        {
            if (context.Node is TypeDeclarationSyntax tds && tds.AttributeLists.Count > 0)
            {
                var symbol = (INamedTypeSymbol)context.SemanticModel.GetDeclaredSymbol(tds)!;
                if (symbol.GetAttributes().Any(x => x.AttributeClass?.Name == nameof(ScribeAttribute)))
                {
                    ScribeAttributeSymbols.Add(symbol);
                }
            }
            else if (context.Node is PropertyDeclarationSyntax pds && pds.AttributeLists.Count > 0)
            {
                var symbol = (IPropertySymbol)context.SemanticModel.GetDeclaredSymbol(pds)!;
                if (symbol.GetAttributes().Any(x => x.AttributeClass?.Name == nameof(NoScribeAttribute)))
                {
                    NoScribeAttributePropertySymbols.Add(symbol);
                }
            }
            else if (context.Node is FieldDeclarationSyntax fds && fds.AttributeLists.Count > 0)
            {
                var symbol = (IFieldSymbol)context.SemanticModel.GetDeclaredSymbol(fds)!;
                if ((symbol.IsImplicitlyDeclared == false)
                    && symbol.GetAttributes().Any(x => x.AttributeClass?.Name == nameof(NoScribeAttribute)))
                {
                    NoScribeAttributeFieldSymbols.Add(symbol);
                }
            }
        }
    }
}
