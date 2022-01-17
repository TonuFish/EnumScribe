using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using static EnumScribe.Generator.EnumScribeConsts;

namespace EnumScribe.Generator
{
    internal static class EnumScribeExtensions
    {
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
