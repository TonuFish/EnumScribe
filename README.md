<h1 align="center">
    <img src="img/icon_256.png" />
</h1>

<h1 align="center">

[![Nuget](https://img.shields.io/nuget/v/EnumScribe?color=%236b2671&logo=Nuget&logoColor=%23ba2f8c&style=for-the-badge)](https://www.nuget.org/packages/EnumScribe)
[![Nuget](https://img.shields.io/nuget/dt/EnumScribe?color=%236b2671&logo=Nuget&logoColor=%23ba2f8c&style=for-the-badge)](https://www.nuget.org/packages/EnumScribe)
</h1>

# EnumScribe

An easy-to-use source generator providing efficient access to enum description text.

## Why?

When binding an enum to UI component, it's uncommon to be able to display the enum identifier itself, EG. "OutOfStock" doesn't adhere to english grammar. Working around this usually either comes with a runtime cost (reflection) or maintainability cost (manual enum -> text mapping)

EnumScribe simplifies the process by generating the mapping at compile time based on `Description` attributes and exposing it via properties added to the type, making the text easier to consume in most UI frameworks.

## Features

- Create additional properties returning the matching enum's description
- Implement available partial methods instead of creating new properties
- Customise the default suffix ("Description")
- Selectively scribe enums by accessibility and member type
- Ignore individual enums with `NoScribe`
- Opt out of serializing generated properties with `JsonIgnore` (Supports [Json.NET](https://www.newtonsoft.com/json) and [System.Text.Json](https://docs.microsoft.com/en-us/dotnet/api/system.text.json))

## Basic usage

```Csharp
// Source code
[Scribe]
public partial class MyDto
{
    public MyEnum? ToProperty { get; set; }

    public MyEnum ToMethod { get; set; }
    public partial string ToMethodDescription();

    [NoScribe]
    public MyEnum HiddenProperty { get; set; }
}

// Generated code
public partial class MyDto
{
    // "ToProperty" + "Description"
    public string? ToPropertyDescription { get { /* ... */ } }
    // "ToMethod" + "Description"
    public partial string ToMethodDescription() { /* ... */ }
}
```

## Troubleshooting

### No code is being generated :\(

Source generators are a relatively new feature and are still facing some intermittent teething issues (EG. [#49249](https://github.com/dotnet/roslyn/issues/49249))

Most issues are addressed by cleaning the project, then restarting your IDE and rebuilding it.

It can be helpful to opt into outputting generated files by including the below snippet in the consuming project. While these are normally viewable in the VS solution explorer (`Dependencies\Analyzers\EnumScribe`) it's not entirely reliable yet.

```xml
  <PropertyGroup Condition="'$(Configuration)'=='Debug'">
    <!-- MyProject\obj\GeneratedFiles by default -->
    <CompilerGeneratedFilesOutputPath>$(BaseIntermediateOutputPath)\GeneratedFiles</CompilerGeneratedFilesOutputPath>
    <EmitCompilerGeneratedFiles>true</EmitCompilerGeneratedFiles>
  </PropertyGroup>
```

### Definition cannot be found

Compiler errors [CS1061](https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/compiler-messages/cs1061) and CS8795 warns of missing or unimplemented type members and may erronously report that generated code is missing. This may presist through project rebuilds, but can usually be resolved by restarting your IDE.

## Planned work

- Proper tests and benchmarks
- Analyzer warning for unnecessary `NoScribe` attributes
- Localise diagnostic text
- Scribe `T` in generic types where `T` is an enum
- New attribute `ReScribe` to manually override scribe rules on a property/field basis (suffix, accessibility)
- Acknowledge `ReScribe` attribute on property without the containing class requiring ScribeEnum
- Scribing structs and record structs
