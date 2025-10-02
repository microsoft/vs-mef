﻿; Unshipped analyzer release
; <https://github.com/dotnet/roslyn/blob/main/src/RoslynAnalyzers/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md>

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
VSMEF003 | Usage | Warning | Exported type not implemented by exporting class
VSMEF004 | Usage | Error | Exported type missing importing constructor
VSMEF005 | Usage | Error | Multiple importing constructors
VSMEF006 | Usage | Warning | Import nullability and AllowDefault mismatch
VSMEF007 | Usage | Warning | Duplicate import contract
