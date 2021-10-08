using System;
using System.ComponentModel;
using EnumScribe;

namespace Samples
{
    static class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Hello World!");
            //var trout = new Trout();
            //var suffix = trout.UnderscoreEnumProperty_Description;
        }
    }

    public partial class Fish
    {
        //public partial class FishyTest
        //{

        //}
    }

    //[Scribe("EditedSuffix", IncludeFields = true, AccessModifiers = AccessModifier.Public | AccessModifier.Private)]
    public partial class Fish
    {
        //[IgnoreScribe]
        //public Fisherman ToIgnoreEnum { get; set; } = Fisherman.IgnoreCake;

        public Fisherman ShouldSeeProperty { get; set; } = Fisherman.PropertyCake;

        [NoScribe]
        public Fisherman ShouldSeeField = Fisherman.FieldCake;

        //public Fisherman? NullableQMarkShouldSeeProperty { get; set; } = Fisherman.NullablePropertyCake;
        //public Nullable<Fisherman> NullableVerboseShouldSeeProperty { get; set; } = Fisherman.NullablePropertyCake;

        //[ScribeEnum]
        //public partial class FishyTest
        //{
        //    public Fisherman TestFishy { get; set; }
        //}

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

    //[ScribeEnum("_Description")]
    //public partial class Trout
    //{
    //    public Fisherman UnderscoreEnumProperty { get; set; } = Fisherman.UnderscorePie;

    //    public enum Fisherman
    //    {
    //        [Description("pie")]
    //        UnderscorePie = 0,
    //    }
    //}

    //public class Cake<T>
    //{
    //    // Generics exist. Not making V1.

    //    public T CakeProperty { get; set; }
    //}

    partial class ShouldNotAppear
    {
        public Fisherman NoAppear { get; set; } = Fisherman.Alpaca;

        [Scribe]
        partial class ShouldAppearInsideShouldNotAppear
        {
            public Fisherman Fish { get; set; }
            //public partial string FishDescription(); Support this.
        }

        public enum Fisherman
        {
            Alpaca = 0,
        }
    }
}
