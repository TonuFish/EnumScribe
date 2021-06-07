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
    [Generator]
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
                    typeInfo = GetTypeInfoFromSymbol(typeSymbol);
                    typeInfos.Add(typeInfo);
                    if (typeInfo.IsPartial == false)
                    {
                        // If class can't be partial-ed, skip processing
                        // TODO: Error, class cannot be partial-ed
                        continue;
                    }
                }
                else if (typeInfo.IsPartial == false)
                {
                    continue;
                }

                var scribeAttribute = typeSymbol.GetAttributes().First(x => x.AttributeClass!.Name == nameof(ScribeEnumAttribute));

                if (scribeAttribute.ConstructorArguments.Length == 1)
                {
                    // Get attribute details
                    var userSuffix = (string)scribeAttribute.ConstructorArguments[0].Value!;
                    // TODO: Could do with a more accurate regex? "Think" it's fine
                    if (Regex.IsMatch(userSuffix, @"[^\s]{1,}") == false)
                    {
                        // TODO: Tidy up the wording on this.
                        // TODO: Diagnostic descriptors can live in a file by themselves, don't need to be bulk in the generator
                        context.ReportDiagnostic(Diagnostic.Create(
                            new DiagnosticDescriptor(
                                "ES0001",
                                "Invalid suffix argument",
                                "Class {0} {1} suffix argument must consist of valid identifier characters.",
                                "EnumScribe.Naming",
                                DiagnosticSeverity.Error,
                                true,
                                helpLinkUri: "https://github.com/TonuFish/EnumScribe/docs/analyzers/ES0001.md"),
                            typeSymbol.Locations.FirstOrDefault(), // TODO: Make sure that location targets the right class location, non-trivial :\
                            typeSymbol.Name, nameof(ScribeEnumAttribute)));

                        continue;
                    }

                    typeInfo.Suffix = userSuffix;
                } // else default suffix is used

                /*
                bool includeFields = false;
                AccessModifiers accessModifiers = AccessModifiers.Public;
                foreach (var arg in scribeAttribute.NamedArguments)
                {
                    switch (arg.Key)
                    {
                        case nameof(ScribeEnumAttribute.IncludeFields):
                            includeFields = (bool)arg.Value.Value!;
                            break;
                        case nameof(ScribeEnumAttribute.AccessModifiers):
                            accessModifiers = (AccessModifiers)arg.Value.Value!;
                            break;
                    }
                }
                */

                if (typeSymbol.ContainingType != null)
                {
                    // annotated class is nested, record parents
                    INamedTypeSymbol parentSymbol;
                    TypeInfo? parentInfo;
                    do
                    {
                        parentSymbol = (INamedTypeSymbol)typeSymbol.ContainingSymbol!;
                        var parentFullName = parentSymbol.ToDisplayString();
                        parentInfo = typeInfos.Find(x => x.FullName == parentFullName);
                        if (parentInfo != default(TypeInfo))
                        {
                            // Parent type already recorded
                            parentInfo.NestedTypes ??= new();
                            parentInfo.NestedTypes!.Add(typeInfo);
                            typeInfo.ParentType = parentInfo;
                            break;
                        }

                        // Parent is an unseen class
                        parentInfo = GetTypeInfoFromSymbol(parentSymbol);
                        parentInfo.NestedTypes = new List<TypeInfo> { typeInfo };
                        typeInfo.ParentType = parentInfo;
                        if (parentInfo.IsPartial == false)
                        {
                            // TODO: Error, enclosing class cannot be partial-ed
                            break;
                        }
                    } while (parentSymbol.ContainingType != null);

                    if (parentInfo.IsPartial == false)
                    {
                        // Break outer loop
                        continue;
                    }
                }

                // Get class properties that're enums
                var typeMemberNameList = typeSymbol.GetMembers().Select(x => x.Name).ToList(); // TODO: Should be a set
                var typeEnumPropertySymbols = typeSymbol.GetMembers().OfType<IPropertySymbol>()
                    .Where(x =>
                        x.DeclaredAccessibility == Accessibility.Public && // TODO: Custom accessibility
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
                    if (typeMemberNameList.Contains(memberSymbol.Name + typeInfo.Suffix))
                    {
                        // If Name+Suffix already exists inside classSymbol.GetMembers(), warn it cannot be scribed
                        // TODO: Warning, cannot generate property as name is already taken
                        continue;
                    }

                    if (memberSymbol.GetAttributes().Any(x => x.AttributeClass!.Name == nameof(IgnoreScribeAttribute)))
                    {
                        // IgnoreScribe attribute present, skip
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
                Type = symbol.IsRecord ? "record" : "class"
            };

            if (((ClassDeclarationSyntax)symbol.DeclaringSyntaxReferences[0].GetSyntax())
                    .Modifiers.Any(x => x.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.PartialKeyword))
                == false)
            {
                // Partial must be delcared on every reference, therefore checking any text is fine
                classInfo.IsPartial = false;
            }

            return classInfo;
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

        private string GenerateEnumsFile(List<EnumInfo> enumInfos)
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

        private string GeneratePartialsFile(List<TypeInfo> types)
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

        #endregion Generatng

        #region DataClasses

        private class TypeInfo
        {
            public Accessibility Accessibility { get; set; }
            public string FullName => $"{Namespace}.{Name}";
            public bool IsPartial { get; set; } = true;
            public bool IsStatic { get; set; }
            public string Name { get; set; } = null!;
            public string Namespace { get; set; } = null!;
            public bool ShouldScribe { get; set; }
            public string Suffix { get; set; } = ScribeEnumAttribute.DefaultSuffix;
            public string Type { get; set; } = null!; // TODO: Change to non-string
            public List<(string PropertyName, bool isNullable, EnumInfo EnumInfo)>? PropertyEnumMap { get; set; }
            public List<(string PropertyName, bool isNullable, EnumInfo EnumInfo)>? FieldEnumMap { get; set; }

            public TypeInfo? ParentType { get; set; }
            public List<TypeInfo>? NestedTypes { get; set; }

            public bool ValidClassToScribe
            {
                get
                {
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

        private class EnumInfo
        {
            public string FullName { get; set; } = null!; // Including namespace
            public string Name { get; set; } = null!;
            public List<(string Name, string Description)> EnumMap { get; set; } = null!;
        }

        #endregion DataClasses
    }
}
