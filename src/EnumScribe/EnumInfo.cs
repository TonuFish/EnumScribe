using System.Collections.Generic;

namespace EnumScribe
{
    internal class EnumInfo
    {
        public string FullName { get; set; } = null!;
        public List<(string Name, string Description)> EnumNameDescriptionPairs { get; set; } = null!;
    }
}
