using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Linq;
using System.Collections.Generic;

namespace EnumScribe
{
    internal class ScribeEnumSyntaxReceiver : ISyntaxContextReceiver
    {
        public List<INamedTypeSymbol> ClassSymbolsWithScribeAttribute { get; } = new();
        //public List<StructDeclarationSyntax> StructsWithAttributes { get; } = new();
        //public List<MemberDeclarationSyntax> MembersWithIgnoreScribeAttribute

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
                    ClassSymbolsWithScribeAttribute.Add(symbol);
                }
            }
            //else if (syntaxNode is StructDeclarationSyntax
            //    {
            //        AttributeLists:
            //        {
            //            Count: > 0
            //        }
            //    } sds)
            //{
            //    StructsWithAttributes.Add(sds);
            //}
            //else if (context.Node is PropertyDeclarationSyntax or FieldDeclarationSyntax
            //    {
            //        AttributeLists:
            //        {
            //            Count: > 0
            //        }
            //    } mds)
            //{

            //}
        }
    }
}
