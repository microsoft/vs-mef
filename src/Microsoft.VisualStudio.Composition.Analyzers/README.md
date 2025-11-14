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
