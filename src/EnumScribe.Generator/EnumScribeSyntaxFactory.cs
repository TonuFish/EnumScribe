using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System.Collections.Generic;
using System.Text;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace EnumScribe.Generator
{
    // Massive thanks to Kirill for RoslynQuoter (https://github.com/KirillOsenkov/RoslynQuoter)
    // Unbelievably handy to get a solid starting point when writing a syntax factory :)

    /// <summary>
    /// A <see cref="SyntaxFactory"/> to generate EnumScribe source text.
    /// </summary>
    internal static class EnumScribeSyntaxFactory
    {
        /// <summary>
        /// Generates a switch expression for the supplied enum details.
        /// </summary>
        /// <remarks>
        /// Each enum is mapped to a description, with a default <c>string.Empty</c> return for unknown varients.
        /// </remarks>
        /// <param name="enumInfo">The enum details to generate for.</param>
        /// <returns>The switch expression syntax for the supplied enum details.</returns>
        private static SyntaxNodeOrToken[] GenerateEnumMethodSwitchArmsSyntax(EnumInfo enumInfo)
        {
            // Member + comma syntax
            var enumMemberSyntaxCount = enumInfo.EnumNameDescriptionPairs.Count * 2;
            // count + switch default arm and comma
            var switchExpressionArmsSyntax = new SyntaxNodeOrToken[enumMemberSyntaxCount + 2];

            // Build switch body
            for (int syntaxIdx = 0, enumIdx = 0; syntaxIdx < enumMemberSyntaxCount; syntaxIdx += 2, ++enumIdx)
            {
                var (name, description) = enumInfo.EnumNameDescriptionPairs[enumIdx];

                // Pattern => Expression,
                switchExpressionArmsSyntax[syntaxIdx] =
                SwitchExpressionArm
                (
                    // Pattern
                    ConstantPattern(
                        MemberAccessExpression(
                            SyntaxKind.SimpleMemberAccessExpression,
                            IdentifierName(enumInfo.OutputFullName),
                            IdentifierName(name))),
                    // Expression
                    LiteralExpression(
                        SyntaxKind.StringLiteralExpression,
                        Literal(description)));

                // Comma
                switchExpressionArmsSyntax[syntaxIdx + 1] = Token(SyntaxKind.CommaToken);
            }

            // _ => string.Empty
            switchExpressionArmsSyntax[switchExpressionArmsSyntax.Length - 2] =
            SwitchExpressionArm
            (
                DiscardPattern(),
                MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    PredefinedType(Token(SyntaxKind.StringKeyword)),
                    IdentifierName("Empty")));

            // Comma
            switchExpressionArmsSyntax[switchExpressionArmsSyntax.Length - 1] = Token(SyntaxKind.CommaToken);

            return switchExpressionArmsSyntax;
        }

        /// <summary>
        /// Generates an extension method for the supplied enum details.
        /// </summary>
        /// <remarks>
        /// Each extension method is marked with <see cref="System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining"/>
        /// to encourage the compiler to inline the calls in the partials file.
        /// </remarks>
        /// <param name="enumInfos">The enum details to generate for.</param>
        /// <returns>The extension method syntax for the supplied enum details.</returns>
        private static MemberDeclarationSyntax[] GenerateEnumMethodSyntaxes(List<EnumInfo> enumInfos)
        {
            MemberDeclarationSyntax[] enumMethodSyntaxes = new MemberDeclarationSyntax[enumInfos.Count];

            for (var ii = 0; ii < enumInfos.Count; ++ii)
            {
                var enumInfo = enumInfos[ii];
                var switchExpressionArmsSyntax = GenerateEnumMethodSwitchArmsSyntax(enumInfo);

                // Build the method
                enumMethodSyntaxes[ii] =
                // string DescriptionText
                MethodDeclaration
                (
                    PredefinedType(Token(SyntaxKind.StringKeyword)),
                    Identifier("DescriptionText")
                )
                // [MethodImpl]
                .WithAttributeLists(SingletonList<AttributeListSyntax>(
                    AttributeList(SingletonSeparatedList<AttributeSyntax>(
                        Attribute(IdentifierName("MethodImpl"))
                        // (MethodImplOptions.AggressiveInlining)
                        .WithArgumentList(
                            AttributeArgumentList(SingletonSeparatedList<AttributeArgumentSyntax>(
                                AttributeArgument(
                                    MemberAccessExpression(
                                        SyntaxKind.SimpleMemberAccessExpression,
                                        IdentifierName("MethodImplOptions"),
                                        IdentifierName("AggressiveInlining"))))))))))
                // public static
                .WithModifiers(TokenList(new[] { Token(SyntaxKind.PublicKeyword), Token(SyntaxKind.StaticKeyword) } ))
                // (this Fully.Qualified.Enum e)
                .WithParameterList
                (
                    ParameterList(SingletonSeparatedList<ParameterSyntax>(
                        Parameter(Identifier("e"))
                        .WithModifiers(TokenList(Token(SyntaxKind.ThisKeyword)))
                        .WithType(IdentifierName(enumInfo.OutputFullName))))
                )
                // => e switch { arms }
                .WithExpressionBody
                (
                    ArrowExpressionClause(
                        SwitchExpression(IdentifierName("e"))
                        .WithArms(SeparatedList<SwitchExpressionArmSyntax>(switchExpressionArmsSyntax)))
                )
                // Body closing ;
                .WithSemicolonToken(Token(SyntaxKind.SemicolonToken));
            }

            return enumMethodSyntaxes;
        }

        /// <summary>
        /// Generates the enum extensions file contents.
        /// </summary>
        /// <param name="enumInfos">The enum details to include.</param>
        /// <param name="indentWidth">The number of whitespaces (' ') used each indentation level.</param>
        /// <returns>The source text for the enum extensions.</returns>
        public static SourceText GenerateEnumsSource(List<EnumInfo> enumInfos, int indentWidth)
        {
            var enumMethodSyntax = GenerateEnumMethodSyntaxes(enumInfos);

            var unit = CompilationUnit()
            .WithUsings
            (
                SingletonList<UsingDirectiveSyntax>(
                    // using System.Runtime.CompilerServices
                    UsingDirective(IdentifierName("System.Runtime.CompilerServices"))
                    .WithUsingKeyword
                    (
                        Token(
                            TriviaList(new[]
                            {
                                // // <autogenerated/>
                                Comment("// <autogenerated/>"),
                                // #nullable enable
                                Trivia(NullableDirectiveTrivia(Token(SyntaxKind.EnableKeyword), true))
                            }),
                            SyntaxKind.UsingKeyword,
                            TriviaList())
                    ))
            )
            .WithMembers
            (
                SingletonList<MemberDeclarationSyntax>(
                    NamespaceDeclaration(IdentifierName("EnumScribe.Extensions"))
                    .WithMembers
                    (
                        SingletonList<MemberDeclarationSyntax>(
                            // class EnumDescriptions
                            ClassDeclaration("EnumDescriptions")
                            // [System.CodeDom.Compiler.GeneratedCodeAttribute]
                            .WithAttributeLists
                            (
                                SingletonList<AttributeListSyntax>(
                                    AttributeList(SingletonSeparatedList<AttributeSyntax>(
                                        Attribute(IdentifierName("System.CodeDom.Compiler.GeneratedCodeAttribute"))
                                        // ("EnumScribe", "x.y.z")
                                        .WithArgumentList
                                        (
                                            AttributeArgumentList(SeparatedList<AttributeArgumentSyntax>(
                                                new SyntaxNodeOrToken[]
                                                {
                                                    AttributeArgument(LiteralExpression(
                                                        SyntaxKind.StringLiteralExpression,
                                                        Literal("EnumScribe"))),
                                                    Token(SyntaxKind.CommaToken),
                                                    AttributeArgument(LiteralExpression(
                                                        SyntaxKind.StringLiteralExpression,
                                                        Literal(EnumScribeConsts.PackageVersion)))
                                                }))
                                        ))))
                            )
                            // public static
                            .WithModifiers(TokenList(new[] { Token(SyntaxKind.PublicKeyword), Token(SyntaxKind.StaticKeyword) } ))
                            // DescriptionText members
                            .WithMembers(List(enumMethodSyntax)))
                    ))
            )
            .WithEndOfFileToken
            (
                // #nullable restore
                Token(TriviaList(Trivia(NullableDirectiveTrivia(Token(SyntaxKind.RestoreKeyword), true))),
                SyntaxKind.EndOfFileToken,
                TriviaList())
            )
            .NormalizeWhitespace(indentation: new string(' ', indentWidth));

            return unit.GetText(Encoding.UTF8, SourceHashAlgorithm.Sha256);
        }
    }
}
