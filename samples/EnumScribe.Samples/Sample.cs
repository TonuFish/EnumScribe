using EnumScribe.Extensions;
using System;
using System.ComponentModel;

namespace EnumScribe.Samples
{
    // Uses the default suffix of "Description"
    // Targets public, internal and private properties
    [Scribe(AccessModifiers = AccessModifier.Internal | AccessModifier.Private)]
    public partial class Inventory
    {
        public StockLevel? CakeStock { get; set; }
        // public string? CakeStockDescription { get; }

        internal StockLevel FishStock { get; set; }
        // internal StockLevel FishStockDescription { get; }

        [NoScribe]
        public StockLevel PieStock { get; set; }
        // Excluded by NoScribe attribute

        private StockLevel _alpacaStock;
        // Excluded as fields are not scribed by default
    }

    // Uses the suffix "Text"
    // Targets public properties and fields
    // Adds the System.Text.Json and/or Json.NET [JsonIgnore] attributes, if the respective library is in scope
    [Scribe("Text", IncludeFields = true, JsonIgnore = true)]
    public partial record InventoryHistory
    {
        internal StockLevel DuckStock { get; set; }
        // Excluded as internal accessibility isn't explicitly targeted

        public StockLevel dragonStock;
        // public string dragonStockText { get; } and [JsonIgnore] attributes

        public StockLevel? OwlStock { get; set; }
        public partial string? OwlStockText();
        // public string? OwlStockText() { /* implemented */ }
    }

    public enum StockLevel
    {
        [Description("In stock")]
        Available = 0,
        [Description("Low stock")]
        Low,
        [Description("Out of stock")]
        OutOfStock,
        [Description("Unavailable")]
        Retired,
    }

    public static class Sample
    {
        public static void Main()
        {
            // Build the project to view the generated files!
            // EnumScribe.Samples\obj\GeneratedFiles\EnumScribe\EnumScribe.EnumScribeGenerator

            Inventory inv = new() { CakeStock = StockLevel.Low, FishStock = StockLevel.OutOfStock };
            InventoryHistory invHist = new() { OwlStock = StockLevel.Retired };
            Console.WriteLine(inv.CakeStockDescription);            // Low stock
            Console.WriteLine(inv.FishStockDescription);            // Out of stock
            Console.WriteLine(invHist.dragonStockText);             // In stock
            Console.WriteLine(invHist.OwlStockText());              // Unavailable
            Console.WriteLine(StockLevel.Low.DescriptionText());    // Low stock
        }
    }
}