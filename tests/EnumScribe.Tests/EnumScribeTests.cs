using System.Threading.Tasks;
using VerifyXunit;
using Xunit;

namespace EnumScribe.Tests
{
    [UsesVerify]
    public class EnumScribeTests
    {
        /*
         * TODO: Rough test list
         * Class v record scribed type
         * Nested permutations (class, record, struct, record struct)
         * Global namespace
         * Enum nullability correct
         * Suffix validates
         * Suffix works
         * AccessModifiers works
         * ImplementPartialMethods toggles correctly
         * Partial method implementation when correct
         * IncludeFields works
         * IgnoreScribe works for all permutations (This will be interesting...)
         * NoScribe works
         * Remaining analyzer warnings
         * ETC.
         */

        [Theory]
        [InlineData("class")]
        [InlineData("record")]
        public Task DummyTest(string type)
        {
            const string rawSource =
@"using EnumScribe;
using System;
using System.ComponentModel;

namespace TestCode
{
    [Scribe(AccessModifiers = AccessModifier.Internal | AccessModifier.Private)]
    public partial %%TYPE%% Inventory
    {
        public StockLevel? CakeStock { get; set; }
        internal StockLevel FishStock { get; set; }

        [NoScribe]
        public StockLevel PieStock { get; set; }

        private StockLevel _alpacaStock;
    }

    public enum StockLevel
    {
        [Description(""In stock"")]
        Available = 0,
        [Description(""Low stock"")]
        Low,
        [Description(""Out of stock"")]
        OutOfStock,
        [Description(""Unavailable"")]
        Retired,
    }
}
";

            return TestHelper.Verify(rawSource.Replace("%%TYPE%%", type), type);
        }
    }
}
