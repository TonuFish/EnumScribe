using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;

namespace EnumScribe
{
    internal static class ScribeEnumExtensions
    {
        private static readonly IReadOnlyDictionary<AccessModifier, Accessibility> AccessibilityByAccessModifier =
            new Dictionary<AccessModifier, Accessibility>
            {
                { AccessModifier.Public, Accessibility.Public },
                { AccessModifier.Private, Accessibility.Private },
                { AccessModifier.Protected, Accessibility.Protected },
                { AccessModifier.Internal, Accessibility.Internal },
                { AccessModifier.ProtectedInternal, Accessibility.ProtectedOrInternal },
                { AccessModifier.PrivateProtected, Accessibility.ProtectedAndInternal },
            };

        public static HashSet<Accessibility> ToAccessibility(this AccessModifier accessModifier)
        {
            HashSet<Accessibility> accessibility = new() { Accessibility.Public };

            foreach (AccessModifier mod in Enum.GetValues(typeof(AccessModifier)))
            {
                if (mod is not AccessModifier.All && (accessModifier & mod) > 0)
                {
                    accessibility.Add(AccessibilityByAccessModifier[mod]);
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
                // Default case should never be hit
                _ => string.Empty,
            };

        public static string ToText(this TypeClassification typeClassification) => typeClassification switch
            {
                TypeClassification.Class => "class",
                TypeClassification.Record => "record",
                _ => string.Empty,
            };
    }
}
