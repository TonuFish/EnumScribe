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
        public string Suffix { get; set; } = string.Empty;
        public string Type { get; set; } = null!; // TODO: Change to non-string
        public List<(string PropertyName, bool isNullable, EnumInfo EnumInfo)>? PropertyEnumMap { get; set; }
        public List<(string PropertyName, bool isNullable, EnumInfo EnumInfo)>? FieldEnumMap { get; set; }

        public TypeInfo? ParentType { get; set; }
        public List<TypeInfo>? NestedTypes { get; set; }

        public bool ValidClassToScribe
        {
            get
            {
                // TODO: This won't work, need more thought
                var shouldScribe = ShouldScribe;
                var parent = ParentType;
                while (shouldScribe && parent != default)
                {
                    shouldScribe = parent.IsPartial;
                    parent = parent.ParentType;
                }
                return shouldScribe;
            }
        }
    }

    internal class EnumInfo
    {
        public string FullName { get; set; } = null!; // Including namespace
        public string Name { get; set; } = null!;
        public List<(string Name, string Description)> EnumMap { get; set; } = null!;
    }
}
