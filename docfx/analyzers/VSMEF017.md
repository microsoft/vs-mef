# VSMEF017 Invalid [MetadataView] usage

`VSMEF017` reports an error when <xref:Microsoft.VisualStudio.Composition.MetadataViewAttribute> is applied to an interface that the VS MEF source generator cannot handle.

## Valid `[MetadataView]` targets

`[MetadataView]` should be applied only to metadata-view interfaces that:

- are declared `partial`
- are nested only inside `partial` containing types
- contain only get-only, non-indexer properties

## Example

This triggers `VSMEF017` because the interface is not partial:

```csharp
using Microsoft.VisualStudio.Composition;

[MetadataView]
public interface IMyMetadata
{
    string Name { get; }
}
```

Make the interface (and any containing types) `partial`, and keep the interface to simple property-bag members.
