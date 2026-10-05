# Metadata views

VS MEF supports several ways to consume export metadata through a strongly-typed view. The differences between them matter because they affect:

- which exports are filtered out during composition
- whether <xref:System.ComponentModel.DefaultValueAttribute> participates in metadata access only, or also in filtering
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

## Source-generated metadata view implementations

If you reference the VS MEF analyzers/source generator, VS MEF can generate the concrete metadata view implementation for you from a metadata interface that you annotate with <xref:Microsoft.VisualStudio.Composition.MetadataViewAttribute>.

[!code-csharp[](../../samples/docs/MetadataViews.cs#SourceGeneratedMetadataView)]

For source-generated metadata views:

- apply `[MetadataView]` to the **interface declaration**
- declare the interface `partial`
- if the interface is nested, every containing type must also be `partial`
- build the assembly that **declares** the interface with the VS MEF analyzer package enabled

The generator emits a concrete <xref:Microsoft.VisualStudio.Composition.MetadataView>-derived type and adds <xref:System.ComponentModel.Composition.MetadataViewImplementationAttribute> directly to the interface in that same compilation.

The generator intentionally does **not** generate another implementation when the interface already names one via <xref:System.ComponentModel.Composition.MetadataViewImplementationAttribute>.

This is the **recommended** way to get a concrete metadata view type:

- the interface remains the metadata contract, so filtering semantics stay the same as ordinary interface metadata views
- `[DefaultValue]` keeps working for both filtering and metadata access
- metadata values such as `System.Type` and `System.Type[]` preserve VS MEF's lazy materialization behavior
- you do not have to hand-author a dictionary-reading constructor or manually forward each property through `MetadataView.GetMetadata<T>()`

If the interface lives in another assembly, recompile that assembly with `[MetadataView]` applied there. VS MEF no longer generates metadata view implementations from consuming assemblies for referenced interfaces.

If you own the metadata interface, prefer this source-generated path over hand-writing a `MetadataView`-derived class.

## Legacy metadata view implementation classes

MEF v1 also supports applying <xref:System.ComponentModel.Composition.MetadataViewImplementationAttribute> to an interface and pointing it at a concrete implementation type.

[!code-csharp[](../../samples/docs/MetadataViews.cs#LegacyMetadataViewImplementation)]

The legacy implementation style uses a constructor that accepts the raw metadata dictionary. This remains supported for compatibility.

For these legacy dictionary-backed implementation classes:

- required interface properties still participate in filtering
- optional interface properties are **not** included in the filter model
- the implementation class is responsible for reading metadata, applying defaults, and coercing values

Because the implementation is working directly from the raw dictionary, it does **not** automatically inherit the full interface metadata-view behavior and is generally less convenient than the source-generated path.

## Hand-written `MetadataView` implementation classes

VS MEF now includes <xref:Microsoft.VisualStudio.Composition.MetadataView> to make concrete metadata view implementations easier to write while still keeping the interface as the metadata contract.

[!code-csharp[](../../samples/docs/MetadataViews.cs#MetadataViewBaseImplementation)]

For interface-backed `MetadataView`-derived implementation classes:

- the interface remains the canonical filtering contract
- required properties filter by presence and type compatibility
- optional properties participate as optional constraints just like ordinary interface metadata views
- `[DefaultValue]` is honored through the same metadata-view pipeline used for interfaces
- metadata values such as `System.Type` and `System.Type[]` preserve VS MEF's lazy materialization behavior because the values are read through the library's metadata wrappers instead of eagerly coerced by user code

This is a good escape hatch when the generated implementation is not sufficient, but most new code should prefer the source-generated approach so the implementation stays declarative and maintenance-free.

> [!IMPORTANT]
> When a <xref:Microsoft.VisualStudio.Composition.MetadataView>-derived type is reached through <xref:System.ComponentModel.Composition.MetadataViewImplementationAttribute>, the attributed interface remains the metadata contract.

## Direct `MetadataView` classes

You can also use a <xref:Microsoft.VisualStudio.Composition.MetadataView>-derived class directly as `TMetadata`.

[!code-csharp[](../../samples/docs/MetadataViews.cs#DirectMetadataView)]

For direct `MetadataView` classes:

- the metadata contract is the set of **public instance properties** on the class and its base types
- required properties are those without `[DefaultValue]`
- optional properties are those with `[DefaultValue]`
- optional properties still filter by type compatibility when metadata is present
- inherited public properties participate in both filtering and default handling
- if a derived class hides a base property with the same name, the **most-derived property wins**
- implemented interfaces are ignored for this direct path
- metadata values such as `System.Type` and `System.Type[]` preserve VS MEF's lazy materialization behavior because the values are read through the library's metadata wrappers instead of eagerly coerced by user code

## Choosing a model

Use:

- a plain **interface metadata view** when an interface is sufficient
- a **source-generated metadata view implementation** when you want a concrete type while keeping the interface as the metadata contract
- a hand-written **interface-backed `MetadataView`-derived class** only when you need custom implementation logic that the generator cannot express
- a **direct `MetadataView`-derived class** when the class itself should define the metadata contract through its public instance properties
- a **dictionary-constructor implementation** only when you need to preserve existing MEF v1 behavior or custom dictionary-based logic

## Behavior summary

| Metadata view model | Required properties filter | Optional properties filter | Defaults handled by VS MEF | Preserves lazy `Type` / `Type[]` behavior |
| --- | --- | --- | --- | --- |
| Interface metadata view | Yes | Yes, when present | Yes | Yes |
| Legacy dictionary-backed implementation | Yes | No | No, implementation decides | Not automatically |
| Source-generated metadata view implementation | Yes | Yes, when present | Yes | Yes |
| Hand-written interface-backed `MetadataView` implementation | Yes | Yes, when present | Yes | Yes |
| Direct `MetadataView` class | Yes | Yes, when present | Yes | Yes |
