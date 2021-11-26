using Microsoft.CodeAnalysis;
using System;
using System.ComponentModel;

namespace EnumScribe
{
    /// <summary>
    /// Indicates this type should be scribed. This class cannot be inherited.
    /// </summary>
    /// <remarks>
    /// Scribing creates additional methods or get-only properties to access the
    /// <see cref="DescriptionAttribute"/> text of enum members.
    /// </remarks>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class ScribeAttribute : Attribute
    {
        internal const string DefaultSuffix = "Description";

        /// <summary>
        /// Specifies the default value for <see cref="ScribeAttribute"/>, which has the default suffix
        /// ("Description") and scribes public properties only.
        /// </summary>
        public static readonly ScribeAttribute Default = new();

        /// <summary>
        /// A bitwise combination of accessibility modifiers that specify which members may be scribed.
        /// </summary>
        public AccessModifier AccessModifiers { get; set; } = AccessModifier.Public;

        /// <summary>
        /// Indicates that valid partial methods should be implemented, otherwise resulting in a compilation error.
        /// </summary>
        public bool ImplementPartialMethods { get; set; } = TypeInfo.DefaultImplementPartialMethods;

        /// <summary>
        /// Indicates that field members should be scribed.
        /// </summary>
        public bool IncludeFields { get; set; } = TypeInfo.DefaultIncludeFields;

        /// <summary>
        /// Indicates that all scribe generated properties will be declared with <c>JsonIgnore</c> attribute[s].
        /// </summary>
        /// <remarks>
        /// Supported JSON libraries:
        /// <list type="bullet">
        /// <item><description>System.Text.Json</description></item>
        /// <item><description>Json.NET (Newtonsoft)</description></item>
        /// </list>
        /// </remarks>
        public bool JsonIgnore { get; set; } = false;

        /// <summary>
        /// Gets the suffix text used by this instance of the <see cref="ScribeAttribute"/> class.
        /// </summary>
        public string Suffix { get; }

        public ScribeAttribute() : this(DefaultSuffix) { }

        /// <summary>
        /// Initializes a new instance of the <see cref="ScribeAttribute"/> class.
        /// </summary>
        /// <param name="suffix">The suffix text appended to member identifiers.</param>
        public ScribeAttribute(string suffix) => Suffix = suffix;

        public override bool Equals(object? obj) =>
            obj is ScribeAttribute o
            && o.AccessModifiers == AccessModifiers
            && o.ImplementPartialMethods == ImplementPartialMethods
            && o.IncludeFields == IncludeFields
            && o.JsonIgnore == JsonIgnore
            && o.Suffix == Suffix;

        /// <summary>
        /// Hash uses <see cref="Suffix"/> only.
        /// </summary>
        public override int GetHashCode() => Suffix.GetHashCode();

        public override bool IsDefaultAttribute() => Equals(Default);
    }

    /// <summary>
    /// Specifies member accessibility modifiers.
    /// </summary>
    /// <seealso cref="Accessibility"/>
    [Flags]
    public enum AccessModifier
    {
        /// <summary>
        /// <see langword="public"/> accessibility.
        /// </summary>
        /// <seealso cref="Accessibility.Public"/>
        Public = 0,

        /// <summary>
        /// <see langword="private"/> accessibility.
        /// </summary>
        /// <seealso cref="Accessibility.Private"/>
        Private = 1,

        /// <summary>
        /// <see langword="protected"/> accessibility.
        /// </summary>
        /// <seealso cref="Accessibility.Protected"/>
        Protected = 2,

        /// <summary>
        /// <see langword="internal"/> accessibility.
        /// </summary>
        /// <seealso cref="Accessibility.Internal"/>
        Internal = 4,

        /// <summary>
        /// <see langword="protected"/> <see langword="internal"/> accessibility.
        /// </summary>
        /// <seealso cref="Accessibility.ProtectedOrInternal"/>
        ProtectedInternal = 8,

        /// <summary>
        /// <see langword="private"/> <see langword="protected"/> accessibility.
        /// </summary>
        /// <seealso cref="Accessibility.ProtectedAndInternal"/>
        PrivateProtected = 16,

        /// <summary>
        /// Allows all accessibilities.
        /// </summary>
        All = Public | Private | Protected | Internal | ProtectedInternal | PrivateProtected,
    }

    /// <summary>
    /// Prevents this member from being scribed. This class cannot be inherited.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
    public sealed class NoScribeAttribute : Attribute
    {
    }
}
