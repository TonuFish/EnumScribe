using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Globalization;

namespace EnumScribe
{
    [Generator(LanguageNames.CSharp)]
    internal sealed class ScribeEnumGenerator : ISourceGenerator
    {
        private const string EnumsHintName = "Enums.EnumScribe.g.cs";
        private const string PartialsHintName = "Partials.EnumScribe.g.cs";
        private const string PackageVersion = "0.9.0-alpha";
        private const int IndentWidth = 4;

        private GeneratorExecutionContext _context;
        private readonly List<EnumInfo> _enumInfos = new();
        private readonly List<TypeInfo> _typeInfos = new();

        public void Initialize(GeneratorInitializationContext context)
        {
            context.RegisterForSyntaxNotifications(() => new ScribeEnumSyntaxReceiver());
        }

        public void Execute(GeneratorExecutionContext context)
        {
            if (context.SyntaxContextReceiver is not ScribeEnumSyntaxReceiver receiver)
            {
                return;
            }

            //! EXTERNAL PROJECT DEBUGGING
            //System.Diagnostics.Debugger.Launch();
            //System.Diagnostics.Debugger.Break();

            _context = context;
            ParseScribedSymbols(receiver.ScribeAttributeSymbols);

            if (_typeInfos.Any(x => x.ShouldScribe))
            {
                var enumsSource = GenerateEnumsSource();
                context.AddSource(EnumsHintName, enumsSource);

                var partialsSource = GeneratePartialsSource();
                context.AddSource(PartialsHintName, partialsSource);
            }
        }

        #region Parsing

        private void ParseScribedSymbols(List<INamedTypeSymbol> scribedTypeSymbols)
        {
            _typeInfos.Capacity = scribedTypeSymbols.Count;

            foreach (var typeSymbol in scribedTypeSymbols)
            {
                var typeInfo = _typeInfos.Find(x => x.FullName == typeSymbol.ToDisplayString());
                if (typeInfo is default(TypeInfo))
                {
                    // Record unseen type
                    typeInfo = GetTypeInfo(typeSymbol);
                    _typeInfos.Add(typeInfo);

                    if (typeInfo.IsPartial == false)
                    {
                        _context.ReportDiagnostic(Diagnostic.Create(
                            descriptor: ScribeEnumDiagnostics.ES0002,
                            location: typeSymbol.Locations[0],
                            typeInfo.Name));

                        // Type can't be partialed, skip
                        continue;
                    }
                }
                else if (typeInfo.IsPartial == false)
                {
                    // Seen non-partialed type, skip
                    continue;
                }

                if (GetScribeArguments(typeSymbol,
                    out var suffix, out var includeFields, out var implementPartialMethods,
                    out var jsonIgnoreNewtonsoft, out var jsonIgnoreSystem, out var accessibility)
                        == false)
                {
                    // Invalid attribute argument[s], skip
                    continue;
                }

                typeInfo.Suffix = suffix;
                typeInfo.ImplementPartialMethods = implementPartialMethods;
                typeInfo.JsonIgnoreNewtonsoft = jsonIgnoreNewtonsoft;
                typeInfo.JsonIgnoreSystem = jsonIgnoreSystem;

                if (ProcessTypeLineage(typeSymbol, typeInfo) == false)
                {
                    // 1+ parent type[s] aren't partial, skip
                    continue;
                }

                var typeEnumMemberSymbols = GetEnumMembers(typeSymbol, includeFields, accessibility);

                if (typeEnumMemberSymbols.Any() == false)
                {
                    _context.ReportDiagnostic(Diagnostic.Create(
                        descriptor: ScribeEnumDiagnostics.ES0004,
                        location: typeSymbol.Locations[0], // TODO: Correct location target
                        typeInfo.Name));

                    // No enums in type that meet Scribe conditions, skip
                    continue;
                }

                var typeMemberNameToSymbols = typeSymbol.GetMembers()
                    .GroupBy(x => x.Name)
                    .ToDictionary(
                        keySelector: x => x.First().Name,
                        elementSelector: x => x.AsEnumerable());

                typeInfo.ShouldScribe = ProcessEnumMembers(typeEnumMemberSymbols, typeMemberNameToSymbols, typeInfo);
            }
        }

