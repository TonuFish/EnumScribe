using Microsoft.CodeAnalysis;

namespace EnumScribe
{
    internal static class ScribeEnumDiagnostics
    {
        // TODO: Release tracking analyzer, thanks roslynator!
        // https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

        /// <summary>
        /// Fix invalid suffix argument.
        /// </summary>
        internal static readonly DiagnosticDescriptor ES0001 = new
        (
            id: "ES0001",
            title: "Fix invalid suffix argument",
            messageFormat: "Argument 'suffix' must only contain valid identifier characters",
            category: "EnumScribe.Naming",
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            helpLinkUri: "https://github.com/TonuFish/EnumScribe/docs/analyzers/ES0001.md"
        );

        /// <summary>
        /// Add missing partial modifier on scribed type.
        /// </summary>
        internal static readonly DiagnosticDescriptor ES0002 = new
        (
            id: "ES0002",
            title: "Add missing 'partial' modifier on scribed type",
            messageFormat: "Missing 'partial' modifier on declaration of type '{0}'; type cannot be scribed",
            category: "EnumScribe.Something",
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            helpLinkUri: "https://github.com/TonuFish/EnumScribe/docs/analyzers/ES0002.md"
        );

        /// <summary>
        /// Add missing partial modifier on type enclosing scribed type.
        /// </summary>
        internal static readonly DiagnosticDescriptor ES0003 = new
        (
            id: "ES0003",
            title: "Add missing 'partial' modifier on type enclosing scribed type",
            messageFormat: "Missing 'partial' modifier on declaration of type '{0}'; nested type[s] cannot be scribed",
            category: "EnumScribe.Something",
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            helpLinkUri: "https://github.com/TonuFish/EnumScribe/docs/analyzers/ES0003.md"
        );

        internal static readonly DiagnosticDescriptor ES0004 = new
        (
            id: "ES0004",
            title: "Remove redundant 'Scribe' attribute",
            messageFormat: "Redundant 'Scribe' attribute on type '{0}'; type does not contain any enum properties",
            category: "EnumScribe.Redundancy",
            defaultSeverity: DiagnosticSeverity.Hidden,
            isEnabledByDefault: true,
            helpLinkUri: "https://github.com/TonuFish/EnumScribe/docs/analyzers/ES0004.md"
        );

        internal static readonly DiagnosticDescriptor ES0005 = new
        (
            id: "ES0005",
            title: "Fix 'Scribe' naming collision",
            messageFormat: "Property x could not be generated as it already exists",
            category: "EnumScribe.Something",
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            helpLinkUri: "https://github.com/TonuFish/EnumScribe/docs/analyzers/ES0005.md"
        );

        internal static readonly DiagnosticDescriptor ES0006 = new
        (
            id: "ES0006",
            title: "Scribed enum entry missing description",
            messageFormat: "Scribed enum entry missing description; therefore is using the default value",
            category: "EnumScribe.Something",
            defaultSeverity: DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            helpLinkUri: "https://github.com/TonuFish/EnumScribe/docs/analyzers/ES0006.md"
        );

        internal static readonly DiagnosticDescriptor ES0007 = new
        (
            id: "ES0007",
            title: "TODO",
            messageFormat: "Description attribute present, but no description set. Warn and use empty string.",
            category: "EnumScribe.Something",
            defaultSeverity: DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            helpLinkUri: "https://github.com/TonuFish/EnumScribe/docs/analyzers/ES0007.md"
        );

        internal static readonly DiagnosticDescriptor ES0008 = new
        (
            id: "ES0008",
            title: "TODO",
            messageFormat: "Description attribute present, but description is null. Warn and use empty string.",
            category: "EnumScribe.Something",
            defaultSeverity: DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            helpLinkUri: "https://github.com/TonuFish/EnumScribe/docs/analyzers/ES0008.md"
        );

        // INFO: Remove redundant NoScribe attribute x2 -- (property/field not an enum | enclosing type Non-Scribe)
    }

}
