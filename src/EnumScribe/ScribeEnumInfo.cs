using Microsoft.CodeAnalysis;
using System.Collections.Generic;

namespace EnumScribe
{
    internal class TypeInfo
    {
        public Accessibility Accessibility { get; set; }
        public string FullName => $"{Namespace}.{Name}";

        /// <summary>
        /// Defaults to <c>true</c>.
        /// </summary>
        public bool IsPartial { get; set; } = true;

        public bool IsStatic { get; set; }
        public string Name { get; set; } = null!;
        public string Namespace { get; set; } = null!;
        public bool ShouldScribe { get; set; }
        public string Suffix { get; set; } = null!;
        public TypeClassification Type { get; set; }
        public List<MemberInfo>? EnumMembers { get; set; }

        public TypeInfo? ParentType { get; set; }
        public List<TypeInfo>? NestedTypes { get; set; }

        public bool HasFullPartialLineage
        {
            get
            {
                var fullPartialLineage = IsPartial;
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

    /// <summary>
    /// Property currently, TODO.
    /// </summary>
    internal class MemberInfo
    {
        public Accessibility Accessibility { get; set; }
        public EnumInfo EnumInfo { get; set; } = null!;
        public string Name { get; set; } = null!;
        public bool IsNullable { get; set; }
        public bool IsPartial { get; set; }
        public bool IsStatic { get; set; }
    }

    internal class EnumInfo
    {
        public string FullName { get; set; } = null!;
        public string Name { get; set; } = null!;
        public List<(string Name, string Description)> EnumMap { get; set; } = null!;
    }

    internal enum TypeClassification
    {
        Unknown = 0,
        Class = 1,
        Record = 2,
    }
}
