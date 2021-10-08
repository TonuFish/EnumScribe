using System;

namespace EnumScribe
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class ScribeAttribute : Attribute
    {
        private readonly string _suffix;
        internal const string DefaultSuffix = "Description";

        public ScribeAttribute() : this(suffix: DefaultSuffix)
        {
        }

        public ScribeAttribute(string suffix = DefaultSuffix)
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
        Protected = 2,
        Internal = 4,
        ProtectedInternal = 8,
        PrivateProtected = 16,
        All = Public | Private | Protected | Internal | ProtectedInternal | PrivateProtected,
    }

    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
    public sealed class NoScribeAttribute : Attribute
    {
    }
}
