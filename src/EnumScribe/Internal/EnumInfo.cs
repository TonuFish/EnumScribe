using System.Collections.Generic;

namespace EnumScribe.Internal
{
    internal sealed class EnumInfo
    {
        public string FullName { get; set; } = null!;
        public List<(string Name, string Description)> EnumNameDescriptionPairs { get; set; } = null!;
    }
}
