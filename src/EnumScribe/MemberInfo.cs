using Microsoft.CodeAnalysis;

namespace EnumScribe
{
    internal class MemberInfo
    {
        public Accessibility Accessibility { get; set; }
        public bool IsNullable { get; set; }
        public bool IsPartialMethod { get; set; }
        public bool IsStatic { get; set; }
        public string Name { get; set; } = null!;

        public EnumInfo EnumInfo { get; set; } = null!;
    }
}
