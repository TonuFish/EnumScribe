using Microsoft.CodeAnalysis;
using System.Collections.Generic;

namespace EnumScribe.Internal
{
    internal static class EnumScribeConsts
    {
        public const string PackageVersion = "1.1.0";
        public const string ExtensionsNamespace = "EnumScribe.Extensions";
        public const string JsonIgnoreNewtonsoftAttribute = "Newtonsoft.Json.JsonIgnoreAttribute";
        public const string JsonIgnoreSystemAttribute = "System.Text.Json.Serialization.JsonIgnoreAttribute";

        internal static class Defaults
        {
            public const Accessibility Accessibility = Microsoft.CodeAnalysis.Accessibility.Public;
            public const AccessModifier AccessModifier = EnumScribe.AccessModifier.Public;
            public const bool ImplementPartialMethods = true;
            public const bool IncludeFields = false;
            public const bool JsonIgnoreNewtonsoft = false;
            public const bool JsonIgnoreSystem = false;
            public const string Suffix = ScribeAttribute.DefaultSuffix;

            public static HashSet<Accessibility> MutableAccessibility() => AccessModifier.ToAccessibility();
        }
    }
}
