using Microsoft.CodeAnalysis;
using System;

namespace EnumScribe
{
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
}
