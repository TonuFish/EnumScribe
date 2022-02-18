using Microsoft.CodeAnalysis;
using System.Collections.Generic;

namespace EnumScribe.Generator
{
    internal static class Consts
    {
        public const string PackageVersion = "1.1.1";
        public const string ExtensionsNamespace = "EnumScribe.Extensions";
        public const string JsonIgnoreNewtonsoftAttribute = "Newtonsoft.Json.JsonIgnoreAttribute";
        public const string JsonIgnoreSystemAttribute = "System.Text.Json.Serialization.JsonIgnoreAttribute";

        /// <summary>
        /// The default settings used by the <see cref="ScribeAttribute"/>
        /// </summary>
        /// <remarks>
        /// Given the current limitations of source generators, updating a default here will not change the default used
        /// in ScribeAttribute.cs; remember to change it in both places.
        /// </remarks>
        internal static class Defaults
        {
            public const Accessibility Accessibility = Microsoft.CodeAnalysis.Accessibility.Public;
            public const AccessModifier AccessModifier = EnumScribe.AccessModifier.Public;
            public const bool ImplementPartialMethods = true;
            public const bool IncludeFields = false;
            public const bool JsonIgnoreNewtonsoft = false;
            public const bool JsonIgnoreSystem = false;
            public const string Suffix = "Description";

            /// <summary>
            /// Gets the default <see cref="ScribeAttribute"/> accessibilities.
            /// </summary>
            /// <returns>
            /// The default <see cref="EnumScribe.AccessModifier"/> flags translated to
            /// a set of <see cref="Microsoft.CodeAnalysis.Accessibility"/>.
            /// </returns>
            public static HashSet<Accessibility> MutableAccessibility() => AccessModifier.ToAccessibility();
        }
    }
}