        private TypeInfo GetTypeInfo(INamedTypeSymbol symbol)
        {
            var classInfo = new TypeInfo
            {
                Accessibility = symbol.DeclaredAccessibility,
                GenericSignature = GetGenericSignature(symbol),
                IsStatic = symbol.IsStatic,
                Name = symbol.Name,
                Namespace = symbol.ContainingNamespace.ToDisplayString(),
                Type = symbol.IsRecord ? Type.Record : Type.Class
            };

            if (((TypeDeclarationSyntax)symbol.DeclaringSyntaxReferences[0].GetSyntax())
                    .Modifiers.Any(x => x.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.PartialKeyword))
                == false)
            {
                // `partial` keyword must be delcared on every reference, therefore checking any (the first one in
                // this case) is acceptable. Error diagnostic is reported at call site as context differ.
                classInfo.IsPartial = false;
            }

            return classInfo;
        }

        private bool GetScribeArguments(INamedTypeSymbol symbol,
            out string suffix,
            out bool includeFields, out bool implementPartialMethods,
            out bool jsonIgnoreNewtonsoft, out bool jsonIgnoreSystem,
            out HashSet<Accessibility> accessibility)
        {
            suffix = TypeInfo.DefaultSuffix;
            includeFields = TypeInfo.DefaultIncludeFields;
            implementPartialMethods = TypeInfo.DefaultImplementPartialMethods;
            jsonIgnoreNewtonsoft = TypeInfo.DefaultJsonIgnoreNewtonsoft;
            jsonIgnoreSystem = TypeInfo.DefaultJsonIgnoreSystem;
            accessibility = TypeInfo.DefaultAccessibility;

            var attribute = symbol.GetAttributes().First(x => x.AttributeClass!.Name == nameof(ScribeAttribute));
            if (attribute.ConstructorArguments.Length == 1)
            {
                var userSuffix = (string)attribute.ConstructorArguments[0].Value!;

                if (IsValidIdentifierSuffix(userSuffix.AsSpan()) == false)
                {
                    _context.ReportDiagnostic(Diagnostic.Create(
                        descriptor: ScribeEnumDiagnostics.ES0001,
                        location: symbol.Locations[0])); // TODO: Correct location target

                    return false;
                }

                suffix = userSuffix;
            }

            foreach (var arg in attribute.NamedArguments)
            {
                switch (arg.Key)
                {
                    case nameof(ScribeAttribute.ImplementPartialMethods):
                        implementPartialMethods = (bool)arg.Value.Value!;
                        break;

                    case nameof(ScribeAttribute.IncludeFields):
                        includeFields = (bool)arg.Value.Value!;
                        break;

                    case nameof(ScribeAttribute.JsonIgnore):
                        var system = _context.Compilation.GetTypeByMetadataName(TypeInfo.JsonIgnoreSystemAttribute);
                        var newtonsoft = _context.Compilation.GetTypeByMetadataName(TypeInfo.JsonIgnoreNewtonsoftAttribute);
                        if (system is not default(INamedTypeSymbol))
                        {
                            jsonIgnoreSystem = true;
                        }
                        if (newtonsoft is not default(INamedTypeSymbol))
                        {
                            jsonIgnoreNewtonsoft = true;
                        }
                        break;

                    case nameof(ScribeAttribute.AccessModifiers):
                        var accessModifiers = (AccessModifier)arg.Value.Value!;
                        accessibility = accessModifiers.ToAccessibility();
                        break;
                }
            }

            return true;
        }

