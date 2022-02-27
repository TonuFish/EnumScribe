//#define LAUNCH_DEBUGGER
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Globalization;
using static EnumScribe.Generator.Consts;

namespace EnumScribe.Generator
{
    /// <summary>
    /// Single generator responsible for parsing all relevant syntax nodes, generating source files and emitting
    /// analyzer diagnostics.
    /// </summary>
    [Generator(LanguageNames.CSharp)]
    internal sealed class EnumScribeGenerator : ISourceGenerator
    {
        /// <summary>
        /// The output file name of the enum extensions file.
        /// </summary>
        public const string EnumsHintName = "Enums.EnumScribe.g.cs";

        /// <summary>
        /// The display name of the global namespace.
        /// </summary>
        /// <remarks>
        /// In the absence of having a consistent way to identify the global namespace symbol, match by name.<br/>
        /// Unfortunately <c>Context.Compilation.GlobalNamespace</c> doesn't match a given symbol's
        /// <c>ContainingNamespace</c>.
        /// </remarks>
        public const string GlobalNamespaceName = "<global namespace>";

        /// <summary>
        /// The output file name of the partials file.
        /// </summary>
        public const string PartialsHintName = "Partials.EnumScribe.g.cs";

        /// <summary>
        /// The number of whitespaces (' ') used each indentation level in the generated source code.
        /// </summary>
        private const int IndentWidth = 4;

        /// <summary>
        /// Context associated with the current generator run. Used to access the current compilation, emit analyzer
        /// diagnostics and register additional source files.
        /// </summary>
        private GeneratorExecutionContext _context;

        private readonly List<EnumInfo> _enumInfos = new();
        private readonly List<TypeInfo> _typeInfos = new();

        /// <inheritdoc />
        public void Initialize(GeneratorInitializationContext context)
        {
            context.RegisterForSyntaxNotifications(() => new EnumScribeSyntaxReceiver());
        }

        /// <inheritdoc />
        public void Execute(GeneratorExecutionContext context)
        {
            if (context.SyntaxContextReceiver is not EnumScribeSyntaxReceiver receiver)
            {
                return;
            }

#if LAUNCH_DEBUGGER
            // Debug the generator during a build where the analyzer is referenced (instead of on RoslynComponent run)
            if (System.Diagnostics.Debugger.IsAttached == false)
            {
            System.Diagnostics.Debugger.Launch();
            }
#endif

            _context = context;
            ParseScribedSymbols(receiver.ScribeAttributeSymbols);

            if (_typeInfos.Any(x => x.ShouldScribe))
            {
                // If any type may be scribed (regardless of overall success), create source files
                var enumsSource = GenerateEnumsSource();
                context.AddSource(EnumsHintName, enumsSource);

                var partialsSource = GeneratePartialsSource();
                context.AddSource(PartialsHintName, partialsSource);
            }

            // NoScribe attribute validation
            ValidateNoScribedFieldSymbols(receiver.NoScribeAttributeFieldSymbols);
            ValidateNoScribedPropertySymbols(receiver.NoScribeAttributePropertySymbols);
        }

        #region Parsing

        /// <summary>
        /// Parses scribed types in the current compilation, gathering the required information for source generation
        /// and emitting analyzer diagnostics as necessary.
        /// </summary>
        /// <param name="scribedTypeSymbols">
        /// The type symbols marked with <see cref="ScribeAttribute"/> found in this compilation.
        /// </param>
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
                            descriptor: AnalyzerDiagnostics.ES0002,
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
                        descriptor: AnalyzerDiagnostics.ES1003,
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

        /// <summary>
        /// Gets the source code location of the supplied attribute.
        /// </summary>
        /// <param name="attribute">The attribute to locate.</param>
        /// <returns>The location information of the supplied attribute.</returns>
        private static Location GetAttributeLocation(AttributeData attribute)
            => attribute.ApplicationSyntaxReference!.GetSyntax().GetLocation();

