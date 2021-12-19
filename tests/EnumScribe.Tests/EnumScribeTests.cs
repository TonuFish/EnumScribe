// REFERENCE:
// https://github.com/dotnet/roslyn-sdk/blob/main/src/Microsoft.CodeAnalysis.Testing/README.md

// Waiting on a PR to Microsoft.CodeAnalysis.CSharp.SourceGenerators.Testing.XUnit
// https://github.com/dotnet/roslyn-sdk/pull/918
// Currently depends on Microsoft.CodeAnalysis 3.8.0.0, EnumScribe uses 4.0.0.0
// It's not really feasible to downgrade scribe as there's significant differences in the API
// between the versions.

//using EnumScribe.Internal;
//using Microsoft.CodeAnalysis;
//using Microsoft.CodeAnalysis.CSharp;
//using Microsoft.CodeAnalysis.CSharp.Testing;
//using Microsoft.CodeAnalysis.CSharp.Syntax;
//using Microsoft.CodeAnalysis.Testing;
//using Microsoft.CodeAnalysis.Testing.Verifiers;
//using Microsoft.CodeAnalysis.Text;
//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Text;
//using System.Threading.Tasks;
//using FluentAssertions;
//using Xunit;
//using static EnumScribe.Internal.EnumScribeConsts;
//using Verifier = EnumScribe.Tests.CSharpSourceGeneratorVerifier<EnumScribe.Internal.EnumScribeGenerator>;

//namespace EnumScribe.Tests
//{
//    public class EnumScribeTests
//    {
//        [Fact]
//        public async Task DummyTest()
//        {
//            var enumSource = PrepareSourceTemplate(SourceTemplates.Enum.OneEnumThreeMembersTemplate, new());
//            var typeSource = PrepareSourceTemplate(SourceTemplates.Type.OneTypeNoNestingInNamespaceTemplate, new());
//            var enumResult = PrepareResultTemplate(ResultTemplates.Enum.OneEnumThreeMembersTemplateResult, new());
//            var typeResult = PrepareResultTemplate(ResultTemplates.Type.OneTypeNoNestingInNamespaceTemplateResult, new());

//            await new Verifier.Test
//            {
//                TestState =
//                {
//                    Sources = { enumSource, typeSource, },
//                    GeneratedSources =
//                    {
//                        (typeof(EnumScribeGenerator), EnumScribeGenerator.EnumsHintName, enumResult),
//                        (typeof(EnumScribeGenerator), EnumScribeGenerator.PartialsHintName, typeResult),
//                    },
//                    //ExpectedDiagnostics =
//                    //{
//                    //    new DiagnosticResult(EnumScribeDiagnostics.ES0001).WithLocation(line int, col int)
//                    //}
//                },
//            }.RunAsync().ConfigureAwait(true);
//        }

//        // ScribeEnum default works
//        // - Targeting right namespaces
//        // - Targeting right classes
//        //   - Nullable properties found
//        // - Targeting right enums
//        // Convention works
//        // IgnoreScribe works
//        // IncludeFields works
//        // PublicOnly works
//        // Etc.

//        public static SourceText PrepareSourceTemplate(string sourceTemplate, TemplateOptions options)
//        {
//            // TODO: Implement, or not, probably do something better &| easier
//            return SourceText.From(sourceTemplate, Encoding.UTF8, SourceHashAlgorithm.Sha256);
//        }

//        public static SourceText PrepareResultTemplate(string resultTemplate, TemplateOptions options)
//        {
//            // TODO: Implement, or not, probably do something better &| easier
//            return SourceText.From(resultTemplate, Encoding.UTF8, SourceHashAlgorithm.Sha256);
//        }
//    }

//    public record TemplateOptions(
//        string? EnumNamespace = null, string? EnumName = null,
//        string? Enum0Desc = null, string? Enum0Name = null,
//        string? Enum1Desc = null, string? Enum1Name = null,
//        string? Enum2Desc = null, string? Enum2Name = null,
//        string? TypeNamespace = null, string? TypeType = null, string? TypeName = null,
//        string? TypeScribeArguments = null,
//        string? Prop0Accessibility = null, string? Prop0Name = null,
//        string? ScribeSuffix = null);
//}
