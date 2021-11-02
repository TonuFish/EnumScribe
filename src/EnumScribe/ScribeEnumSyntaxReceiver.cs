using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Linq;
using System.Collections.Generic;

namespace EnumScribe
{
    internal class ScribeEnumSyntaxReceiver : ISyntaxContextReceiver
    {
        public List<INamedTypeSymbol> ScribeAttributeSymbols { get; } = new();

        public void OnVisitSyntaxNode(GeneratorSyntaxContext context)
        {
            if (context.Node is ClassDeclarationSyntax
                {
                    AttributeLists:
                    {
                        Count: > 0
                    }
                } cds)
            {
                var symbol = (INamedTypeSymbol)context.SemanticModel.GetDeclaredSymbol(cds)!;
                if (symbol.GetAttributes().Any(x => x.AttributeClass?.Name == nameof(ScribeAttribute)))
                {
                    ScribeAttributeSymbols.Add(symbol);
                }
            }
        }
    }
}
