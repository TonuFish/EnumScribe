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

        private readonly List<TypeInfo> _typeInfos = new();
        private readonly List<EnumInfo> _enumInfos = new();
        private GeneratorExecutionContext _context;

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

            _context = context;
            ParseTypeNodes(receiver.ClassSymbolsWithScribeAttribute);

            if (_typeInfos.Any(x => x.ShouldScribe))
            {
                var enumsSource = GenerateEnumsSource();
                context.AddSource(EnumsHintName, enumsSource);

                var partialsSource = GeneratePartialsSource();
                context.AddSource(PartialsHintName, partialsSource);
            }
        }

        #region Parsing

        private void ParseTypeNodes(List<INamedTypeSymbol> scribedTypeSymbols)
        {
            _typeInfos.Capacity = scribedTypeSymbols.Count;

            foreach (var typeSymbol in scribedTypeSymbols)
            {
                var typeInfo = _typeInfos.Find(x => x.FullName == typeSymbol.ToDisplayString());
                if (typeInfo == default)
                {
                    // Unseen type
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
                    // Already seen non-partialed type, skip without logging an error
                    continue;
                }

                if (GetScribeArguments(typeSymbol, out var suffix, out var includeFields, out var accessibility)
                        == false)
                {
                    // Invalid attribute argument[s], skip
                    continue;
                }

                typeInfo.Suffix = suffix;

                if (ProcessTypeLineage(typeSymbol, typeInfo) == false)
                {
                    // At least one parent isn't partialed, skip
                    continue;
                }

                var typeEnumMemberSymbols = GetEnumMembers(typeSymbol, includeFields, accessibility);

                if (typeEnumMemberSymbols.Any() == false)
                {
                    // No enums in Scribed class that meet Scribe conditions, skip
                    _context.ReportDiagnostic(Diagnostic.Create(
                        descriptor: ScribeEnumDiagnostics.ES0004,
                        location: typeSymbol.Locations[0], // TODO: Correct location target
                        typeInfo.Name));

                    continue;
                }

                Dictionary<string, ISymbol> typeMemberNameToSymbol = typeSymbol.GetMembers().ToDictionary(x => x.Name);

                typeInfo.ShouldScribe = ProcessEnumMembers(typeEnumMemberSymbols, typeMemberNameToSymbol, typeInfo);
            }
        }

        private IEnumerable<(ISymbol Symbol, ITypeSymbol Type, bool IsNullable)> GetEnumMembers(
            INamedTypeSymbol typeSymbol,
            bool includeFields,
            HashSet<Accessibility> accessibility)
        {
            // This is a little clunky as the most specific shared interface between IPropertySymbol and IFieldSymbol
            // is ISymbol, which loses access to Type and NullableAnnotation.

            // Local func closes over accessibility
            bool IsEnumMember<T>((T Symbol, ITypeSymbol Type, bool IsNullable) member) where T : ISymbol
                => accessibility.Contains(member.Symbol.DeclaredAccessibility)
                    && (
                        // Enum
                        member.Type.TypeKind is TypeKind.Enum
                        || (
                            // Nullable<Enum>
                            member.IsNullable
                            && member.Type.TypeKind is TypeKind.Struct
                            && ((INamedTypeSymbol)member.Type).TypeArguments
                                .SingleOrDefault(y => y.TypeKind is TypeKind.Enum) != default
                        )
                    );

            var typeEnumMemberSymbols = typeSymbol.GetMembers().OfType<IPropertySymbol>()
                .Select(x => ((ISymbol)x, x.Type, x.NullableAnnotation is NullableAnnotation.Annotated))
                .Where(IsEnumMember);

            if (includeFields)
            {
                typeEnumMemberSymbols = typeSymbol.GetMembers().OfType<IFieldSymbol>()
                    .Select(x => (Field: (ISymbol)x, x.Type, x.NullableAnnotation is NullableAnnotation.Annotated))
                    // Auto-property backing fields are implicity declared, ignore
                    .Where(x => x.Field.IsImplicitlyDeclared == false && IsEnumMember(x))
                    .Concat(typeEnumMemberSymbols);
            }

            return typeEnumMemberSymbols;
        }

        private bool ProcessEnumMembers(
            IEnumerable<(ISymbol Symbol, ITypeSymbol Type, bool IsNullable)> typeEnumMemberData,
            Dictionary<string, ISymbol> typeMemberNameToSymbol,
            TypeInfo typeInfo)
        {
            var shouldScribe = false;

            foreach (var memberSymbolData in typeEnumMemberData)
            {
                if (memberSymbolData.Symbol.GetAttributes().Any(x => x.AttributeClass!.Name == nameof(NoScribeAttribute)))
                {
                    // NoScribe attribute present, skip
                    continue;
                }

                var memberNameWithSuffix = memberSymbolData.Symbol.Name + typeInfo.Suffix;
                var accessibility = memberSymbolData.Symbol.DeclaredAccessibility;
                var isPartial = false;

                if (typeMemberNameToSymbol.TryGetValue(memberNameWithSuffix, out var existingSymbol))
                {
                    if (existingSymbol is IMethodSymbol m)
                    {
                        if (m.IsPartialDefinition)
                        {
                            // No body to partial, all good
                            accessibility = m.DeclaredAccessibility;
                            isPartial = true;
                        }
                        else if (m.PartialImplementationPart is not null)
                        {
                            // Is partial, but an implementation already exists
                            continue;
                        }
                        else
                        {
                            // Isn't partial, rip
                            continue;
                        }
                    }
                    else
                    {
                        _context.ReportDiagnostic(Diagnostic.Create(
                            descriptor: ScribeEnumDiagnostics.ES0005,
                            location: memberSymbolData.Symbol.Locations[0],
                            typeInfo.Name));

                        // Naming collision, skip
                        continue;
                    }
                }

                var enumFullName = memberSymbolData.Type.NullableAnnotation is NullableAnnotation.Annotated
                    ? memberSymbolData.Type.ToDisplayString().TrimEnd('?')
                    : memberSymbolData.Type.ToDisplayString();

                var enumInfo = _enumInfos.Find(x => x.FullName == enumFullName);
                if (enumInfo == default)
                {
                    // Create unseen enum info
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
                    IsPartial = isPartial,
                    IsStatic = memberSymbolData.Symbol.IsStatic,
                });

                if (isPartial == false)
                {
                    typeMemberNameToSymbol.Add(memberNameWithSuffix, memberSymbolData.Symbol);
                }

                shouldScribe = true;
            }

            return shouldScribe;
        }

        private TypeInfo GetTypeInfo(INamedTypeSymbol symbol)
        {
            var classInfo = new TypeInfo
            {
                Accessibility = symbol.DeclaredAccessibility,
                IsStatic = symbol.IsStatic,
                Name = symbol.Name,
                Namespace = symbol.ContainingNamespace.ToDisplayString(),
                Type = symbol.IsRecord ? TypeClassification.Record : TypeClassification.Class
            };

            if (((TypeDeclarationSyntax)symbol.DeclaringSyntaxReferences[0].GetSyntax())
                    .Modifiers.Any(x => x.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.PartialKeyword))
                == false)
            {
                // 'partial' must be delcared at every site, therefore checking any (the first one in this case) is fine
                // Error is reported at call site as conditions differ
                classInfo.IsPartial = false;
            }

            return classInfo;
        }

        private bool GetScribeArguments(INamedTypeSymbol symbol,
            out string suffix, out bool includeFields, out HashSet<Accessibility> accessibility)
        {
            suffix = ScribeAttribute.DefaultSuffix;
            includeFields = false;
            accessibility = new() { Accessibility.Public };

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
                    case nameof(ScribeAttribute.IncludeFields):
                        includeFields = (bool)arg.Value.Value!;
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
            if (parentInfo != default)
            {
                // Parent type already recorded
                parentInfo.NestedTypes ??= new();
                parentInfo.NestedTypes!.Add(info);
                info.ParentType = parentInfo;
                return parentInfo.IsPartial;
            }

            // Parent is an unseen type
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

        private EnumInfo GetEnumInfo(ITypeSymbol symbol)
        {
            var enumInfo = new EnumInfo
            {
                FullName = symbol.ToDisplayString(),
                Name = symbol.Name,
            };

            var enumSymbols = symbol.GetMembers().Where(x => x.Kind == SymbolKind.Field).Cast<IFieldSymbol>().ToList();
            enumInfo.EnumMap = new(enumSymbols.Count);

            foreach (var enumSymbol in enumSymbols)
            {
                var descriptionAttribute = enumSymbol.GetAttributes()
                    .FirstOrDefault(x => x.AttributeClass!.Name == nameof(DescriptionAttribute));
                if (descriptionAttribute == default)
                {
                    // Missing Description attribute. Warn and use the default value.
                    _context.ReportDiagnostic(Diagnostic.Create(
                        descriptor: ScribeEnumDiagnostics.ES0006,
                        location: enumSymbol.Locations[0]));

                    enumInfo.EnumMap.Add((enumSymbol.Name, enumSymbol.Name));
                }
                else if (descriptionAttribute.ConstructorArguments.Length == 0)
                {
                    // Description attribute present, but no description set. Warn and use empty string.
                    _context.ReportDiagnostic(Diagnostic.Create(
                        descriptor: ScribeEnumDiagnostics.ES0007,
                        location: enumSymbol.Locations[0]));

                    enumInfo.EnumMap.Add((enumSymbol.Name, string.Empty));
                }
                else
                {
                    // The only possible argument
                    var constructorArg = descriptionAttribute.ConstructorArguments[0].Value;
                    if (constructorArg is string description)
                    {
                        enumInfo.EnumMap.Add((enumSymbol.Name, description));
                    }
                    else
                    {
                        // Description attribute present, but description is null. Warn and use empty string.
                        _context.ReportDiagnostic(Diagnostic.Create(
                            descriptor: ScribeEnumDiagnostics.ES0008,
                            location: enumSymbol.Locations[0]));

                        enumInfo.EnumMap.Add((enumSymbol.Name, string.Empty));
                    }
                }
            }

            return enumInfo;
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

            // Namespace, class headers
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
                // method header
                sb.Append(
@"        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string DescriptionText(this ").Append(enumInfo.FullName).AppendLine(@" e) => e switch
            {");

                foreach (var (name, description) in enumInfo.EnumMap)
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

                // method footer
                sb.AppendLine("            };");
                sb.AppendLine();
            }

            // Remove trailing newline
            sb.Length -= Environment.NewLine.Length;

            // Namespace, class footers
            sb.AppendLine(
@"    }
}

#nullable restore");

            return sb.ToString();
        }

        private string GeneratePartialsSource()
        {
            StringBuilder sb = new(capacity: 320);

            // Required generator usings
            sb.AppendLine(
@"#nullable enable

using System.CodeDom.Compiler;
using System.Runtime.CompilerServices;
using EnumScribe.Generated.Enums;
");

            // Reduce to base classes, group by namespace
            var typesByNamespace = _typeInfos
                .Where(x => x.ParentType == default && x.HasFullPartialLineage)
                .GroupBy(x => x.Namespace);

            foreach (var namespaceGroup in typesByNamespace)
            {
                // Write namespace header
                sb
                    .Append("namespace ")
                    .AppendLine(namespaceGroup.Key)
                    .Append("{");

                foreach (var rootType in namespaceGroup)
                {
                    sb.AppendLine();
                    GenerateTypeText(sb, rootType, 1);
                }

                // Write namespace footer
                sb.AppendLine("}");
                sb.AppendLine();
            }

            sb.AppendLine(
"#nullable restore");

            return sb.ToString();

            static void GenerateTypeText(StringBuilder sb, TypeInfo type, int baseIndentation)
            {
                var classIndent = GetIndentation(baseIndentation);

                // Write type header
                sb
                    .Append(classIndent)
                    .Append(type.Accessibility.ToText())
                    .Append(' ')
                    .Append(StaticText(type.IsStatic))
                    .Append("partial ")
                    .Append(type.Type.ToText())
                    .Append(' ')
                    .AppendLine(type.Name)
                    .Append(classIndent)
                    .AppendLine("{");

                if (type.ShouldScribe)
                {
                    var methodIndent = GetIndentation(baseIndentation + 1);

                    if (type.EnumMembers is not null)
                    {
                        foreach (var member in type.EnumMembers)
                        {
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

                // Write type footer
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
                    .Append(member.IsPartial ? "partial " : string.Empty)
                    .Append(member.IsNullable ? "string? " : "string ")
                    .Append(member.Name)
                    .Append(type.Suffix)
                    .Append(member.IsPartial ? "() => " : " => ")
                    .Append(member.Name)
                    .AppendLine(member.IsNullable ? "?.DescriptionText();" : ".DescriptionText();");
            }
        }

        #endregion Generating
    }
}
