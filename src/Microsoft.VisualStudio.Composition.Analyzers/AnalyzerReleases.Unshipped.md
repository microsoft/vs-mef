; Unshipped analyzer release
; <https://github.com/dotnet/roslyn/blob/main/src/RoslynAnalyzers/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md>

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
VSMEF003 | Usage | Warning | Exported type not implemented by exporting class
VSMEF004 | Usage | Error | Exported type missing importing constructor
VSMEF005 | Usage | Error | Multiple importing constructors
VSMEF006 | Usage | Warning | Import nullability and AllowDefault mismatch
VSMEF007 | Usage | Warning | Duplicate import contract
VSMEF008 | Usage | Error | Import contract type not assignable to member type
VSMEF009 | Usage | Error | ImportMany on non-collection type
VSMEF010 | Usage | Error | ImportMany with unsupported collection type in constructor
VSMEF011 | Usage | Error | Both Import and ImportMany applied to same member
VSMEF012 | Usage | Warning | Disallow MEF attribute version
