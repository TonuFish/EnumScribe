using System.Collections.Generic;

namespace EnumScribe.Internal
{
    internal sealed class EnumInfo
    {
        /// <summary>
        /// Fully qualified name.
        /// </summary>
        public string FullName { get; set; } = null!;
        public bool InGlobalNamespace { get; set; }
        public string OutputFullName => InGlobalNamespace ? $"global::{FullName}" : FullName;

        public List<(string Name, string Description)> EnumNameDescriptionPairs { get; set; } = null!;
    }
}
