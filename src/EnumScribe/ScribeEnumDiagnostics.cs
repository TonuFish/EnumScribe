using Microsoft.CodeAnalysis;

namespace EnumScribe
{
    internal static class ScribeEnumDiagnostics
    {
        // TODO: Release tracking analyzer, thanks roslynator!
        // https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

        internal static readonly DiagnosticDescriptor ES0001 = new DiagnosticDescriptor
        (
            "ES0001",
            "Invalid suffix argument",
            "Class {0} {1} suffix argument must consist of valid identifier characters",
            "EnumScribe.Naming",
            DiagnosticSeverity.Error,
            true,
            helpLinkUri: "https://github.com/TonuFish/EnumScribe/docs/analyzers/ES0001.md"
        );
    }
}
