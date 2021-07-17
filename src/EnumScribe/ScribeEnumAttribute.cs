using System;

namespace EnumScribe
{
    // Analyzer :: make there is an enum under the class, else warn it can be removed
    // Analyzer :: warn if struct annotation contains protected, as it doesn't apply to structs
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false, Inherited = false)]
    public sealed class ScribeEnumAttribute : Attribute
    {
        private readonly string _suffix;
        internal const string DefaultSuffix = "Description";

        public ScribeEnumAttribute() : this(suffix: DefaultSuffix)
        {
        }

        public ScribeEnumAttribute(string suffix = DefaultSuffix)
        {
            _suffix = suffix;
        }

        public AccessModifier AccessModifiers { get; set; } = AccessModifier.Public;
        public bool IncludeFields { get; set; } = false;
    }

    [Flags]
    public enum AccessModifier
    {
        Public = 0,
        Private = 1,
        Protected = 2, // INFO: Anything protected has no impact on struct
        Internal = 4,
        ProtectedInternal = 8, // INFO: Anything protected has no impact on struct
        PrivateProtected = 16, // INFO: Anything protected has no impact on struct
        All = Public | Private | Protected | Internal | ProtectedInternal | PrivateProtected,
    }

    // Analyzer :: make sure this is only applied to an enum, else warn
    // Analyzer :: make sure this is only applied inside class/structs with ScribeEnum annotation, else warn
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
    public sealed class IgnoreScribeAttribute : Attribute
    {
    }
}
