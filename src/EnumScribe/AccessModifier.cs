namespace EnumScribe
{
    /// <summary>
    /// Specifies member accessibility modifiers.
    /// </summary>
    [System.Flags]
    public enum AccessModifier
    {
        /// <summary>
        /// <see langword="public"/> accessibility.
        /// </summary>
        Public = 0,

        /// <summary>
        /// <see langword="private"/> accessibility.
        /// </summary>
        Private = 1,

        /// <summary>
        /// <see langword="protected"/> accessibility.
        /// </summary>
        Protected = 2,

        /// <summary>
        /// <see langword="internal"/> accessibility.
        /// </summary>
        Internal = 4,

        /// <summary>
        /// <see langword="protected"/> <see langword="internal"/> accessibility.
        /// </summary>
        ProtectedInternal = 8,

        /// <summary>
        /// <see langword="private"/> <see langword="protected"/> accessibility.
        /// </summary>
        PrivateProtected = 16,

        /// <summary>
        /// Allows all accessibilities.
        /// </summary>
        All = Public | Private | Protected | Internal | ProtectedInternal | PrivateProtected,
    }
}
