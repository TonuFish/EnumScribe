using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using static EnumScribe.Generator.Consts;

namespace EnumScribe.Generator
{
    internal static class Extensions
    {
        /// <summary>
        /// Converts the Scribe <see cref="AccessModifier"/> flags to standard <see cref="Accessibility"/> enums.
        /// </summary>
        /// <param name="accessModifier">The access modifiers to convert.</param>
        /// <returns>A set of <see cref="Accessibility"/> mirroring the supplied <see cref="AccessModifier"/>s.</returns>
        public static HashSet<Accessibility> ToAccessibility(this AccessModifier accessModifier)
        {
            HashSet<Accessibility> accessibility = new() { Defaults.Accessibility };

            foreach (AccessModifier mod in Enum.GetValues(typeof(AccessModifier)))
            {
                if ((accessModifier & mod) > 0 && mod is not AccessModifier.All)
                {
                    switch (mod)
                    {
                        case AccessModifier.Public:
                            accessibility.Add(Accessibility.Public);
                            break;
                        case AccessModifier.Private:
                            accessibility.Add(Accessibility.Private);
                            break;
                        case AccessModifier.Protected:
                            accessibility.Add(Accessibility.Protected);
                            break;
                        case AccessModifier.Internal:
                            accessibility.Add(Accessibility.Internal);
                            break;
                        case AccessModifier.ProtectedInternal:
                            accessibility.Add(Accessibility.ProtectedOrInternal);
                            break;
                        case AccessModifier.PrivateProtected:
                            accessibility.Add(Accessibility.ProtectedAndInternal);
                            break;
                    }
                }
            }

            return accessibility;
        }

        /// <summary>
        /// Converts <see cref="Accessibility"/> to the source code equivalent text.
        /// </summary>
        /// <remarks>
        /// This is effectively a const string version <see cref="Microsoft.CodeAnalysis.CSharp.SyntaxKind"/> ToString.
        /// </remarks>
        /// <param name="accessibility">The accessibility to convert to text.</param>
        /// <returns>The source text representation of the supplied <see cref="Accessibility"/>.</returns>
        public static string ToText(this Accessibility accessibility) => accessibility switch
            {
                Accessibility.Private => "private",
                Accessibility.ProtectedAndInternal => "private protected",
                Accessibility.Protected => "protected",
                Accessibility.Internal => "internal",
                Accessibility.ProtectedOrInternal => "protected internal",
                Accessibility.Public => "public",
                // Default case should never happen
                _ => string.Empty,
            };

        /// <summary>
        /// Converts <see cref="Type"/> to the source code equivalent text.
        /// </summary>
        /// <remarks>
        /// This is effectively a const string version <see cref="Microsoft.CodeAnalysis.CSharp.SyntaxKind"/> ToString.
        /// </remarks>
        /// <param name="typeClassification"></param>
        /// <returns>The source text representation of the supplied <see cref="Type"/>.</returns>
        public static string ToText(this Type typeClassification) => typeClassification switch
            {
                Type.Class => "class",
                Type.Record => "record",
                Type.Struct => "struct",
                Type.RecordStruct => "record struct",
                // Default case should never happen
                _ => string.Empty,
            };
    }
}
