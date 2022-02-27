using EnumScribe.Generator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System.Collections.Generic;
using System.ComponentModel;
using System.Reflection;
using System.Threading.Tasks;
using VerifyXunit;

namespace EnumScribe.Tests
{
    // Thanks to Andrew Lock for the Verify testing method + examples!
    // Below method is adapted from this blog post:
    // https://andrewlock.net/creating-a-source-generator-part-2-testing-an-incremental-generator-with-snapshot-testing/

    public static class TestHelper
    {
        public static Task Verify(string source, params object[] parameters)
        {
            SyntaxTree syntaxTree = CSharpSyntaxTree.ParseText(source);

            IEnumerable<PortableExecutableReference> references = new[]
            {
                // System.Private.CoreLib.dll
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                // System.ComponentModel.Primitives.dll
                MetadataReference.CreateFromFile(typeof(DescriptionAttribute).Assembly.Location),
                // System.Runtime.dll; Doesn't contain a type to reference, so load directly
                MetadataReference.CreateFromFile(Assembly.Load("System.Runtime").Location),

                // Generator references
                MetadataReference.CreateFromFile(Assembly.Load(
                    "netstandard, Version=2.0.3.0, Culture=neutral, PublicKeyToken=cc7b13ffcd2ddd51").Location),
                MetadataReference.CreateFromFile(Assembly.Load(
                    "Microsoft.CodeAnalysis, Version=4.1.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35").Location),
                MetadataReference.CreateFromFile(Assembly.Load(
                    "Microsoft.CodeAnalysis.CSharp, Version=4.1.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35").Location),
                MetadataReference.CreateFromFile(Assembly.Load(
                    "System.Collections.Immutable, Version=5.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a").Location),
                MetadataReference.CreateFromFile(Assembly.Load(
                    "System.Memory, Version=4.0.1.1, Culture=neutral, PublicKeyToken=cc7b13ffcd2ddd51").Location),
                // EnumScribe.Generator.dll
                MetadataReference.CreateFromFile(typeof(EnumScribeGenerator).Assembly.Location),
            };

            CSharpCompilation compilation = CSharpCompilation.Create(
                assemblyName: "Tests",
                syntaxTrees: new[] { syntaxTree },
                references: references);

            EnumScribeGenerator generator = new();
            GeneratorDriver driver = CSharpGeneratorDriver.Create(generator);
            driver = driver.RunGenerators(compilation);

            return Verifier.Verify(driver).UseParameters(parameters).UseDirectory("Snapshots");
        }
    }
}