        /// <summary>
        /// Parses the supplied compilation symbol into the generator's internal type representation.
        /// </summary>
        /// <param name="symbol">The compilation symbol to parse.</param>
        /// <returns>The parsed type data object.</returns>
        private static TypeInfo GetTypeInfo(INamedTypeSymbol symbol)
        {
            // Symbol will always be a Type (not Namespace) - Sanity check omitted

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

        /// <summary>
        /// Parses the <see cref="ScribeAttribute"/> arguments from the supplied compilation symbol.
        /// </summary>
        /// <param name="symbol">The compilation symbol to parse.</param>
        /// <param name="attributeLocation">
        /// The location of the <see cref="ScribeAttribute"/> in the compilation source code.
        /// </param>
        /// <param name="suffix">
        /// The nominated <see cref="ScribeAttribute.Suffix"/> if present and valid; otherwise <see cref="Defaults.Suffix"/>.
        /// </param>
        /// <param name="includeFields">
        /// The nominated <see cref="ScribeAttribute.IncludeFields"/>; otherwise <see cref="Defaults.IncludeFields"/>.
        /// </param>
        /// <param name="implementPartialMethods">
        /// The nominated <see cref="ScribeAttribute.ImplementPartialMethods"/>;
        /// otherwise <see cref="Defaults.ImplementPartialMethods"/>.
        /// </param>
        /// <param name="jsonIgnoreNewtonsoft">
        /// The nominated <see cref="ScribeAttribute.JsonIgnore"/>; otherwise <see cref="Defaults.JsonIgnoreNewtonsoft"/>.
        /// </param>
        /// <param name="jsonIgnoreSystem">
        /// The nominated <see cref="ScribeAttribute.JsonIgnore"/>; otherwise <see cref="Defaults.JsonIgnoreSystem"/>.
        /// </param>
        /// <param name="accessibility">
        /// The nominated <see cref="ScribeAttribute.AccessModifiers"/> as <see cref="Accessibility"/>;
        /// otherwise <see cref="Defaults.MutableAccessibility"/>.
        /// </param>
        /// <returns>
        /// <see langword="true"/> if specified <see cref="ScribeAttribute.Suffix"/> is valid or omitted;
        /// otherwise <see langword="false"/>.
        /// </returns>
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
                // Validate user supplied suffix
                var userSuffix = (string?)attribute.ConstructorArguments[0].Value;

                if (userSuffix is null || (IsValidTypeMemberIdentifierSuffix(userSuffix!.AsSpan()) == false))
                {
                    _context.ReportDiagnostic(Diagnostic.Create(
                        descriptor: AnalyzerDiagnostics.ES0001,
                        location: attributeLocation));

                    return false;
                }

                suffix = userSuffix!;
            }

            foreach (var arg in attribute.NamedArguments)
            {
                // Overwrite defaults with user args
                switch (arg.Key)
                {
                    case nameof(ScribeAttribute.ImplementPartialMethods):
                        implementPartialMethods = (bool)arg.Value.Value!;
                        break;
                    case nameof(ScribeAttribute.IncludeFields):
                        includeFields = (bool)arg.Value.Value!;
                        break;
                    case nameof(ScribeAttribute.JsonIgnore):
                        // Checks if the ignore attribute of each supported serializer available
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

        /// <summary>
        /// Validates that the supplied name exists in the current compilation.
        /// </summary>
        /// <param name="fullyQualifiedMetadataName">The fully qualified name to validate.</param>
        /// <returns><see langword="true"/> if the specified name exists; otherwise <see langword="false"/>.</returns>
        private bool IsTypeAvailable(string fullyQualifiedMetadataName)
            => _context.Compilation.GetTypeByMetadataName(fullyQualifiedMetadataName) is not null;

        /// <summary>
        /// Parses the lineage of the supplied compilation symbol up to the global namespace, creating an internal type
        /// object for each type.
        /// </summary>
        /// <remarks>
        /// Parsing short circuits if a non-<see langword="partial"/> parent type is encountered.
        /// </remarks>
        /// <param name="symbol">The compilation symbol represented by <paramref name="info"/></param>
        /// <param name="info">The internal type object associated with <paramref name="symbol"/></param>
        /// <returns>
        /// <see langword="true"/> if all parent types are <see langword="partial"/>; otherwise <see langword="false"/>.
        /// </returns>
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
                    descriptor: AnalyzerDiagnostics.ES0003,
                    location: parentSymbol.Locations[0],
                    parentInfo.Name));
                return false;
            }

