using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Linq;
using System.Collections.Generic;

namespace EnumScribe
{
    internal class EnumScribeSyntaxReceiver : ISyntaxContextReceiver
    {
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
        }
    }
}
