# Analyzers and suppressors

The following analyzers and suppressors are included in the
`Microsoft.VisualStudio.Composition.Analyzers` package
to help you avoid common mistakes while authoring MEF parts.

ID | Title
--|--
[VSMEF001](VSMEF001.md) | Importing property must have setter
[VSMEF002](VSMEF002.md) | Avoid mixing MEF attribute libraries
[VSMEF003](VSMEF003.md) | Exported type not implemented by exporting class
[VSMEF004](VSMEF004.md) | Exported type missing importing constructor
[VSMEF005](VSMEF005.md) | Multiple importing constructors
[VSMEF006](VSMEF006.md) | Import nullability and AllowDefault mismatch
[VSMEF007](VSMEF007.md) | Duplicate import contract
[VSMEF008](VSMEF008.md) | Import contract type not assignable to member type
[VSMEF009](VSMEF009.md) | ImportMany on non-collection type
[VSMEF010](VSMEF010.md) | ImportMany with unsupported collection type in constructor
[VSMEF011](VSMEF011.md) | Both Import and ImportMany applied to same member
[VSMEF012](VSMEF012.md) | Disallow MEF attribute version
[VSMEF013](VSMEF013.md) | Suppress IDE0044 for MEF imported fields
[VSMEF014](VSMEF014.md) | Suppress CS8618 for MEF importing members on exported parts
[VSMEF015](VSMEF015.md) | Metadata view interface should be source-generated
[VSMEF016](VSMEF016.md) | Referenced metadata view interface should be source-generated
[VSMEF017](VSMEF017.md) | Invalid [MetadataView] usage
[VSMEF018](VSMEF018.md) | Suppress CS0649 for MEF imported fields on exported parts
