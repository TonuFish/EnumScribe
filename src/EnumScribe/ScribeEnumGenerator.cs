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
    internal class ScribeEnumGenerator : ISourceGenerator
    {
        private const string EnumsHintName = "Enums.EnumScribe.g.cs";
        private const string PartialsHintName = "Partials.EnumScribe.g.cs";
        private const string PackageVersion = "0.9.0-alpha";
        private const int IndentWidth = 4;

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

            (var typeInfos, var enumInfos) = ParseTypeNodes(context, receiver.ClassSymbolsWithScribeAttribute);

            if (typeInfos.Any(x => x.ShouldScribe))
            {
                var enumsSource = GenerateEnumsSource(enumInfos);
                context.AddSource(EnumsHintName, enumsSource);

                var partialsSource = GeneratePartialsSource(typeInfos);
                context.AddSource(PartialsHintName, partialsSource);
            }
        }

        #region Parsing

        private static (List<TypeInfo>, List<EnumInfo>) ParseTypeNodes(GeneratorExecutionContext context,
            List<INamedTypeSymbol> scribedTypeSymbols)
        {
            var compilation = context.Compilation;
            List<TypeInfo> typeInfos = new(scribedTypeSymbols.Count);
            List<EnumInfo> enumInfos = new();

            foreach (var typeSymbol in scribedTypeSymbols)
            {
                var typeInfo = typeInfos.Find(x => x.FullName == typeSymbol.ToDisplayString());
                if (typeInfo == default)
                {
                    // Unseen type
                    typeInfo = GetTypeInfoFromSymbol(typeSymbol);
                    typeInfos.Add(typeInfo);

                    if (typeInfo.IsPartial == false)
                    {
                        context.ReportDiagnostic(Diagnostic.Create(
                            descriptor: ScribeEnumDiagnostics.ES0002,
                            location: typeSymbol.Locations[0], // TODO: Correct location target
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

                if (GetScribeArgumentsFromSymbol(typeSymbol, context,
                        out var suffix, out var includeFields, out var accessibility)
                        == false)
                {
                    // Invalid attribute argument[s], skip
                    continue;
                }

                typeInfo.Suffix = suffix;

                if (GetTypeInfoLineageFromSymbol(typeSymbol, typeInfo, typeInfos, context) == false)
                {
                    // At least one parent isn't partialed, skip
                    continue;
                }

                // Get enum properties
                var typeEnumPropertySymbols = typeSymbol.GetMembers().OfType<IPropertySymbol>()
                    .Where(x =>
                        accessibility.Contains(x.DeclaredAccessibility)
                        && (
                            x.Type.TypeKind is TypeKind.Enum
                            || (
                                // Nullable<Enum>
                                x.NullableAnnotation is NullableAnnotation.Annotated
                                && x.Type.TypeKind is TypeKind.Struct
                                && ((INamedTypeSymbol)x.Type).TypeArguments
                                    .SingleOrDefault(y => y.TypeKind is TypeKind.Enum) != default
                            )
                        ));

                if (typeEnumPropertySymbols.Any() == false)
                {
                    // No enums in Scribed class, skip
                    context.ReportDiagnostic(Diagnostic.Create(
                        descriptor: ScribeEnumDiagnostics.ES0004,
                        location: typeSymbol.Locations[0], // TODO: Correct location target
                        typeInfo.Name));

                    continue;
                }

                IReadOnlyDictionary<string, ISymbol> typeMemberNameToSymbol = typeSymbol.GetMembers().ToDictionary(x => x.Name);
                var shouldScribe = false;

                foreach (var propertySymbol in typeEnumPropertySymbols)
                {
                    if (propertySymbol.GetAttributes().Any(x => x.AttributeClass!.Name == nameof(NoScribeAttribute)))
                    {
                        // NoScribe attribute present, skip
                        continue;
                    }

                    var isPartial = false;
                    if (typeMemberNameToSymbol.TryGetValue(propertySymbol.Name + typeInfo.Suffix, out var existingSymbol))
                    {
                        if (existingSymbol is IMethodSymbol m && m.IsPartialDefinition)
                        {
                            isPartial = true;
                        }
                        else
                        {
                            context.ReportDiagnostic(Diagnostic.Create(
                                descriptor: ScribeEnumDiagnostics.ES0005,
                                location: propertySymbol.Locations[0],
                                typeInfo.Name));

                            // Naming collision, skip
                            continue;
                        }
                    }

                    var enumFullName = propertySymbol.Type.NullableAnnotation is NullableAnnotation.Annotated
                        ? propertySymbol.Type.ToDisplayString().TrimEnd('?')
                        : propertySymbol.Type.ToDisplayString();

                    var enumInfo = enumInfos.Find(x => x.FullName == enumFullName);
                    if (enumInfo == default)
                    {
                        // Create unseen enum info
                        enumInfo = GetEnumInfoFromSymbol(propertySymbol.Type, context);
                        enumInfos.Add(enumInfo);
                    }

                    typeInfo.PropertyEnumMembers ??= new();
                    typeInfo.PropertyEnumMembers.Add(new()
                    {
                        Accessibility = propertySymbol.DeclaredAccessibility,
                        EnumInfo = enumInfo!,
                        Name = propertySymbol.Name,
                        IsNullable = propertySymbol.NullableAnnotation is NullableAnnotation.Annotated,
                        IsPartial = isPartial,
                        IsStatic = propertySymbol.IsStatic,
                    });

                    shouldScribe = true;
                }

                typeInfo.ShouldScribe = shouldScribe;
            }

            return (typeInfos, enumInfos);
        }

        private static TypeInfo GetTypeInfoFromSymbol(INamedTypeSymbol symbol)
        {
            var classInfo = new TypeInfo
            {
                Accessibility = symbol.DeclaredAccessibility,
                IsStatic = symbol.IsStatic,
                Name = symbol.Name,
                Namespace = symbol.ContainingNamespace.ToDisplayString(),
                Type = symbol.IsRecord ? "record" : "class" // Clunky, change when implementing structs
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

        private static bool GetScribeArgumentsFromSymbol(INamedTypeSymbol symbol, GeneratorExecutionContext context,
            out string suffix, out bool includeFields, out HashSet<Accessibility> accessibility)
        {
            suffix = ScribeAttribute.DefaultSuffix;
            includeFields = false;
            accessibility = new() { Accessibility.Public };

            var attribute = symbol.GetAttributes().First(x => x.AttributeClass!.Name == nameof(ScribeAttribute));
            if (attribute.ConstructorArguments.Length == 1)
            {
                var userSuffix = ((string)attribute.ConstructorArguments[0].Value!);

                if (IsValidMethodSuffix(userSuffix) == false)
                {
                    context.ReportDiagnostic(Diagnostic.Create(
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

        private static bool IsValidMethodSuffix(string suffix)
        {
            for (int ii = 0 ; ii < suffix.Length ; ++ii)
            {
                switch (char.GetUnicodeCategory(suffix, ii))
                {
                    // Cases cover all valid characters in a method name
                    // ref. ECMA-334 5th ed. pg. 20; "7.4.3 Identifiers"
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

        private static bool GetTypeInfoLineageFromSymbol(INamedTypeSymbol symbol, TypeInfo info, List<TypeInfo> typeInfos,
            GeneratorExecutionContext context)
        {
            if (symbol.ContainingType is null)
            {
                return info.IsPartial;
            }

            var parentSymbol = (INamedTypeSymbol)symbol.ContainingSymbol!;
            var parentFullName = parentSymbol.ToDisplayString();
            var parentInfo = typeInfos.Find(x => x.FullName == parentFullName);
            if (parentInfo != default)
            {
                // Parent type already recorded
                parentInfo.NestedTypes ??= new();
                parentInfo.NestedTypes!.Add(info);
                info.ParentType = parentInfo;
                return parentInfo.IsPartial;
            }

            // Parent is an unseen type
            parentInfo = GetTypeInfoFromSymbol(parentSymbol);
            parentInfo.NestedTypes = new() { info };
            typeInfos.Add(parentInfo);
            info.ParentType = parentInfo;

            if (parentInfo.IsPartial == false)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    descriptor: ScribeEnumDiagnostics.ES0003,
                    location: parentSymbol.Locations[0], // TODO: Correct location target
                    parentInfo.Name));
                return false;
            }

            return GetTypeInfoLineageFromSymbol(parentSymbol, parentInfo, typeInfos, context);
        }

        private static EnumInfo GetEnumInfoFromSymbol(ITypeSymbol symbol, GeneratorExecutionContext context)
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
                    context.ReportDiagnostic(Diagnostic.Create(
                        descriptor: ScribeEnumDiagnostics.ES0006,
                        location: enumSymbol.Locations[0]));

                    enumInfo.EnumMap.Add((enumSymbol.Name, enumSymbol.Name));
                }
                else if (descriptionAttribute.ConstructorArguments.Length == 0)
                {
                    // Description attribute present, but no description set. Warn and use empty string.
                    context.ReportDiagnostic(Diagnostic.Create(
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
                        context.ReportDiagnostic(Diagnostic.Create(
                            descriptor: ScribeEnumDiagnostics.ES0008,
                            location: enumSymbol.Locations[0]));

                        enumInfo.EnumMap.Add((enumSymbol.Name, string.Empty));
                    }
                }
            }

            return enumInfo;
        }

        #endregion Parsing

        #region Generating

        private static string GenerateEnumsSource(List<EnumInfo> enumInfos)
        {
            StringBuilder sb = new(550);

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

            foreach (var enumInfo in enumInfos)
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

        private static string GeneratePartialsSource(List<TypeInfo> types)
        {
            StringBuilder sb = new(280);

            // Required generator usings
            sb.AppendLine(
@"#nullable enable

using System.CodeDom.Compiler;
using System.Runtime.CompilerServices;
using EnumScribe.Generated.Enums;
");

            // Reduce to base classes, group by namespace
            var typesByNamespace = types
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
                    .Append(type.Type)
                    .Append(' ')
                    .AppendLine(type.Name)
                    .Append(classIndent)
                    .AppendLine("{");

                if (type.ShouldScribe)
                {
                    var methodIndent = GetIndentation(baseIndentation + 1);

                    if (type.PropertyEnumMembers is not null)
                    {
                        foreach (var property in type.PropertyEnumMembers)
                        {
                            if (property.IsPartial)
                            {
                                WritePartialMemberText(sb, type, property, methodIndent);
                            }
                            else
                            {
                                WriteMemberText(sb, type, property, methodIndent);
                            }
                        }
                    }

                    if (type.FieldEnumMembers is not null)
                    {
                        foreach (var field in type.FieldEnumMembers)
                        {
                            if (field.IsPartial)
                            {
                                WritePartialMemberText(sb, type, field, methodIndent);
                            }
                            else
                            {
                                WriteMemberText(sb, type, field, methodIndent);
                            }
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
                    .Append(member.IsNullable ? "string? " : "string ")
                    .Append(member.Name)
                    .Append(type.Suffix)
                    .Append(" => ")
                    .Append(member.Name)
                    .AppendLine(member.IsNullable ? "?.DescriptionText();" : ".DescriptionText();");
            }

            static void WritePartialMemberText(StringBuilder sb, TypeInfo type, MemberInfo member, string methodIndent)
            {
                sb
                    .Append(methodIndent)
                    .Append(member.Accessibility.ToText())
                    .Append(' ')
                    .Append(StaticText(type.IsStatic))
                    .Append("partial ")
                    .Append(member.IsNullable ? "string? " : "string ")
                    .Append(member.Name)
                    .Append(type.Suffix)
                    .Append("() { return ")
                    .Append(member.Name)
                    .Append(member.IsNullable ? "?.DescriptionText();" : ".DescriptionText();")
                    .AppendLine(" }");
            }
        }

        #endregion Generating
    }
}
