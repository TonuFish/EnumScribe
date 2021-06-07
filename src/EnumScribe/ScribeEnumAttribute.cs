using System;

namespace EnumScribe
{
    // Analyzer :: make there is an enum under the class, else warn it can be removed
    // Analyzer :: warn if struct annotation contains protected, as it doesn't apply to structs
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false, Inherited = false)]
    public sealed class ScribeEnumAttribute : Attribute
    {
        internal readonly string _suffix;
        internal const string DefaultSuffix = "Description";

        public ScribeEnumAttribute() : this(suffix: DefaultSuffix)
        {
        }

        public ScribeEnumAttribute(string suffix = DefaultSuffix)
        {
            _suffix = suffix;
        }

        public AccessModifiers AccessModifiers { get; set; } = AccessModifiers.Public;
        public bool IncludeFields { get; set; }
    }

    [Flags]
    public enum AccessModifiers
    {
        Public = 0b0001,
        Private = 0b0010,
        Protected = 0b0100, // WARNING: Anything protected has no impact on struct
        Internal = 0b1000,
        ProtectedInternal = Protected | Internal,
        PrivateProtected = Private | Protected,
    }

    // Analyzer :: make sure this is only applied to an enum, else warn
    // Analyzer :: make sure this is only applied inside class/structs with ScribeEnum annotation, else warn
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
    public sealed class IgnoreScribeAttribute : Attribute
    {
    }
}