        private bool ProcessTypeLineage(INamedTypeSymbol symbol, TypeInfo info)
        {
            if (symbol.ContainingType is null)
            {
                return info.IsPartial;
            }

            var parentSymbol = (INamedTypeSymbol)symbol.ContainingSymbol!;
            var parentFullName = parentSymbol.ToDisplayString();
            var parentInfo = _typeInfos.Find(x => x.FullName == parentFullName);
            if (parentInfo is not default(TypeInfo))
            {
                // Parent type already recorded
                parentInfo.NestedTypes ??= new();
                parentInfo.NestedTypes!.Add(info);
                info.ParentType = parentInfo;
                return parentInfo.IsPartial;
            }

            // Record unseen parent type
            parentInfo = GetTypeInfo(parentSymbol);
            parentInfo.NestedTypes = new() { info };
            _typeInfos.Add(parentInfo);
            info.ParentType = parentInfo;

            if (parentInfo.IsPartial == false)
            {
                _context.ReportDiagnostic(Diagnostic.Create(
                    descriptor: ScribeEnumDiagnostics.ES0003,
                    location: parentSymbol.Locations[0], // TODO: Correct location target
                    parentInfo.Name));
                return false;
            }

            return ProcessTypeLineage(parentSymbol, parentInfo);
        }

        private IEnumerable<(ISymbol Symbol, ITypeSymbol Type, bool IsNullable)> GetEnumMembers(
            INamedTypeSymbol typeSymbol,
            bool includeFields,
            HashSet<Accessibility> accessibility)
        {
            // This is a little clunky as the most specific shared interface between IPropertySymbol and IFieldSymbol
            // is ISymbol, which loses access to Type and NullableAnnotation members.

            //! Local func closes over accessibility
            bool IsEnumMember<T>((T Symbol, ITypeSymbol Type, bool IsNullable) member) where T : ISymbol
                => accessibility.Contains(member.Symbol.DeclaredAccessibility)
                    && (
                        member.Type is INamedTypeSymbol t
                        && (
                            // Enum
                            t.TypeKind is TypeKind.Enum
                            || (
                                // Nullable<Enum>
                                t.OriginalDefinition.SpecialType is SpecialType.System_Nullable_T
                                // One type arg is guaranteed by Nullable<T>
                                && t.TypeArguments[0].TypeKind is TypeKind.Enum
                            )
                        )
                    );

            var typeEnumMemberSymbols = typeSymbol.GetMembers()
                .OfType<IPropertySymbol>()
                .Select(x => ((ISymbol)x, x.Type, x.NullableAnnotation is NullableAnnotation.Annotated))
                .Where(IsEnumMember);

            if (includeFields)
            {
                typeEnumMemberSymbols = typeSymbol.GetMembers()
                    .OfType<IFieldSymbol>()
                    .Select(x => (Field: (ISymbol)x, x.Type, x.NullableAnnotation is NullableAnnotation.Annotated))
                    // Auto-property backing fields are implicity declared, ignore
                    .Where(x => x.Field.IsImplicitlyDeclared == false && IsEnumMember(x))
                    .Concat(typeEnumMemberSymbols);
            }

            return typeEnumMemberSymbols;
        }

