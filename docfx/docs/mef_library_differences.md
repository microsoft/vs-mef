# Differences between .NET MEF, NuGet MEF and VS MEF

VS MEF achieves very high compatibility with two prior MEF libraries that were not
themselves compatible with each other. They each have a unique set of attributes, interfaces
and patterns which may look similar but have contradicting default behaviors and more subtle
but important differences.

## MEF parts in the catalog

VS MEF models its parts catalog such that it can describe all the behaviors of both systems.
It has an extensible part discovery system, with two discovery modules built-in:
one that understands .NET MEF parts and another for NuGet MEF parts.
Each part discovery module can contribute parts to a common VS MEF catalog, after which
MEF parts no longer retain affinity to a particular MEF variety and are considered compatible
with each other.

## Catalog hierarchies vs. sharing boundaries

.NET MEF and NuGet MEF both had their own way to express "sub-scopes" of containers.
.NET MEF's story requires custom MEF hosting code that is aware of each scope, making it
impossible for a MEF part itself to define a new scope. Each scope is backed by a unique
catalog.
NuGet MEF's story utilizes what it calls "sharing boundaries" and gave MEF parts this freedom
to create sub-scopes. The scopes are discovered automatically based on MEF attributes.

VS MEF follows NuGet MEF's model and supports sub-scopes through NuGet MEF attributes.
To define a MEF part that can create new sub-scope instances, that MEF part must use
NuGet MEF attributes as only they can describe sharing boundaries.

## Caveats

* A given MEF part must use a single variety of MEF attributes. Mixing a .NET MEF `Export`
  attribute with a NuGet MEF `ExportMetadata` attribute will result in a MEF part that exports
  without the export metadata, because each part discovery module produces part descriptors
  based on the MEF attributes it was designed to understand.
* .NET MEF offered a `CompositionContainer.Compose` method which leveraged its dynamic
  recomposition feature to add a part to the graph after the container was instantiated.
  VS MEF offers no such facility. The catalog must be complete before the graph is created.
  VS-MEF has [compatible equivalents for two common uses of `CompositionContainer.Compose`][Compose].

[Compose]: dynamic_recomposition.md