            return ProcessTypeLineage(parentSymbol, parentInfo);
        }

        /// <summary>
        /// First pass selection of any <see cref="Enum"/> (or nullable <see cref="Enum"/>) type members meeting the supplied type and
        /// accessibility requirements.
        /// </summary>
        /// <param name="typeSymbol">The compilation symbol to parse.</param>
        /// <param name="includeFields">The switch to include field members.</param>
        /// <param name="accessibility">The access levels to include.</param>
        /// <returns>
        /// Information (Member symbol, member symbol's type, nullability) on each compilation symbol member meeting
        /// the supplied criteria.
        /// </returns>
        private IEnumerable<(ISymbol Symbol, INamedTypeSymbol Type, bool IsNullable)> GetEnumMembers(
            INamedTypeSymbol typeSymbol, bool includeFields, HashSet<Accessibility> accessibility)
        {
            // TODO: This could really do with an efficiency pass...

            // This is a little clunky as the most specific shared interface between IPropertySymbol and IFieldSymbol
            // is ISymbol, which loses access to Type and NullableAnnotation members.

            //! Local func closes over accessibility - Does not increase its' lifetime though
            bool IsEnumMember<T>((T Symbol, INamedTypeSymbol Type, bool IsNullable) member) where T : ISymbol
                => accessibility.Contains(member.Symbol.DeclaredAccessibility)
                    && member.Type is INamedTypeSymbol t && IsMemberSymbolAnEnum(t);

            var typeEnumMemberSymbols = typeSymbol.GetMembers()
                .OfType<IPropertySymbol>()
                .Select(x => ((ISymbol)x, (INamedTypeSymbol)x.Type, x.NullableAnnotation is NullableAnnotation.Annotated))
                .Where(IsEnumMember);

            if (includeFields)
            {
                // TODO: Fix quick and dirty field handling; Concat

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

        /// <summary>
        /// Parses the filtered enum type member data associated with the supplied <paramref name="typeInfo"/>.
        /// </summary>
        /// <remarks>
        /// The process is <b>not</b> short circuited by errors, failed members will be skipped without aborting
        /// processing entirely.
        /// </remarks>
        /// <param name="typeEnumMemberData">The type member data to parse.</param>
        /// <param name="typeMemberNameToSymbols">Mapping of each member identifier name to compilation symbol.</param>
        /// <param name="typeInfo">
        /// The internal type object associated with the compilation type symbol owning the members present in
        /// <paramref name="typeEnumMemberData"/>.
        /// </param>
        /// <returns>
        /// <see langword="true"/> if any member was successfully parsed; otherwise <see langword="false"/>.
        /// </returns>
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
                        && (m.IsGenericMethod == false)
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
                                descriptor: AnalyzerDiagnostics.ES0005,
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
                            descriptor: AnalyzerDiagnostics.ES0004,
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

        /// <summary>
        /// Parses the supplied compilation symbol into the generator's internal enum representation.
        /// </summary>
        /// <param name="symbol">The compilation symbol to parse.</param>
        /// <returns>The parsed enum data object.</returns>
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
                        descriptor: AnalyzerDiagnostics.ES1001,
                        location: enumSymbol.Locations[0],
                        enumSymbol.Name));

                    // Missing DescriptionAttribute, use the member name
                    enumInfo.EnumNameDescriptionPairs.Add((enumSymbol.Name, enumSymbol.Name));
                }
                else if (descriptionAttribute.ConstructorArguments.Length == 0)
                {
                    _context.ReportDiagnostic(Diagnostic.Create(
                        descriptor: AnalyzerDiagnostics.ES1002,
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
                            descriptor: AnalyzerDiagnostics.ES1002,
                            location: enumSymbol.Locations[0],
                            enumSymbol.Name));

                        // DescriptionAttribute present but description is null, use empty string
                        enumInfo.EnumNameDescriptionPairs.Add((enumSymbol.Name, string.Empty));
                    }
                }
            }

            return enumInfo;
        }

        /// <summary>
        /// Parses the generic arguments on the supplied compilation symbol.
        /// </summary>
        /// <param name="symbol">The compilation symbol to parse.</param>
        /// <returns>
        /// The source text representation of the type's generic parameters if present; otherwise <see langword="null"/>.
        /// </returns>
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

        /// <summary>
        /// Generates the enum extensions source code using a syntax factory.
        /// </summary>
        /// <returns>The text of the enum extensions file.</returns>
        private SourceText GenerateEnumsSource() => EnumExtensionsSyntaxFactory.GenerateEnumsSource(_enumInfos, IndentWidth);

        /// <summary>
        /// Generates the partials source code using manual generation.
        /// </summary>
        /// <returns>The text of the partials file.</returns>
        private SourceText GeneratePartialsSource() => PartialsSyntaxGeneration.GeneratePartialsSource(_typeInfos, IndentWidth);

        #endregion Generating

        #region Validation

        /// <summary>
        /// Validates the supplied suffix only contains valid type member identifier characters.
        /// </summary>
        /// <remarks>
        /// This does not account for the maximum length of a the combined identifier, which is currently a maximum of
        /// 512 characters. (Anything longer results in CS0645)
        /// </remarks>
        /// <param name="suffix">The type member identifier suffix to validate.</param>
        /// <returns><see langword="true"/> if the suffix is valid; otherwise <see langword="false"/>.</returns>
        private static bool IsValidTypeMemberIdentifierSuffix(ReadOnlySpan<char> suffix)
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

        /// <summary>
        /// Checks if the supplied symbol is an <see cref="Enum"/>.
        /// </summary>
        /// <param name="symbol">The compilation symbol to examine.</param>
        /// <returns>
        /// <see langword="true"/> if the supplied symbol is an <see cref="Enum"/> or nullable <see cref="Enum"/>;
        /// otherwise <see langword="false"/>.
        /// </returns>
        private static bool IsMemberSymbolAnEnum(INamedTypeSymbol symbol)
            // Enum
            => symbol.TypeKind is TypeKind.Enum
                // Nullable<Enum>
                || (symbol.OriginalDefinition.SpecialType is SpecialType.System_Nullable_T
                    // One type arg is guaranteed by Nullable<T>
                    && symbol.TypeArguments[0].TypeKind is TypeKind.Enum);

        /// <summary>
        /// Checks if the supplied type symbol is marked with the <see cref="ScribeAttribute"/>.
        /// </summary>
        /// <param name="symbol">The compilation symbol to examine.</param>
        /// <returns>
        /// <see langword="true"/> if the supplied symbol is scribed; otherwise <see langword="false"/>.
        /// </returns>
        private static bool IsTypeSymbolScribed(INamedTypeSymbol symbol)
            => symbol.GetAttributes().Any(x => x.AttributeClass?.Name == nameof(ScribeAttribute));

        /// <summary>
        /// Validates all uses of <see cref="NoScribeAttribute"/> on field members, emitting anaylzer diagnostics as
        /// necessary.
        /// </summary>
        /// <remarks>
        /// Behaviour is identical to <see cref="ValidateNoScribedPropertySymbols"/>; however is split into two methods
        /// as <see cref="IFieldSymbol"/> and <see cref="IPropertySymbol"/> don't have a shared ancestor with access to
        /// <see cref="IFieldSymbol.Type"/>.
        /// </remarks>
        /// <param name="noScribeFieldSymbols">The field compilation symbols to validate.</param>
        private void ValidateNoScribedFieldSymbols(List<IFieldSymbol> noScribeFieldSymbols)
        {
            foreach (var fieldSymbol in noScribeFieldSymbols)
            {
                if (IsTypeSymbolScribed(fieldSymbol.ContainingType) == false)
                {
                    var attribute = fieldSymbol.GetAttributes()
                        .First(x => x.AttributeClass?.Name == nameof(NoScribeAttribute));

                    _context.ReportDiagnostic(Diagnostic.Create(
                        descriptor: AnalyzerDiagnostics.ES1004,
                        location: GetAttributeLocation(attribute),
                        fieldSymbol.Name));
                }
                else if (IsMemberSymbolAnEnum((INamedTypeSymbol)fieldSymbol.Type) == false)
                {
                    var attribute = fieldSymbol.GetAttributes()
                        .First(x => x.AttributeClass?.Name == nameof(NoScribeAttribute));

                    _context.ReportDiagnostic(Diagnostic.Create(
                        descriptor: AnalyzerDiagnostics.ES1005,
                        location: GetAttributeLocation(attribute),
                        fieldSymbol.Name));
                }
            }
        }

        /// <summary>
        /// Validates all uses of <see cref="NoScribeAttribute"/> on property members, emitting anaylzer diagnostics as
        /// necessary.
        /// </summary>
        /// <remarks>
        /// Behaviour is identical to <see cref="ValidateNoScribedFieldSymbols"/>; however is split into two methods
        /// as <see cref="IPropertySymbol"/> and <see cref="IFieldSymbol"/> don't have a shared ancestor with access to
        /// <see cref="IPropertySymbol.Type"/>.
        /// </remarks>
        /// <param name="noScribePropertySymbols">The property compilation symbols to validate.</param>
        private void ValidateNoScribedPropertySymbols(List<IPropertySymbol> noScribePropertySymbols)
        {
            foreach (var propertySymbol in noScribePropertySymbols)
            {
                if (IsTypeSymbolScribed(propertySymbol.ContainingType) == false)
                {
                    var attribute = propertySymbol.GetAttributes()
                        .First(x => x.AttributeClass?.Name == nameof(NoScribeAttribute));

                    _context.ReportDiagnostic(Diagnostic.Create(
                        descriptor: AnalyzerDiagnostics.ES1004,
                        location: GetAttributeLocation(attribute),
                        propertySymbol.Name));
                }
                else if (IsMemberSymbolAnEnum((INamedTypeSymbol)propertySymbol.Type) == false)
                {
                    var attribute = propertySymbol.GetAttributes()
                        .First(x => x.AttributeClass?.Name == nameof(NoScribeAttribute));

                    _context.ReportDiagnostic(Diagnostic.Create(
                        descriptor: AnalyzerDiagnostics.ES1005,
                        location: GetAttributeLocation(attribute),
                        propertySymbol.Name));
                }
            }
        }

        #endregion Validation
    }
}
