using Microsoft.CodeAnalysis;
using System.Collections.Generic;

namespace EnumScribe
{
    internal sealed class TypeInfo
    {
        public Accessibility Accessibility { get; set; }
        public string FullName => $"{Namespace}.{Name}{GenericSignature}";
        public string? GenericSignature { get; set; }
        public bool ImplementPartialMethods { get; set; } = true;

        /// <summary>
        /// Defaults to <see langword="true"/>.
        /// </summary>
        public bool IsPartial { get; set; } = true;

        public bool IsStatic { get; set; }
        public bool JsonIgnoreNewtonsoft { get; set; }
        public bool JsonIgnoreSystem { get; set; }
        public string Name { get; set; } = null!;
        public string Namespace { get; set; } = null!;
        public bool ShouldScribe { get; set; }
        public string Suffix { get; set; } = null!;
        public Type Type { get; set; }

        /// <summary>
        /// Property | Field | Method
        /// </summary>
        public List<MemberInfo>? EnumTypeMembers { get; set; }

        public List<TypeInfo>? NestedTypes { get; set; }
        public TypeInfo? ParentType { get; set; }

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

    internal enum Type
    {
        Class = 1,
        Record = 2,
    }
}