        private bool ProcessEnumMembers(
            IEnumerable<(ISymbol Symbol, ITypeSymbol Type, bool IsNullable)> typeEnumMemberData,
            Dictionary<string, IEnumerable<ISymbol>> typeMemberNameToSymbols,
            TypeInfo typeInfo)
        {
            var shouldScribe = false;
            var stringSymbol = _context.Compilation.GetSpecialType(SpecialType.System_String);

            foreach (var memberSymbolData in typeEnumMemberData)
            {
                if (memberSymbolData.Symbol.GetAttributes().Any(x => x.AttributeClass!.Name == nameof(NoScribeAttribute)))
                {
                    // NoScribe attribute present, skip
                    continue;
                }

                var memberNameWithSuffix = memberSymbolData.Symbol.Name + typeInfo.Suffix;
                var accessibility = memberSymbolData.Symbol.DeclaredAccessibility;
                var isPartialMethod = false;

                if (typeMemberNameToSymbols.TryGetValue(memberNameWithSuffix, out var existingSymbols))
                {
                    // A member with the chosen name already exists; this is only acceptable when it corresponds to a
                    // valid partial method and the user has not opted out of implementing them.

                    // Search for a valid partial method symbol
                    var validSymbol = existingSymbols.FirstOrDefault(x => x is IMethodSymbol m
                        && m.IsPartialDefinition
                        && m.PartialImplementationPart is null
                        && m.IsGenericMethod == false
                        && m.Parameters.IsEmpty
                        // Method nullability is compatible; method cannot return a notnull string if member is nullable
                        && (
                            // If they match, all good
                            m.ReturnNullableAnnotation == memberSymbolData.Type.NullableAnnotation
                            || (
                                // If they don't match, the only legal nullable permutation is member out of context
                                // + method inside with nullable annotation.
                                memberSymbolData.Type.NullableAnnotation is NullableAnnotation.None
                                && m.ReturnNullableAnnotation is NullableAnnotation.Annotated
                            ))
                        // Method returns a string (nullable context independent)
                        && m.ReturnType.Equals(stringSymbol, SymbolEqualityComparer.Default)
                    );

                    if (validSymbol is not default(ISymbol))
                    {
                        if (typeInfo.ImplementPartialMethods == false)
                        {
                            // TODO: Report diagnostic: Valid partial method exists, but has been deliberately opted out

                            //_context.ReportDiagnostic(Diagnostic.Create(
                            //    descriptor: ScribeEnumDiagnostics.ES0005,
                            //    location: memberSymbolData.Symbol.Locations[0],
                            //    memberSymbolData.Symbol.Name));

                            continue;
                        }

                        // Partial method will be scribed instead of a get-only property
                        accessibility = validSymbol.DeclaredAccessibility;
                        isPartialMethod = true;
                    }
                    else
                    {
                        _context.ReportDiagnostic(Diagnostic.Create(
                            descriptor: ScribeEnumDiagnostics.ES0005,
                            location: memberSymbolData.Symbol.Locations[0],
                            memberSymbolData.Symbol.Name));

                        // Member name already in use by at least one other symbol, skip
                        // Really, this is "can't find the correct sort of method overload OR can't in general"
                        continue;
                    }
                }

                var enumFullName = memberSymbolData.Type.NullableAnnotation is NullableAnnotation.Annotated
                    ? memberSymbolData.Type.ToDisplayString().TrimEnd('?')
                    : memberSymbolData.Type.ToDisplayString();

                var enumInfo = _enumInfos.Find(x => x.FullName == enumFullName);
                if (enumInfo is default(EnumInfo))
                {
                    // Record unseen enum
                    enumInfo = GetEnumInfo(memberSymbolData.Type);
                    _enumInfos.Add(enumInfo);
                }

                typeInfo.EnumMembers ??= new();
                typeInfo.EnumMembers.Add(new()
                {
                    Accessibility = accessibility,
                    EnumInfo = enumInfo!,
                    Name = memberSymbolData.Symbol.Name,
                    IsNullable = memberSymbolData.IsNullable,
                    IsPartialMethod = isPartialMethod,
                    IsStatic = memberSymbolData.Symbol.IsStatic,
                });

                if (isPartialMethod == false)
                {
                    // TODO: Clean up this dodgyness after the IEnumerable swap
                    // Record the name of to-scribe property to prevent potential collisions
                    typeMemberNameToSymbols.Add(memberNameWithSuffix, new[] { memberSymbolData.Symbol });
                }

                shouldScribe = true;
            }

            return shouldScribe;
        }

