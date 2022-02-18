using System.Collections.Generic;

namespace EnumScribe.Generator
{
    /// <summary>
    /// Internal representation of a scribed <see cref="System.Enum"/>.
    /// </summary>
    internal sealed class EnumInfo
    {
        /// <summary>
        /// Fully qualified name.
        /// </summary>
        public string FullName { get; set; } = null!;
        public bool InGlobalNamespace { get; set; }
        public string OutputFullName => InGlobalNamespace ? $"global::{FullName}" : FullName;

        /// <summary>
        /// Pairing of source name and description for each member of the current enum.
        /// </summary>
        /// <remarks>
        /// Description parsing rules found in <see cref="EnumScribeGenerator.GetEnumInfo"/>.
        /// </remarks>
        public List<(string Name, string Description)> EnumNameDescriptionPairs { get; set; } = null!;
    }
}
