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
            UseNamespaceEnumGeneric<int> q = new();
            q.SeeProperty = NamespaceLevelEnum.ZeroethEntry | NamespaceLevelEnum.FirstEntry;
            Console.WriteLine($"Value: {q.SeePropertyDescription()}");
        }
    }

    [Flags]
    public enum NamespaceLevelEnum
    {
        [Description("0th Entry")]
        ZeroethEntry = 0,
        [Description("1st Entry")]
        FirstEntry = 1,
        [Description("2nd Entry")]
        SecondEntry = 2,
    }

    [Scribe(JsonIgnore = true)]
    public partial record UseNamespaceEnum
    {
        public NamespaceLevelEnum SeeProperty { get; set; }
        public NamespaceLevelEnum? SeePropertyNullable { get; set; }
    }

    [Scribe]
    public partial class UseNamespaceEnumGeneric<T>
    {
        public NamespaceLevelEnum? SeeProperty { get; set; }

        //public partial string SeePropertyDescription();

        public partial string? SeePropertyDescription();

        //public partial string? SeePropertyDescription(int num);

        //public partial NamespaceLevelEnum SeePropertyDescription();

        //public partial NamespaceLevelEnum SeePropertyDescription(int num);

        //public partial NamespaceLevelEnum SeePropertyDescription(int num, int num2);

        //public partial NamespaceLevelEnum SeePropertyDescription<GenericType>();

        //public partial NamespaceLevelEnum SeePropertyDescription<GenericType, GenericTypeTwo>();

        [Scribe]
        public partial class UseNamespaceEnumGenericNested<U, V>
        {
            public NamespaceLevelEnum? SeeProperty { get; set; }

            public partial string? SeePropertyDescription();

            //public partial NamespaceLevelEnum SeePropertyDescription<GenericType>();

            //public partial NamespaceLevelEnum SeePropertyDescription<GenericType, GenericTypeTwo>();
        }
    }
}
