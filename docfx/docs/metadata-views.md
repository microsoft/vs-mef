# Metadata views

VS MEF supports several ways to consume export metadata through a strongly-typed view. The differences between them matter because they affect:

- which exports are filtered out during composition
- whether `[DefaultValue]` participates in metadata access only, or also in filtering
- whether metadata types such as `System.Type` and `System.Type[]` preserve VS MEF's lazy assembly-load behavior

## Interface metadata views

An interface metadata view is the baseline model.

[!code-csharp[](../../samples/docs/MetadataViews.cs#InterfaceMetadataView)]

For interface metadata views:

- properties **without** `[DefaultValue]` are **required**
- properties **with** `[DefaultValue]` are **optional**
- required properties filter exports by **presence** and **type compatibility**
- optional properties allow the metadata key to be absent, but if the key is present its value still has to be type-compatible

This is the most predictable model when you want the metadata contract itself to define filtering behavior.

## Legacy metadata view implementation classes

MEF v1 also supports applying <xref:System.ComponentModel.Composition.MetadataViewImplementationAttribute> to an interface and pointing it at a concrete implementation type.

[!code-csharp[](../../samples/docs/MetadataViews.cs#LegacyMetadataViewImplementation)]

The legacy implementation style uses a constructor that accepts the raw metadata dictionary. This remains supported for compatibility.

For these legacy dictionary-backed implementation classes:

- required interface properties still participate in filtering
- optional interface properties are **not** included in the filter model
- the implementation class is responsible for reading metadata, applying defaults, and coercing values

Because the implementation is working directly from the raw dictionary, it does **not** automatically inherit the full interface metadata-view behavior.

## `MetadataView`-based implementation classes

VS MEF now includes <xref:Microsoft.VisualStudio.Composition.MetadataView> to make concrete metadata view implementations easier to write while still keeping the interface as the metadata contract.

[!code-csharp[](../../samples/docs/MetadataViews.cs#MetadataViewBaseImplementation)]

For `MetadataView`-derived implementation classes:

- the interface remains the canonical filtering contract
- required properties filter by presence and type compatibility
- optional properties participate as optional constraints just like ordinary interface metadata views
- `[DefaultValue]` is honored through the same metadata-view pipeline used for interfaces
- metadata values such as `System.Type` and `System.Type[]` preserve VS MEF's lazy materialization behavior because the values are read through the library's metadata wrappers instead of eagerly coerced by user code

This is the recommended approach when you want a concrete metadata view type without reimplementing the filtering and lazy metadata semantics yourself.

> [!IMPORTANT]
> A type derived from <xref:Microsoft.VisualStudio.Composition.MetadataView> is **not** meant to be used directly as `TMetadata` in `Lazy<T, TMetadata>`. It must be reached through an interface metadata view that is annotated with <xref:System.ComponentModel.Composition.MetadataViewImplementationAttribute>. VS MEF rejects direct use as an unsupported metadata view type, so declared imports fail during composition and direct export queries fail when the query is evaluated.

## Choosing a model

Use:

- a plain **interface metadata view** when an interface is sufficient
- a **`MetadataView`-derived class** when you want a concrete type but still want interface-style filtering and metadata handling
- a **dictionary-constructor implementation** only when you need to preserve existing MEF v1 behavior or custom dictionary-based logic

## Behavior summary

| Metadata view model | Required properties filter | Optional properties filter | Defaults handled by VS MEF | Preserves lazy `Type` / `Type[]` behavior |
| --- | --- | --- | --- | --- |
| Interface metadata view | Yes | Yes, when present | Yes | Yes |
| Legacy dictionary-backed implementation | Yes | No | No, implementation decides | Not automatically |
| `MetadataView`-derived implementation | Yes | Yes, when present | Yes | Yes |
