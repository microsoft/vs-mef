# Microsoft.VisualStudio.Composition.Analyzers

Analyzers for MEF consumers to help identify common errors in MEF parts.

Analyzer ID | Description
--|--
VSMEF001 | Ensures that importing properties define a `set` accessor.
VSMEF002 | Detects mixing of MEF v1 and MEF v2 attributes on the same type.
VSMEF003 | Ensures that exported types are implemented by the exporting class.
VSMEF004 | Ensures exported types have a parameterless constructor or importing constructor.
VSMEF005 | Detects multiple constructors marked with `[ImportingConstructor]`.
VSMEF006 | Ensures import nullability matches `AllowDefault` setting.
VSMEF007 | Detects when a type imports the same contract multiple times.
VSMEF008 | Warns when an import contract type is not assignable to the importing member type.
VSMEF009 | Ensures `[ImportMany]` members use supported collection shapes and can be initialized correctly.
VSMEF010 | Ensures constructor-parameter `[ImportMany]` imports use supported collection types.
VSMEF011 | Detects members annotated with both `[Import]` and `[ImportMany]`.
VSMEF012 | Warns when a disallowed MEF attribute version is used.
VSMEF015 | Warns when a same-compilation metadata view interface could be source-generated but is not annotated for generation.
VSMEF016 | Warns when a referenced metadata view interface should be recompiled with `[MetadataView]`.
VSMEF017 | Reports invalid `[MetadataView]` usage on interfaces that cannot be source-generated.

## Diagnostic Suppressors

Suppressor ID | Suppressed Diagnostic | Description
--|--|--
VSMEF013 | IDE0044 | Suppresses "Make field readonly" for fields decorated with MEF `[Import]` or `[ImportMany]` attributes, since such fields are assigned at runtime via reflection.
VSMEF014 | CS8618 | Suppresses "Non-nullable member must contain a non-null value when exiting constructor" for MEF importing members that are initialized after construction unless `AllowDefault = true` is specified.
VSMEF018 | CS0649 | Suppresses "Field is never assigned to, and will always have its default value" for MEF imported fields on exported parts, since composition assigns them at runtime.
