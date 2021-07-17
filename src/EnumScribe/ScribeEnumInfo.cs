using Microsoft.CodeAnalysis;
using System.Collections.Generic;

namespace EnumScribe
{
    internal class TypeInfo
    {
        public Accessibility Accessibility { get; set; }
        public string FullName => $"{Namespace}.{Name}";
        public bool IsPartial { get; set; } = true;
        public bool IsStatic { get; set; }
        public string Name { get; set; } = null!;
        public string Namespace { get; set; } = null!;
        public bool ShouldScribe { get; set; }
        public string Suffix { get; set; } = null!;
        public string Type { get; set; } = null!; // TODO: Change to non-string
        public List<MemberInfo>? PropertyEnumMembers { get; set; }
        public List<MemberInfo>? FieldEnumMembers { get; set; }

        public TypeInfo? ParentType { get; set; }
        public List<TypeInfo>? NestedTypes { get; set; }

        public bool HasFullPartialLineage
        {
            get
            {
                var fullPartialLineage = true;
                var parent = ParentType;
                while (fullPartialLineage && parent != default)
                {
                    fullPartialLineage = parent.IsPartial;
                    parent = parent.ParentType;
                }
                return fullPartialLineage;
            }
        }
    }

    internal class MemberInfo
    {
        public Accessibility Accessibility { get; set; }
        public EnumInfo EnumInfo { get; set; } = null!;
        public string Name { get; set; } = null!;
        public bool IsNullable { get; set; }
        public bool IsStatic { get; set; }
    }

    internal class EnumInfo
    {
        public string FullName { get; set; } = null!;
        public string Name { get; set; } = null!;
        public List<(string Name, string Description)> EnumMap { get; set; } = null!;
    }
}