        private EnumInfo GetEnumInfo(ITypeSymbol symbol)
        {
            var enumInfo = new EnumInfo
            {
                FullName = symbol.ToDisplayString(),
                Name = symbol.Name,
            };

            var enumSymbols = symbol.GetMembers().OfType<IFieldSymbol>();
            enumInfo.EnumNameDescriptionPairs = new(enumSymbols.Count());

            foreach (var enumSymbol in enumSymbols)
            {
                var descriptionAttribute = enumSymbol.GetAttributes()
                    .FirstOrDefault(x => x.AttributeClass!.Name == nameof(DescriptionAttribute));
                if (descriptionAttribute is default(AttributeData))
                {
                    _context.ReportDiagnostic(Diagnostic.Create(
                        descriptor: ScribeEnumDiagnostics.ES0006,
                        location: enumSymbol.Locations[0]));

                    // Missing DescriptionAttribute, use the member name
                    enumInfo.EnumNameDescriptionPairs.Add((enumSymbol.Name, enumSymbol.Name));
                }
                else if (descriptionAttribute.ConstructorArguments.Length == 0)
                {
                    _context.ReportDiagnostic(Diagnostic.Create(
                        descriptor: ScribeEnumDiagnostics.ES0007,
                        location: enumSymbol.Locations[0]));

                    // DescriptionAttribute present but no description set, use empty string
                    enumInfo.EnumNameDescriptionPairs.Add((enumSymbol.Name, string.Empty));
                }
                else
                {
                    // DescriptionAttribute only has one constructor argument
                    var constructorArg = descriptionAttribute.ConstructorArguments[0].Value;
                    if (constructorArg is string description)
                    {
                        enumInfo.EnumNameDescriptionPairs.Add((enumSymbol.Name, description));
                    }
                    else
                    {
                        _context.ReportDiagnostic(Diagnostic.Create(
                            descriptor: ScribeEnumDiagnostics.ES0008,
                            location: enumSymbol.Locations[0]));

                        // DescriptionAttribute present but description is null, use empty string
                        enumInfo.EnumNameDescriptionPairs.Add((enumSymbol.Name, string.Empty));
                    }
                }
            }

            return enumInfo;
        }

        private static string? GetGenericSignature(INamedTypeSymbol symbol)
        {
            if (symbol.IsGenericType == false) { return null; }

            const string GenericSeparator = ", ";

            StringBuilder sb = new(8);
            sb.Append('<');

            foreach (var p in symbol.TypeParameters)
            {
                sb.Append(p.Name);
                sb.Append(GenericSeparator);
            }

            // Remove trailing separator
            sb.Length -= GenericSeparator.Length;
            sb.Append('>');

            return sb.ToString();
        }

        private static bool IsValidIdentifierSuffix(ReadOnlySpan<char> suffix)
        {
            for (int ii = 0; ii < suffix.Length; ++ii)
            {
                switch (char.GetUnicodeCategory(suffix[ii]))
                {
                    // Cases cover all valid characters in a method name
                    // ref. ECMA-334 5th ed. pg. 19; "7.4.3 Identifiers"
                    case UnicodeCategory.ConnectorPunctuation:
                    case UnicodeCategory.DecimalDigitNumber:
                    case UnicodeCategory.Format:
                    case UnicodeCategory.LetterNumber:
                    case UnicodeCategory.LowercaseLetter:
                    case UnicodeCategory.ModifierLetter:
                    case UnicodeCategory.NonSpacingMark:
                    case UnicodeCategory.OtherLetter:
                    case UnicodeCategory.SpacingCombiningMark:
                    case UnicodeCategory.TitlecaseLetter:
                    case UnicodeCategory.UppercaseLetter:
                        break;
                    default:
                        return false;
                }
            }

            return true;
        }

        #endregion Parsing

        #region Generating

