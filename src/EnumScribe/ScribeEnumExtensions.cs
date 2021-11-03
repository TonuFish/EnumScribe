using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;

namespace EnumScribe
{
    internal static class ScribeEnumExtensions
    {
        public static HashSet<Accessibility> ToAccessibility(this AccessModifier accessModifier)
        {
            HashSet<Accessibility> accessibility = new() { Accessibility.Public };

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
                        default:
                            // Default case should never happen
                            break;
                    }
                }
            }

            return accessibility;
        }

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

        public static string ToText(this TypeClassification typeClassification) => typeClassification switch
            {
                TypeClassification.Class => "class",
                TypeClassification.Record => "record",
                // Default case should never happen
                _ => string.Empty,
            };
    }
}
