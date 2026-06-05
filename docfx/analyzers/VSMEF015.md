# VSMEF015 Metadata view interface should be source-generated

`VSMEF015` warns when a C# import uses a metadata view interface declared in the same compilation, but that interface is not set up for VS MEF source generation.

## Why this matters

Without source generation, VS MEF falls back to the older runtime-generated metadata view path. If you own the interface, source generation is the simpler and more predictable option.

## How to fix it

Annotate the interface with <xref:Microsoft.VisualStudio.Composition.MetadataViewAttribute> and declare it `partial`.

```csharp
using Microsoft.VisualStudio.Composition;

[MetadataView]
public partial interface IMyMetadata
{
    string Name { get; }
}
```

If the interface is nested, each containing type must also be `partial`.
