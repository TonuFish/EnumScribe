//#define LAUNCH_DEBUGGER
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Globalization;
using static EnumScribe.Generator.EnumScribeConsts;
using Microsoft.CodeAnalysis.Text;

namespace EnumScribe.Generator
{
    [Generator(LanguageNames.CSharp)]
    internal sealed class EnumScribeGenerator : ISourceGenerator
    {
        public const string EnumsHintName = "Enums.EnumScribe.g.cs";

        /// <summary>
        /// The display name of the global namespace.
        /// </summary>
        /// <remarks>
        /// In the absence of having a consistent way to identify the global namespace symbol, match by name.<br/>
        /// <c>Context.Compilation.GlobalNamespace</c> doesn't match a given symbol <c>ContainingNamespace</c>
        /// </remarks>
        public const string GlobalNamespaceName = "<global namespace>";

        public const string PartialsHintName = "Partials.EnumScribe.g.cs";
        private const int IndentWidth = 4;

        private GeneratorExecutionContext _context;
        private readonly List<EnumInfo> _enumInfos = new();
        private readonly List<TypeInfo> _typeInfos = new();

        public void Initialize(GeneratorInitializationContext context)
        {
            context.RegisterForSyntaxNotifications(() => new EnumScribeSyntaxReceiver());
        }

        public void Execute(GeneratorExecutionContext context)
        {
            if (context.SyntaxContextReceiver is not EnumScribeSyntaxReceiver receiver)
            {
                return;
            }

#if LAUNCH_DEBUGGER
            // Debug the generator during build (instead of RoslynComponent run)
            if (System.Diagnostics.Debugger.IsAttached == false)
            {
            System.Diagnostics.Debugger.Launch();
            }
#endif

            _context = context;
            ParseScribedSymbols(receiver.ScribeAttributeSymbols);

            if (_typeInfos.Any(x => x.ShouldScribe))
            {
                var enumsSource = GenerateEnumsSource();
                context.AddSource(EnumsHintName, enumsSource);

                var partialsSource = GeneratePartialsSource();
                context.AddSource(PartialsHintName, partialsSource);
            }

            ValidateNoScribedFieldSymbols(receiver.NoScribeAttributeFieldSymbols);
            ValidateNoScribedPropertySymbols(receiver.NoScribeAttributePropertySymbols);
        }

        #region Parsing

