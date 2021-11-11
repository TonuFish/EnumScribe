using Microsoft.CodeAnalysis;
using System.Collections.Generic;

namespace EnumScribe
{
    internal class TypeInfo
    {
        public Accessibility Accessibility { get; set; }
        public string FullName => $"{Namespace}.{Name}{GenericSignature}";
        public string? GenericSignature { get; set; }

        /// <summary>
        /// Defaults to <see langword="true"/>.
        /// </summary>
        public bool IsPartial { get; set; } = true;

        public bool IsStatic { get; set; }
        public string Name { get; set; } = null!;
        public string Namespace { get; set; } = null!;
        public bool ShouldScribe { get; set; }
        public string Suffix { get; set; } = null!;
        public Type Type { get; set; }

        public List<MemberInfo>? EnumMembers { get; set; }

        public TypeInfo? ParentType { get; set; }
        public List<TypeInfo>? NestedTypes { get; set; }

        /// <summary>
        /// <see langword="true"/> if the <see cref="INamedTypeSymbol"/> represented by this <see cref="TypeInfo"/>
        /// and all enclosing classes are <see langword="partial"/>; otherwise <see langword="false"/>.
        /// </summary>
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

    internal class MemberInfo
    {
        public Accessibility Accessibility { get; set; }
        public bool IsNullable { get; set; }
        public bool IsPartialMethod { get; set; }
        public bool IsStatic { get; set; }
        public string Name { get; set; } = null!;

        public EnumInfo EnumInfo { get; set; } = null!;
    }

    internal class EnumInfo
    {
        public string FullName { get; set; } = null!;
        public string Name { get; set; } = null!;

        public List<(string Name, string Description)> EnumNameDescriptionPairs { get; set; } = null!;
    }

    internal enum Type
    {
        Class = 1,
        Record = 2,
    }
}
