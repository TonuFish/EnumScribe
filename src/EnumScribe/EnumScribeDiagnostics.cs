using Microsoft.CodeAnalysis;
using static Microsoft.CodeAnalysis.DiagnosticSeverity;

namespace EnumScribe
{
    internal static class EnumScribeDiagnostics
    {
        /// <summary>
        /// Fix invalid suffix argument.<br />
        /// <b>LOCATION:</b> Scribe attribute declaration<br />
        /// <b>ARGS:</b> Type identifier
        /// </summary>
        internal static readonly DiagnosticDescriptor ES0001 = new
        (
            id: "ES0001",
            title: "Fix invalid 'suffix' argument",
            messageFormat: "Argument 'suffix' must only contain valid identifier characters",
            category: "EnumScribe.Naming",
            defaultSeverity: Error,
            isEnabledByDefault: true,
            helpLinkUri: "https://github.com/TonuFish/EnumScribe/blob/master/docs/analyzers/ES0001.md",
            customTags: WellKnownDiagnosticTags.NotConfigurable
        );

        /// <summary>
        /// Add missing partial modifier on scribed type.<br />
        /// <b>LOCATION:</b> Type declaration<br />
        /// <b>ARGS:</b> Type identifier
        /// </summary>
        internal static readonly DiagnosticDescriptor ES0002 = new
        (
            id: "ES0002",
            title: "Add missing 'partial' modifier on scribed type",
            messageFormat: "Missing 'partial' modifier on declaration of type '{0}'; type cannot be scribed",
            category: "EnumScribe.Usage",
            defaultSeverity: Error,
            isEnabledByDefault: true,
            helpLinkUri: "https://github.com/TonuFish/EnumScribe/blob/master/docs/analyzers/ES0002.md",
            customTags: WellKnownDiagnosticTags.NotConfigurable
        );

        /// <summary>
        /// Add missing partial modifier on type enclosing scribed type.<br />
        /// <b>LOCATION:</b> Type declaration<br />
        /// <b>ARGS:</b> Type identifier
        /// </summary>
        internal static readonly DiagnosticDescriptor ES0003 = new
        (
            id: "ES0003",
            title: "Add missing 'partial' modifier on type enclosing scribed type",
            messageFormat: "Missing 'partial' modifier on declaration of type '{0}'; nested type[s] cannot be scribed",
            category: "EnumScribe.Usage",
            defaultSeverity: Error,
            isEnabledByDefault: true,
            helpLinkUri: "https://github.com/TonuFish/EnumScribe/blob/master/docs/analyzers/ES0003.md",
            customTags: WellKnownDiagnosticTags.NotConfigurable
        );

        /// <summary>
        /// Fix scribed member naming collision.<br />
        /// <b>LOCATION:</b> Type member<br />
        /// <b>ARGS:</b> Member identifier
        /// </summary>
        internal static readonly DiagnosticDescriptor ES0004 = new
        (
            id: "ES0004",
            title: "Fix scribed member naming collision",
            messageFormat: "Member '{0}' could not be scribed as the nominated identifier is already in use",
            category: "EnumScribe.Naming",
            defaultSeverity: Error,
            isEnabledByDefault: true,
            helpLinkUri: "https://github.com/TonuFish/EnumScribe/blob/master/docs/analyzers/ES0004.md",
            customTags: WellKnownDiagnosticTags.NotConfigurable
        );

        /// <summary>
        /// Fix scribed member partial method collision.<br />
        /// <b>LOCATION:</b> Type member<br />
        /// <b>ARGS:</b> Member identifier, Type identifier
        /// </summary>
        internal static readonly DiagnosticDescriptor ES0005 = new
        (
            id: "ES0005",
            title: "Fix scribed member partial method collision",
            messageFormat: "Member '{0}' could not be scribed as 'ImplementPartialMethods' is false on type '{1}'",
            category: "EnumScribe.Usage",
            defaultSeverity: Error,
            isEnabledByDefault: true,
            helpLinkUri: "https://github.com/TonuFish/EnumScribe/blob/master/docs/analyzers/ES0005.md",
            customTags: WellKnownDiagnosticTags.NotConfigurable
        );

        /// <summary>
        /// Add missing Description attribute on scribed enum.<br />
        /// <b>LOCATION:</b> Enum member<br />
        /// <b>ARGS:</b> Enum field identifier
        /// </summary>
        internal static readonly DiagnosticDescriptor ES1001 = new
        (
            id: "ES1001",
            title: "Add missing 'Description' attribute on scribed enum",
            messageFormat: "Missing 'Description' attribute on declaration of enum member '{0}'; identifier text has been scribed",
            category: "EnumScribe.Usage",
            defaultSeverity: Info,
            isEnabledByDefault: true,
            helpLinkUri: "https://github.com/TonuFish/EnumScribe/blob/master/docs/analyzers/ES1001.md"
        );

        /// <summary>
        /// Update Description attribute with a valid description.<br />
        /// <b>LOCATION:</b> Enum member<br />
        /// <b>ARGS:</b> Enum field identifier
        /// </summary>
        internal static readonly DiagnosticDescriptor ES1002 = new
        (
            id: "ES1002",
            title: "Update 'Description' attribute with a valid description",
            messageFormat: "'Description' attribute on declaration of enum member '{0}' is missing 'description'; empty string has been scribed",
            category: "EnumScribe.Usage",
            defaultSeverity: Info,
            isEnabledByDefault: true,
            helpLinkUri: "https://github.com/TonuFish/EnumScribe/blob/master/docs/analyzers/ES1002.md"
        );

        /// <summary>
        /// Remove redundant scribe attribute.<br />
        /// <b>LOCATION:</b> Scribe attribute declaration<br />
        /// <b>ARGS:</b> Type identifier
        /// </summary>
        internal static readonly DiagnosticDescriptor ES1003 = new
        (
            id: "ES1003",
            title: "Remove redundant 'Scribe' attribute",
            messageFormat: "Redundant 'Scribe' attribute on type '{0}'; type does not contain any accessible enum members",
            category: "EnumScribe.Redundancy",
            defaultSeverity: Warning,
            isEnabledByDefault: true,
            helpLinkUri: "https://github.com/TonuFish/EnumScribe/blob/master/docs/analyzers/ES1003.md",
            customTags: WellKnownDiagnosticTags.Unnecessary
        );
    }
}
