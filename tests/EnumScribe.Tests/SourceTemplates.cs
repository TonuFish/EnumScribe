﻿//using static EnumScribe.Internal.EnumScribeConsts;

//namespace EnumScribe.Tests
//{
//    internal static class SourceTemplates
//    {
//        public static class Enum
//        {
//            public const string OneEnumThreeMembersTemplate =
//@"using System.ComponentModel;

//namespace %%ENUM_NAMESPACE%%
//{
//    public enum %%ENUM_NAME%%
//    {
//        [Description(""%%ENUM_0_DESC%%"")]
//        %%ENUM_0_NAME%%,
//        [Description(""%%ENUM_1_DESC%%"")]
//        %%ENUM_1_NAME%%,
//        [Description(""%%ENUM_2_DESC%%"")]
//        %%ENUM_2_NAME%%,
//    }
//}
//";
//        }

//        public static class Type
//        {
//            public const string OneTypeNoNestingInNamespaceTemplate =
//@"using EnumScribe;
//using %%ENUM_NAMESPACE%%;

//namespace %%TYPE_NAMESPACE%%
//{
//    [Scribe(%%TYPE_SCRIBE_ARGUMENTS%%)]
//    public %%TYPE_TYPE%% %%TYPE_NAME%%
//    {
//        %%PROP_0_ACCESSIBILITY%% %%ENUM_NAME%% %%PROP_0_NAME%% { get; set; }
//    }
//}
//";
//        }
//    }

//    public static class ResultTemplates
//    {
//        public static class Enum
//        {
//            public const string OneEnumThreeMembersTemplateResult =
//@"// <auto-generated>
//#nullable enable

//using System.Runtime.CompilerServices;

//namespace EnumScribe.Extensions
//{
//    [System.CodeDom.Compiler.GeneratedCodeAttribute(""EnumScribe"", """ + PackageVersion + @""")]
//    internal static class EnumDescriptions
//    {
//        [MethodImpl(MethodImplOptions.AggressiveInlining)]
//        public static string DescriptionText(this %%ENUM_NAMESPACE%%.%%ENUM_NAME%% e) => e switch
//            {
//                %%ENUM_NAMESPACE%%.%%ENUM_NAME%%.%%ENUM_0_NAME%% => ""%%ENUM_0_DESC%%"",
//                %%ENUM_NAMESPACE%%.%%ENUM_NAME%%.%%ENUM_1_NAME%% => ""%%ENUM_1_DESC%%"",
//                %%ENUM_NAMESPACE%%.%%ENUM_NAME%%.%%ENUM_2_NAME%% => ""%%ENUM_2_DESC%%"",
//            };
//    }
//}

//#nullable restore
//";
//        }

//        public static class Type
//        {
//            public const string OneTypeNoNestingInNamespaceTemplateResult =
//@"// <autogenerated/>
//#nullable enable

//using EnumScribe.Extensions;

//namespace EnumScribe.Samples
//{
//    partial %%TYPE_TYPE%% %%TYPE_NAME%%
//    {
//        %%PROP_0_ACCESSIBILITY%% %%ENUM_NAME%% %%PROP_0_NAME%%%%SCRIBE_SUFFIX%% => %%PROP_0_NAME%%.DescriptionText();
//    }
//}

//#nullable restore
//";
//        }
//    }
//}