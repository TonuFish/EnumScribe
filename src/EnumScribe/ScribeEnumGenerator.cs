using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace EnumScribe
{
    [Generator(LanguageNames.CSharp)]
    internal class ScribeEnumGenerator : ISourceGenerator
    {
        private const string EnumsHintName = "Enums.EnumScribe.g.cs";
        private const string PartialsHintName = "Partials.EnumScribe.g.cs";
        private const int IndentWidth = 4;

        public void Initialize(GeneratorInitializationContext context)
        {
            context.RegisterForSyntaxNotifications(() => new ScribeEnumSyntaxReceiver());
        }

        public void Execute(GeneratorExecutionContext context)
        {
#if DEBUG
            if (Debugger.IsAttached == false)
            {
                // Debugger.Launch();
            }
#endif

            if (context.SyntaxContextReceiver is not ScribeEnumSyntaxReceiver receiver)
            {
                return;
            }

            (var typeInfos, var enumInfos) = ParseTypeNodes(context, receiver.ClassSymbolsWithScribeEnumAttribute);

            if (typeInfos.Count > 0)
            {
                var enumsSource = GenerateEnumsFile(enumInfos);
                context.AddSource(EnumsHintName, enumsSource);

                var partialsSource = GeneratePartialsFile(typeInfos);
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
                        // If type can't be partial-ed, skip processing
                        // TODO: Error, type can't be partial-ed
                        continue;
                    }
                }
                else if (typeInfo.IsPartial == false)
                {
                    // Seen non-partialed type, skip
                    continue;
                }

                if (GetScribeEnumArgumentsFromSymbol(typeSymbol, context,
                        out var suffix, out var includeFields, out var accessibility)
                        == false)
                {
                    // Invalid attribute argument[s], skip
                    continue;
                }

                typeInfo.Suffix = suffix;

                if (typeSymbol.ContainingType is not null
                    && (GetTypeInfoLineageFromSymbol(typeSymbol, typeInfo, typeInfos, context) == false))
                {
                    // At least one parent isn't partialed, skip
                    continue;
                }

                // Get class properties that're enums
                HashSet<string> typeMemberNameList = new(typeSymbol.GetMembers().Select(x => x.Name));
                var typeEnumPropertySymbols = typeSymbol.GetMembers().OfType<IPropertySymbol>()
                    .Where(x =>
                        accessibility.Contains(x.DeclaredAccessibility)
                        && x.Type.TypeKind == TypeKind.Enum);

                /* TODO:
                 * Nullable enums != TypeKind.Enum (Struct)
                 *
                 * typeSymbol.GetMembers().OfType<IPropertySymbol>().ToList()[#].Type.TypeKind
                 * = Struct
                 */

                foreach (var memberSymbol in typeEnumPropertySymbols)
                {
                    if (memberSymbol.GetAttributes().Any(x => x.AttributeClass!.Name == nameof(IgnoreScribeAttribute)))
                    {
                        // IgnoreScribe attribute present, skip
                        continue;
                    }

                    if (typeMemberNameList.Contains(memberSymbol.Name + typeInfo.Suffix))
                    {
                        // If Name+Suffix already exists inside classSymbol.GetMembers(), warn it cannot be scribed
                        // TODO: Warning, cannot generate property as name is already taken
                        continue;
                    }

                    var enumFullName = memberSymbol.Type.ToDisplayString(); // NOTE: This is the only call that requires narrowed symbol type, refactor-
                    var enumInfo = enumInfos.Find(x => x.FullName == enumFullName);
                    if (enumInfo == default)
                    {
                        // Create enum info, add to enumInfos
                        enumInfo = GetEnumInfoFromSymbol(memberSymbol.Type);
                        enumInfos.Add(enumInfo);
                    }

                    typeInfo.PropertyEnumMembers ??= new();
                    typeInfo.PropertyEnumMembers.Add(new()
                    {
                        Accessibility = memberSymbol.DeclaredAccessibility,
                        EnumInfo = enumInfo!,
                        Name = memberSymbol.Name,
                        IsNullable = false,
                        IsStatic = memberSymbol.IsStatic,
                    });
                }

                /*
                if (includeFields)
                {
                    var typeEnumFieldSymbols = typeSymbol.GetMembers().OfType<IFieldSymbol>()
                        .Where(x =>
                            x.IsImplicitlyDeclared == false &&
                            x.DeclaredAccessibility == Accessibility.Public && // TODO: Custom accessibility
                            x.Type.TypeKind == TypeKind.Enum)
                        .ToList();

                    // TODO: Nullable enum fields

                    // TODO: Repeat the process of the above, but for fields.
                }
                */

                typeInfo.ShouldScribe = true;
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
                Type = symbol.IsRecord ? "record" : "class" // TODO: Change when implementing structs
            };

            if (((ClassDeclarationSyntax)symbol.DeclaringSyntaxReferences[0].GetSyntax())
                    .Modifiers.Any(x => x.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.PartialKeyword))
                == false)
            {
                // Partial must be delcared on every reference, therefore checking any text is fine
                // Error is reported at call site as conditions differ
                // TODO: CallerMemberNameAttribute + switch?
                classInfo.IsPartial = false;
            }

            return classInfo;
        }

        private static bool GetScribeEnumArgumentsFromSymbol(INamedTypeSymbol symbol, GeneratorExecutionContext context,
            out string suffix, out bool includeFields, out HashSet<Accessibility> accessibility)
        {
            suffix = ScribeEnumAttribute.DefaultSuffix;
            includeFields = false;
            accessibility = new() { Accessibility.Public };

            var attribute = symbol.GetAttributes().First(x => x.AttributeClass!.Name == nameof(ScribeEnumAttribute));
            if (attribute.ConstructorArguments.Length == 1)
            {
                var userSuffix = ((string)attribute.ConstructorArguments[0].Value!).Trim();
                // TODO: Could do with "more than 2 second" regex
                if (Regex.IsMatch(userSuffix, @"[^\s]+") == false)
                {
                    context.ReportDiagnostic(Diagnostic.Create(
                        ScribeEnumDiagnostics.ES0001,
                        symbol.Locations[0], // TODO: Make sure that location targets the right class location, non-trivial :\
                        symbol.Name, nameof(ScribeEnumAttribute)));

                    return false;
                }

                suffix = userSuffix;
            }

            foreach (var arg in attribute.NamedArguments)
            {
                switch (arg.Key)
                {
                    case nameof(ScribeEnumAttribute.IncludeFields):
                        includeFields = (bool)arg.Value.Value!;
                        break;
                    case nameof(ScribeEnumAttribute.AccessModifiers):
                        var accessModifiers = (AccessModifier)arg.Value.Value!;
                        accessibility = accessModifiers.ToAccessibility();
                        break;
                }
            }

            return true;
        }

        private static bool GetTypeInfoLineageFromSymbol(INamedTypeSymbol symbol, TypeInfo info, List<TypeInfo> typeInfos,
            GeneratorExecutionContext context)
        {
            var parentSymbol = (INamedTypeSymbol)symbol.ContainingSymbol!;
            var parentFullName = parentSymbol.ToDisplayString();
            var parentInfo = typeInfos.Find(x => x.FullName == parentFullName);
            if (parentInfo != default(TypeInfo))
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
                // TODO: Throw error! Type is not partial when it needs to be
                return false;
            }

            return GetTypeInfoLineageFromSymbol(parentSymbol, parentInfo, typeInfos, context);
        }

        private static EnumInfo GetEnumInfoFromSymbol(ITypeSymbol symbol)
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
                    // TODO: Warn this attribute doesn't have a description, therefore it's using the default value (Name)
                    enumInfo.EnumMap.Add((enumSymbol.Name, enumSymbol.Name));
                }
                else if (descriptionAttribute.ConstructorArguments.Length == 0)
                {
                    // TODO: Info there's an empty description attribute, no text will be displayed
                    enumInfo.EnumMap.Add((enumSymbol.Name, string.Empty));
                }
                else
                {
                    enumInfo.EnumMap.Add((enumSymbol.Name, (string)descriptionAttribute.ConstructorArguments[0].Value!));
                }
            }

            return enumInfo;
        }

        #endregion Parsing

        #region Generating

        private static string GenerateEnumsFile(List<EnumInfo> enumInfos)
        {
            StringBuilder sb = new();

            // Namespace, class headers
            sb.AppendLine(
@"using System.CodeDom.Compiler;
using System.Runtime.CompilerServices;

namespace EnumScribe.Generated.Enums
{
    [GeneratedCodeAttribute(""ScribeEnumGenerator"", ""0.8.0-alpha"")]
    internal static class EnumDescriptions
    {");

            foreach (var enumInfo in enumInfos)
            {
                // method header
                sb.AppendLine(
$@"        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string DescriptionText(this {enumInfo.FullName} e) => e switch
            {{");

                foreach (var (name, description) in enumInfo.EnumMap)
                {
                    sb.AppendLine(
$@"                {enumInfo.FullName}.{name} => ""{description}"",");
                }

                sb.AppendLine(
@"                _ => string.Empty,");

                // method footer
                sb.AppendLine(
@"            };
");
            }

            // Remove trailing newline
            sb.Length -= Environment.NewLine.Length;

            // Namespace, class footers
            sb.Append(
@"    }
}");

            return sb.ToString();
        }

        private static string GeneratePartialsFile(List<TypeInfo> types)
        {
            StringBuilder sb = new();

            // Required generator usings
            sb.AppendLine(
@"using System.CodeDom.Compiler;
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
                sb.Append(
$@"namespace {namespaceGroup.Key}
{{");

                foreach (var rootType in namespaceGroup)
                {
                    sb.AppendLine();
                    GenerateTypeText(sb, rootType, 1);
                }

                // Write namespace footer
                sb.AppendLine(
@"}
");
            }

            // Remove trailing newline
            sb.Length -= Environment.NewLine.Length;

            return sb.ToString();

            static void GenerateTypeText(StringBuilder sb, TypeInfo type, int baseIndentation)
            {
                var classIndent = GetIndentation(baseIndentation);

                // Write type header
                sb.AppendLine(
$@"{classIndent}{type.Accessibility.ToText()} {StaticText(type.IsStatic)}partial {type.Type} {type.Name}
{classIndent}{{");

                if (type.ShouldScribe)
                {
                    var methodIndent = GetIndentation(baseIndentation + 1);

                    if (type.PropertyEnumMembers is not null)
                    {
                        foreach (var property in type.PropertyEnumMembers)
                        {
                            GenerateMemberText(sb, type, property, methodIndent);
                        }
                    }

                    if (type.FieldEnumMembers is not null)
                    {
                        foreach (var field in type.FieldEnumMembers)
                        {
                            GenerateMemberText(sb, type, field, methodIndent);
                        }
                    }
                }

                if (type.NestedTypes is not null)
                {
                    sb.AppendLine();

                    foreach (var nestedType in type.NestedTypes)
                    {
                        GenerateTypeText(sb, nestedType, ++baseIndentation);
                    }
                }

                // Write type footer
                sb.AppendLine($@"{classIndent}}}");
            }

            static string GetIndentation(int indentationLevel)
                => new(' ', indentationLevel * IndentWidth);

            static string StaticText(bool isStatic)
                => isStatic ? "static " : string.Empty;

            static void GenerateMemberText(StringBuilder sb, TypeInfo type, MemberInfo member, string methodIndent)
            {
                sb
                    .Append(methodIndent)
                    .Append(member.Accessibility.ToText())
                    .Append(' ')
                    .Append(StaticText(type.IsStatic))
                    .Append("string ")
                    .Append(member.Name)
                    .Append(type.Suffix)
                    .Append(" => ")
                    .Append(member.Name)
                    .AppendLine(".DescriptionText();");
            }
        }

        #endregion Generating
    }
}
