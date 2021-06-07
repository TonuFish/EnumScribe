using System;
using System.ComponentModel;
using EnumScribe;

namespace Other
{

}

namespace Samples
{
    static class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Hello World!");
            //var x = Fisherman.Cake;
        }
    }

    public partial class Fish
    {
        public partial class FishyTest
        {

        }
    }

    [ScribeEnum("EditedSuffix", IncludeFields = true, AccessModifiers = AccessModifiers.Public | AccessModifiers.Private)]
    public partial class Fish
    {
        [IgnoreScribe]
        public Fisherman ToIgnoreEnum { get; set; } = Fisherman.IgnoreCake;

        public Fisherman ShouldSeeProperty { get; set; } = Fisherman.PropertyCake;
        public Fisherman ShouldSeeField = Fisherman.FieldCake;
        public Fisherman? NullableShouldSeeProperty { get; set; } = Fisherman.NullablePropertyCake; // TODO: Reminder to handle Nullable<Enum>

        [ScribeEnum]
        public partial class FishyTest
        {
            public Fisherman TestFishy { get; set; }
        }

        public enum Fisherman
        {
            [Description("Ignore Cake.")]
            IgnoreCake = 0,
            [Description("Property Cake.")]
            PropertyCake = 1,
            [Description("Field Cake.")]
            FieldCake = 2,
            [Description("Nullable Property Cake.")]
            NullablePropertyCake = 3,
        }
    }

    [ScribeEnum("_Description")]
    public partial class Trout
    {
        public Fisherman UnderscoreEnumProperty { get; set; } = Fisherman.UnderscorePie;

        public enum Fisherman
        {
            UnderscorePie = 0,
        }
    }

    //public class Cake<T>
    //{
    //    // Generics exist. Not making V1.

    //    public T CakeProperty { get; set; }
    //}

    public class ShouldNotAppear
    {
        public Fisherman NoAppear { get; set; } = Fisherman.Alpaca;

        public enum Fisherman
        {
            Alpaca = 0,
        }
    }
}
