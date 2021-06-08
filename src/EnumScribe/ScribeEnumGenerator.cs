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
    [Generator("C#")]
    internal class ScribeEnumGenerator : ISourceGenerator
    {
        private const string EnumsHintName = "Enums.EnumScribe.g.cs";
        private const string PartialsHintName = "Partials.EnumScribe.g.cs";

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

            (var typeInfos, var enumInfos) = ParseTypeNodes(context, receiver.ClassesSymbolsWithScribeEnumAttribute);

            if (enumInfos.Count > 0)
            {
                var enumsSource = GenerateEnumsFile(enumInfos);
                context.AddSource(EnumsHintName, enumsSource);

                var partialsSource = GeneratePartialsFile(typeInfos);
                context.AddSource(PartialsHintName, partialsSource);
            }
        }

        #region Parsing

        private static (List<TypeInfo>, List<EnumInfo>) ParseTypeNodes(GeneratorExecutionContext context, List<INamedTypeSymbol> scribedTypeSymbols)
        {
            var compilation = context.Compilation;
            var typeInfos = new List<TypeInfo>(scribedTypeSymbols.Count);
            var enumInfos = new List<EnumInfo>();

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
                    // Seen non-partialed type
                    continue;
                }

                if (GetScribeEnumArgumentsFromSymbol(typeSymbol, context, out var suffix, out var includeFields, out var accessibility)
                        == false)
                {
                    // Invalid attribute argument, skip processing
                    continue;
                }

                typeInfo.Suffix = suffix;

                if (typeSymbol.ContainingType is not null
                    && (GetTypeInfoParentageFromSymbol(typeSymbol, typeInfo, typeInfos, context) == false))
                {
                    continue;
                }

                // Get class properties that're enums
                var typeMemberNameList = new HashSet<string>(typeSymbol.GetMembers().Select(x => x.Name));
                var typeEnumPropertySymbols = typeSymbol.GetMembers().OfType<IPropertySymbol>()
                    .Where(x =>
                        accessibility.Contains(x.DeclaredAccessibility) &&
                        x.Type.TypeKind == TypeKind.Enum)
                    .ToList();

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

                    typeInfo.PropertyEnumMap ??= new();
                    typeInfo.PropertyEnumMap.Add((memberSymbol.Name, false, enumInfo!));
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
                var userSuffix = (string)attribute.ConstructorArguments[0].Value!;
                // TODO: Could do with "more than 2 second" regex
                if (Regex.IsMatch(userSuffix, @"[^\s]{1,}") == false)
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
                        var accessModifiers = (AccessModifiers)arg.Value.Value!;
                        accessibility = MapAccessModifiersToAccessibility(accessModifiers);
                        break;
                    // default would be an unaccounted property, should never happen
                }
            }

            return true;

            static HashSet<Accessibility> MapAccessModifiersToAccessibility(AccessModifiers a)
            {
                // TODO: Big map Q_Q, prob internal extension method AccessModifiers instead...
                return new() { Accessibility.Public };
            };
        }

        private static bool GetTypeInfoParentageFromSymbol(INamedTypeSymbol symbol, TypeInfo info, List<TypeInfo> typeInfos, GeneratorExecutionContext context)
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
            parentInfo.NestedTypes = new List<TypeInfo> { info };
            info.ParentType = parentInfo;

            if (parentInfo.IsPartial == false)
            {
                // TODO: Throw error! Type is not partial when it needs to be
                return false;
            }

            return GetTypeInfoParentageFromSymbol(parentSymbol, parentInfo, typeInfos, context);
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
                    // TODO: Warn this attribute does not have a description, therefore it's using the default value (Name)
                    enumInfo.EnumMap.Add((enumSymbol.Name, enumSymbol.Name));
                }
                else if (descriptionAttribute.ConstructorArguments.Length == 0)
                {
                    // TODO: Warn there's an empty description attribute, no text will be displayed
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
            var sb = new StringBuilder();

            // Namespace, class headers
            sb.Append(
@"using System.CodeDom.Compiler;
using System.Runtime.CompilerServices;

namespace EnumScribe.Generated.Enums
{
    [GeneratedCodeAttribute(""ScribeEnumGenerator"", ""0.8.0-alpha"")]
    internal static class EnumDescriptions
    {
");
            foreach (var enumInfo in enumInfos)
            {
                // method header
                sb.Append(
$@"        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string Description(this {enumInfo.FullName} e) => e switch
            {{
");

                foreach (var (name, description) in enumInfo.EnumMap)
                {
                    sb.Append(
$@"                {enumInfo.FullName}.{name} => ""{description}"",
");
                }

                sb.Append(
@"                _ => string.Empty,
");

                // method footer
                sb.Append(
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
            var sb = new StringBuilder();

            // Reduce to base classes, group by namespace
            var typeByNamespace = types
                .Where(x => x.ParentType == default)
                .GroupBy(x => x.Namespace);

            foreach (var groupedInfo in typeByNamespace)
            {
                // TODO: write namespace header

                foreach (var typeInfo in groupedInfo)
                {
                    GenerateTypeText(sb, typeInfo);
                }

                // TODO: write type footer

                // TODO: write namespace footer
            }

            return sb.ToString();

            static void GenerateTypeText(StringBuilder sb, TypeInfo typeInfo)
            {
                // TODO: Handle indentation, # of spaces

                // TODO: write type header
                // TODO: write property map
                // TODO: write field map

                if (typeInfo.NestedTypes is not null)
                {
                    foreach (var nestedType in typeInfo.NestedTypes)
                    {
                        GenerateTypeText(sb, nestedType);
                    }
                }
            }

            /*
             * typeInfos group by namespace, if any ValidTypeToScribe
             * foreach namespace
             * - write namespace header
             * - foreach type where ParentType == default
             * -- write type header
             * -- write property enum map
             * -- write field enum map
             * -- write nested types
             * --- (loop loop loop)
             * -- write type footer
             * - write namespace footer
             */
        }

        #endregion Generating
    }
}