        private void ParseScribedSymbols(List<INamedTypeSymbol> scribedTypeSymbols)
        {
            _typeInfos.Capacity = scribedTypeSymbols.Count;

            foreach (var typeSymbol in scribedTypeSymbols)
            {
                var typeInfo = _typeInfos.Find(x => x.FullName == typeSymbol.ToDisplayString());
                if (typeInfo is null)
                {
                    // Record unseen type
                    typeInfo = GetTypeInfo(typeSymbol);
                    _typeInfos.Add(typeInfo);

                    if (typeInfo.IsPartial == false)
                    {
                        _context.ReportDiagnostic(Diagnostic.Create(
                            descriptor: EnumScribeDiagnostics.ES0002,
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

                if (GetScribeArguments(typeSymbol, out var attributeLocation,
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
                        descriptor: EnumScribeDiagnostics.ES1003,
                        location: attributeLocation,
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

        private static Location GetAttributeLocation(AttributeData attribute)
            => attribute.ApplicationSyntaxReference!.GetSyntax().GetLocation();

        private static TypeInfo GetTypeInfo(INamedTypeSymbol symbol)
        {
            // Symbol will always be a Type (not Namespace)

            var typeInfo = new TypeInfo
            {
                GenericSignature = GetGenericSignature(symbol),
                Name = symbol.Name,
                Namespace = symbol.ContainingNamespace.ToDisplayString(),
                Type = symbol.TypeKind is TypeKind.Class
                    ? symbol.IsRecord
                        ? Type.Record
                        : Type.Class
                    : symbol.IsRecord
                        ? Type.RecordStruct
                        : Type.Struct
            };

            if (((TypeDeclarationSyntax)symbol.DeclaringSyntaxReferences[0].GetSyntax())
                    .Modifiers.Any(x => x.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.PartialKeyword))
                == false)
            {
                // `partial` keyword must be delcared on every reference, therefore checking any (the first one in
                // this case) is acceptable. Diagnostic error is reported at call site as context differs.
                typeInfo.IsPartial = false;
            }

            return typeInfo;
        }

        private bool GetScribeArguments(INamedTypeSymbol symbol,
            out Location attributeLocation,
            out string suffix,
            out bool includeFields, out bool implementPartialMethods,
            out bool jsonIgnoreNewtonsoft, out bool jsonIgnoreSystem,
            out HashSet<Accessibility> accessibility)
        {
            suffix = Defaults.Suffix;
            includeFields = Defaults.IncludeFields;
            implementPartialMethods = Defaults.ImplementPartialMethods;
            jsonIgnoreNewtonsoft = Defaults.JsonIgnoreNewtonsoft;
            jsonIgnoreSystem = Defaults.JsonIgnoreSystem;
            accessibility = Defaults.MutableAccessibility();

            var attribute = symbol.GetAttributes().First(x => x.AttributeClass?.Name == nameof(ScribeAttribute));
            attributeLocation = GetAttributeLocation(attribute);

            if (attribute.ConstructorArguments.Length == 1)
            {
                var userSuffix = (string?)attribute.ConstructorArguments[0].Value;

                if (userSuffix is null || (IsValidIdentifierSuffix(userSuffix!.AsSpan()) == false))
                {
                    _context.ReportDiagnostic(Diagnostic.Create(
                        descriptor: EnumScribeDiagnostics.ES0001,
                        location: attributeLocation));

                    return false;
                }

                suffix = userSuffix!;
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
                        if (IsTypeAvailable(JsonIgnoreNewtonsoftAttribute)) { jsonIgnoreNewtonsoft = true; }
                        if (IsTypeAvailable(JsonIgnoreSystemAttribute)) { jsonIgnoreSystem = true; }
                        break;
                    case nameof(ScribeAttribute.AccessModifiers):
                        var accessModifiers = (AccessModifier)arg.Value.Value!;
                        accessibility = accessModifiers.ToAccessibility();
                        break;
                }
            }

            return true;
        }

        private bool IsTypeAvailable(string fullyQualifiedMetadataName)
            => _context.Compilation.GetTypeByMetadataName(fullyQualifiedMetadataName) is not null;

        private bool ProcessTypeLineage(INamedTypeSymbol symbol, TypeInfo info)
        {
            if (symbol.ContainingType is null)
            {
                return info.IsPartial;
            }

            var parentSymbol = (INamedTypeSymbol)symbol.ContainingSymbol!;
            var parentFullName = parentSymbol.ToDisplayString();
            var parentInfo = _typeInfos.Find(x => x.FullName == parentFullName);
            if (parentInfo is not null)
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
                    descriptor: EnumScribeDiagnostics.ES0003,
                    location: parentSymbol.Locations[0],
                    parentInfo.Name));
                return false;
            }

            return ProcessTypeLineage(parentSymbol, parentInfo);
        }

        private IEnumerable<(ISymbol Symbol, INamedTypeSymbol Type, bool IsNullable)> GetEnumMembers(
            INamedTypeSymbol typeSymbol, bool includeFields, HashSet<Accessibility> accessibility)
        {
            // This is a little clunky as the most specific shared interface between IPropertySymbol and IFieldSymbol
            // is ISymbol, which loses access to Type and NullableAnnotation members.

            //! Local func closes over accessibility
            bool IsEnumMember<T>((T Symbol, INamedTypeSymbol Type, bool IsNullable) member) where T : ISymbol
                => accessibility.Contains(member.Symbol.DeclaredAccessibility)
                    && member.Type is INamedTypeSymbol t && IsMemberSymbolAnEnum(t);

            var typeEnumMemberSymbols = typeSymbol.GetMembers()
                .OfType<IPropertySymbol>()
                .Select(x => ((ISymbol)x, (INamedTypeSymbol)x.Type, x.NullableAnnotation is NullableAnnotation.Annotated))
                .Where(IsEnumMember);

            if (includeFields)
            {
                typeEnumMemberSymbols = typeSymbol.GetMembers()
                    .OfType<IFieldSymbol>()
                    .Select(x =>
                        (Field: (ISymbol)x, (INamedTypeSymbol)x.Type, x.NullableAnnotation is NullableAnnotation.Annotated))
                    // Auto-property backing fields are implicity declared, ignore
                    .Where(x => x.Field.IsImplicitlyDeclared == false && IsEnumMember(x))
                    .Concat(typeEnumMemberSymbols);
            }

            return typeEnumMemberSymbols;
        }

        private bool ProcessEnumMembers(
            IEnumerable<(ISymbol Symbol, INamedTypeSymbol Type, bool IsNullable)> typeEnumMemberData,
            Dictionary<string, IEnumerable<ISymbol>> typeMemberNameToSymbols,
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
                var isPartialMethod = false;
                var isStatic = memberSymbolData.Symbol.IsStatic;

                if (typeMemberNameToSymbols.TryGetValue(memberNameWithSuffix, out var existingSymbols))
                {
                    // A member with the chosen name already exists; this is only acceptable when it corresponds to a
                    // valid partial method and the user has not opted out of implementing them.

                    // Search for a valid partial method symbol
                    var validSymbol = existingSymbols.FirstOrDefault(x => x is IMethodSymbol m
                        && m.IsPartialDefinition
                        && m.PartialImplementationPart is null
                        && m.IsGenericMethod == false
                        // Method scoping is compatible; only illegal permutation is static method + instance member
                        && (memberSymbolData.Symbol.IsStatic || (m.IsStatic == false))
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
                        && m.ReturnType.Equals(_context.Compilation.GetSpecialType(SpecialType.System_String),
                            SymbolEqualityComparer.Default)
                    );

                    if (validSymbol is not null)
                    {
                        if (typeInfo.ImplementPartialMethods == false)
                        {
                            _context.ReportDiagnostic(Diagnostic.Create(
                                descriptor: EnumScribeDiagnostics.ES0005,
                                location: memberSymbolData.Symbol.Locations[0],
                                memberSymbolData.Symbol.Name, typeInfo.Name));

                            // Valid partial method exists, but partial method implementation has been disabled, skip
                            continue;
                        }

                        // Partial method will be scribed instead of a get-only property
                        accessibility = validSymbol.DeclaredAccessibility;
                        isPartialMethod = true;
                        isStatic = validSymbol.IsStatic;
                    }
                    else
                    {
                        _context.ReportDiagnostic(Diagnostic.Create(
                            descriptor: EnumScribeDiagnostics.ES0004,
                            location: memberSymbolData.Symbol.Locations[0],
                            memberSymbolData.Symbol.Name));

                        // Member name is already in use by at least one other symbol, skip
                        continue;
                    }
                }

                var enumFullName = memberSymbolData.Type.NullableAnnotation is NullableAnnotation.Annotated
                    ? memberSymbolData.Type.ToDisplayString().TrimEnd('?')
                    : memberSymbolData.Type.ToDisplayString();

                var enumInfo = _enumInfos.Find(x => x.FullName == enumFullName);
                if (enumInfo is null)
                {
                    // Record unseen enum
                    enumInfo = GetEnumInfo(memberSymbolData.Type);
                    _enumInfos.Add(enumInfo);
                }

                typeInfo.EnumTypeMembers ??= new();
                typeInfo.EnumTypeMembers.Add(new()
                {
                    Accessibility = accessibility,
                    EnumInfo = enumInfo!,
                    Name = memberSymbolData.Symbol.Name,
                    IsNullable = memberSymbolData.IsNullable,
                    IsPartialMethod = isPartialMethod,
                    IsStatic = isStatic,
                });

                if (isPartialMethod == false)
                {
                    // Record the name of the scribed property to prevent potential collisions
                    typeMemberNameToSymbols.Add(memberNameWithSuffix, new[] { memberSymbolData.Symbol });
                }

                shouldScribe = true;
            }

            // If any member is able to be scribed, true
            return shouldScribe;
        }

        private EnumInfo GetEnumInfo(INamedTypeSymbol symbol)
        {
            if (symbol.OriginalDefinition.SpecialType is SpecialType.System_Nullable_T)
            {
                // Unwrap Nullable<Enum> to Enum
                symbol = (INamedTypeSymbol)symbol.TypeArguments[0];
            }
            var enumInfo = new EnumInfo
            {
                FullName = symbol.ToDisplayString(),
                InGlobalNamespace = symbol.ContainingNamespace.ToDisplayString() == GlobalNamespaceName
            };

            var enumSymbols = symbol.GetMembers().OfType<IFieldSymbol>();
            enumInfo.EnumNameDescriptionPairs = new(enumSymbols.Count());

            foreach (var enumSymbol in enumSymbols)
            {
                // Record each enum member
                var descriptionAttribute = enumSymbol.GetAttributes()
                    .FirstOrDefault(x => x.AttributeClass!.Name == nameof(DescriptionAttribute));
                if (descriptionAttribute is null)
                {
                    _context.ReportDiagnostic(Diagnostic.Create(
                        descriptor: EnumScribeDiagnostics.ES1001,
                        location: enumSymbol.Locations[0],
                        enumSymbol.Name));

                    // Missing DescriptionAttribute, use the member name
                    enumInfo.EnumNameDescriptionPairs.Add((enumSymbol.Name, enumSymbol.Name));
                }
                else if (descriptionAttribute.ConstructorArguments.Length == 0)
                {
                    _context.ReportDiagnostic(Diagnostic.Create(
                        descriptor: EnumScribeDiagnostics.ES1002,
                        location: enumSymbol.Locations[0],
                        enumSymbol.Name));

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
                            descriptor: EnumScribeDiagnostics.ES1002,
                            location: enumSymbol.Locations[0],
                            enumSymbol.Name));

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

        #endregion Parsing

        #region Generating

        private SourceText GenerateEnumsSource() => EnumScribeSyntaxFactory.GenerateEnumsSource(_enumInfos, IndentWidth);

        private SourceText GeneratePartialsSource()
        {
            StringBuilder sb = new(capacity: 320);

            sb.Append(
@"// <autogenerated/>
#nullable enable
using ").Append(ExtensionsNamespace).AppendLine(@";
");

            // Reduce to base types (not nested), group by namespace
            var typesByNamespace = _typeInfos
                .Where(x => x.ParentType is null && x.HasFullPartialLineage)
                .GroupBy(x => x.Namespace);

            foreach (var namespaceGroup in typesByNamespace)
            {
                var baseIndent = 0;

                if (namespaceGroup.Key != GlobalNamespaceName)
                {
                    sb
                        .Append("namespace ")
                        .AppendLine(namespaceGroup.Key)
                        .Append('{');

                    baseIndent = 1;
                }

                foreach (var rootType in namespaceGroup)
                {
                    sb.AppendLine();
                    GenerateTypeText(sb, rootType, baseIndent);
                }

                if (namespaceGroup.Key != GlobalNamespaceName)
                {
                    sb.AppendLine("}");
                }

                sb.AppendLine();
            }

            sb.Length -= Environment.NewLine.Length;

            sb.AppendLine(
"#nullable restore");

            return SourceText.From(sb.ToString(), Encoding.UTF8, SourceHashAlgorithm.Sha256);

            static void GenerateTypeText(StringBuilder sb, TypeInfo type, int typeIndent)
            {
                sb
                    .Append(' ', typeIndent * IndentWidth)
                    .Append("partial ")
                    .Append(type.Type.ToText())
                    .Append(' ')
                    .Append(type.Name)
                    .AppendLine(type.GenericSignature)
                    .Append(' ', typeIndent * IndentWidth)
                    .AppendLine("{");

                if (type.ShouldScribe)
                {
                    var methodIndent = typeIndent + 1;

                    if (type.EnumTypeMembers is not null)
                    {
                        foreach (var member in type.EnumTypeMembers)
                        {
                            if (member.IsPartialMethod == false)
                            {
                                if (type.JsonIgnoreNewtonsoft)
                                {
                                    WriteMemberAttributeText(sb, JsonIgnoreNewtonsoftAttribute, methodIndent);
                                }
                                if (type.JsonIgnoreSystem)
                                {
                                    WriteMemberAttributeText(sb, JsonIgnoreSystemAttribute, methodIndent);
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
                        GenerateTypeText(sb, nestedType, typeIndent + 1);
                    }
                }

                sb.Append(' ', typeIndent * IndentWidth).AppendLine("}");
            }

            static string StaticText(bool isStatic) => isStatic ? "static " : string.Empty;

            static void WriteMemberAttributeText(StringBuilder sb, string attribute, int indent)
            {
                sb
                    .Append(' ', indent * IndentWidth)
                    .Append('[')
                    .Append(attribute)
                    .AppendLine("]");
            }

            static void WriteMemberText(StringBuilder sb, TypeInfo type, MemberInfo member, int indent)
            {
                sb
                    .Append(' ', indent * IndentWidth)
                    .Append(member.Accessibility.ToText())
                    .Append(' ')
                    .Append(StaticText(member.IsStatic))
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

        #region Validation

        private static bool IsValidIdentifierSuffix(ReadOnlySpan<char> suffix)
        {
            for (int ii = 0; ii < suffix.Length; ++ii)
            {
                switch (char.GetUnicodeCategory(suffix[ii]))
                {
                    // Categories cover all legal non-leading characters in a type member identifier
                    // ref. ECMA-334:2017 5th ed. pg. 19; "7.4.3 Identifiers"
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

        private static bool IsMemberSymbolAnEnum(INamedTypeSymbol symbol)
            // Enum
            => symbol.TypeKind is TypeKind.Enum
                // Nullable<Enum>
                || (symbol.OriginalDefinition.SpecialType is SpecialType.System_Nullable_T
                    // One type arg is guaranteed by Nullable<T>
                    && symbol.TypeArguments[0].TypeKind is TypeKind.Enum);

        private static bool IsMemberSymbolInScribedType(INamedTypeSymbol symbol)
            => symbol.GetAttributes().Any(x => x.AttributeClass?.Name == nameof(ScribeAttribute));

        private void ValidateNoScribedFieldSymbols(List<IFieldSymbol> noScribeFieldSymbols)
        {
            foreach (var fieldSymbol in noScribeFieldSymbols)
            {
                if (IsMemberSymbolInScribedType(fieldSymbol.ContainingType) == false)
                {
                    var attribute = fieldSymbol.GetAttributes()
                        .First(x => x.AttributeClass?.Name == nameof(NoScribeAttribute));

                    _context.ReportDiagnostic(Diagnostic.Create(
                        descriptor: EnumScribeDiagnostics.ES1004,
                        location: GetAttributeLocation(attribute),
                        fieldSymbol.Name));
                }
                else if (IsMemberSymbolAnEnum((INamedTypeSymbol)fieldSymbol.Type) == false)
                {
                    var attribute = fieldSymbol.GetAttributes()
                        .First(x => x.AttributeClass?.Name == nameof(NoScribeAttribute));

                    _context.ReportDiagnostic(Diagnostic.Create(
                        descriptor: EnumScribeDiagnostics.ES1005,
                        location: GetAttributeLocation(attribute),
                        fieldSymbol.Name));
                }
            }
        }

        private void ValidateNoScribedPropertySymbols(List<IPropertySymbol> noScribePropertySymbols)
        {
            foreach (var propertySymbol in noScribePropertySymbols)
            {
                if (IsMemberSymbolInScribedType(propertySymbol.ContainingType) == false)
                {
                    var attribute = propertySymbol.GetAttributes()
                        .First(x => x.AttributeClass?.Name == nameof(NoScribeAttribute));

                    _context.ReportDiagnostic(Diagnostic.Create(
                        descriptor: EnumScribeDiagnostics.ES1004,
                        location: GetAttributeLocation(attribute),
                        propertySymbol.Name));
                }
                else if (IsMemberSymbolAnEnum((INamedTypeSymbol)propertySymbol.Type) == false)
                {
                    var attribute = propertySymbol.GetAttributes()
                        .First(x => x.AttributeClass?.Name == nameof(NoScribeAttribute));

                    _context.ReportDiagnostic(Diagnostic.Create(
                        descriptor: EnumScribeDiagnostics.ES1005,
                        location: GetAttributeLocation(attribute),
                        propertySymbol.Name));
                }
            }
        }

        #endregion Validation
    }
}