        private string GenerateEnumsSource()
        {
            StringBuilder sb = new(capacity: 688);

            sb.Append(
@"#nullable enable

using System.CodeDom.Compiler;
using System.Runtime.CompilerServices;

namespace EnumScribe.Generated.Enums
{
    [GeneratedCodeAttribute(""ScribeEnumGenerator"", """).Append(PackageVersion).AppendLine(@""")]
    internal static class EnumDescriptions
    {");

            foreach (var enumInfo in _enumInfos)
            {
                sb.Append(
@"        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string DescriptionText(this ").Append(enumInfo.FullName).AppendLine(@" e) => e switch
            {");

                foreach (var (name, description) in enumInfo.EnumNameDescriptionPairs)
                {
                    sb
                        .Append("                ")
                        .Append(enumInfo.FullName)
                        .Append('.')
                        .Append(name)
                        .Append(@" => """)
                        .Append(description)
                        .AppendLine(@""",");
                }

                sb.AppendLine("                _ => string.Empty,");

                sb.AppendLine("            };");
                sb.AppendLine();
            }

            // Remove trailing newline from method iteration
            sb.Length -= Environment.NewLine.Length;

            sb.AppendLine(
@"    }
}

#nullable restore");

            return sb.ToString();
        }

        private string GeneratePartialsSource()
        {
            StringBuilder sb = new(capacity: 320);

            sb.AppendLine(
@"#nullable enable

using System.CodeDom.Compiler;
using System.Runtime.CompilerServices;
using EnumScribe.Generated.Enums;
");

            // Reduce to base types (not nested), group by namespace
            var typesByNamespace = _typeInfos
                .Where(x => x.ParentType is default(TypeInfo) && x.HasFullPartialLineage)
                .GroupBy(x => x.Namespace);

            foreach (var namespaceGroup in typesByNamespace)
            {
                sb
                    .Append("namespace ")
                    .AppendLine(namespaceGroup.Key)
                    .Append("{");

                foreach (var rootType in namespaceGroup)
                {
                    sb.AppendLine();
                    GenerateTypeText(sb, rootType, 1);
                }

                sb.AppendLine("}");
                sb.AppendLine();
            }

            sb.AppendLine(
"#nullable restore");

            return sb.ToString();

            static void GenerateTypeText(StringBuilder sb, TypeInfo type, int baseIndentation)
            {
                var classIndent = GetIndentation(baseIndentation);

                sb
                    .Append(classIndent)
                    .Append(type.Accessibility.ToText())
                    .Append(' ')
                    .Append(StaticText(type.IsStatic))
                    .Append("partial ")
                    .Append(type.Type.ToText())
                    .Append(' ')
                    .Append(type.Name)
                    .AppendLine(type.GenericSignature)
                    .Append(classIndent)
                    .AppendLine("{");

                if (type.ShouldScribe)
                {
                    var methodIndent = GetIndentation(baseIndentation + 1);

                    if (type.EnumMembers is not null)
                    {
                        foreach (var member in type.EnumMembers)
                        {
                            if (member.IsPartialMethod == false)
                            {
                                if (type.JsonIgnoreNewtonsoft)
                                {
                                    sb
                                        .Append(methodIndent)
                                        .Append('[')
                                        .Append(TypeInfo.JsonIgnoreNewtonsoftAttribute)
                                        .AppendLine("]");
                                }
                                if (type.JsonIgnoreSystem)
                                {
                                    sb
                                        .Append(methodIndent)
                                        .Append('[')
                                        .Append(TypeInfo.JsonIgnoreSystemAttribute)
                                        .AppendLine("]");
                                }
                            }
                            WriteMemberText(sb, type, member, methodIndent);
                        }
                    }

                    if (type.NestedTypes is not null)
                    {
                        sb.AppendLine();
                    }
                }

                if (type.NestedTypes is not null)
                {
                    foreach (var nestedType in type.NestedTypes)
                    {
                        GenerateTypeText(sb, nestedType, ++baseIndentation);
                    }
                }

                sb.Append(classIndent).AppendLine("}");
            }

            static string GetIndentation(int indentationLevel) => new(' ', indentationLevel * IndentWidth);

            static string StaticText(bool isStatic) => isStatic ? "static " : string.Empty;

            static void WriteMemberText(StringBuilder sb, TypeInfo type, MemberInfo member, string methodIndent)
            {
                sb
                    .Append(methodIndent)
                    .Append(member.Accessibility.ToText())
                    .Append(' ')
                    .Append(StaticText(type.IsStatic))
                    .Append(member.IsPartialMethod ? "partial " : string.Empty)
                    .Append(member.IsNullable ? "string? " : "string ")
                    .Append(member.Name)
                    .Append(type.Suffix)
                    .Append(member.IsPartialMethod ? "() => " : " => ")
                    .Append(member.Name)
                    .AppendLine(member.IsNullable ? "?.DescriptionText();" : ".DescriptionText();");
            }
        }

        #endregion Generating
    }
}
