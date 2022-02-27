using VerifyTests;
using System.Runtime.CompilerServices;

#if !NET5_0_OR_GREATER
namespace System.Runtime.CompilerServices
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public sealed class ModuleInitializerAttribute : Attribute { }
}
#endif

namespace EnumScribe.Tests
{
    public static class ModuleInitializer
    {
        /// <summary>
        /// Called on assembly load, some initialisation required by Verify.SourceGenerators.
        /// </summary>
        [ModuleInitializer]
        public static void Init()
        {
            VerifySourceGenerators.Enable();
        }
    }
}
