# VSMEF012 Disallow MEF attribute version

This analyzer allows enforcing that only a specific version of MEF attributes (v1 or v2) is used in a project. This is useful for projects that want to standardize on a single MEF version.

**This analyzer is disabled by default.**

## Cause

A MEF attribute from a disallowed version is used when the project has been configured to only allow a specific MEF version.

## Rule description

MEF has two attribute libraries:

- **MEFv1**: <xref:System.ComponentModel.Composition> (from .NET Framework and NuGet)
- **MEFv2**: <xref:System.Composition> (lightweight, portable)

While VS-MEF supports both attribute libraries, mixing them can lead to confusion and subtle bugs. Some projects may want to standardize on a single version for consistency.

This analyzer can be configured to:

- Only allow MEFv1 attributes (`AllowedMefVersion = V1`)
- Only allow MEFv2 attributes (`AllowedMefVersion = V2`)

When configured, using an attribute from the disallowed version will produce a diagnostic.

## Configuration

Add the following to your `.editorconfig` file:

```ini
# Enable the analyzer and specify allowed version
[*.cs]
dotnet_diagnostic.VSMEF012.severity = warning

# Allow only MEFv1 attributes
dotnet_diagnostic.VSMEF012.allowed_mef_version = V1

# Or allow only MEFv2 attributes
dotnet_diagnostic.VSMEF012.allowed_mef_version = V2
```

## Examples of violations

### When configured for V1 only

```cs
// .editorconfig: dotnet_diagnostic.VSMEF012.allowed_mef_version = V1

using System.Composition;  // MEFv2 namespace

[Export]  // ❌ VSMEF012: MEFv2 attribute not allowed
class Service
{
    [Import]  // ❌ VSMEF012: MEFv2 attribute not allowed
    public ILogger Logger { get; set; }
}
```

### When configured for V2 only

```cs
// .editorconfig: dotnet_diagnostic.VSMEF012.allowed_mef_version = V2

using System.ComponentModel.Composition;  // MEFv1 namespace

[Export]  // ❌ VSMEF012: MEFv1 attribute not allowed
class Service
{
    [Import]  // ❌ VSMEF012: MEFv1 attribute not allowed
    public ILogger Logger { get; set; }
}
```

## Valid scenarios (no diagnostic)

### Using allowed version

```cs
// .editorconfig: dotnet_diagnostic.VSMEF012.allowed_mef_version = V1

using System.ComponentModel.Composition;  // MEFv1 namespace

[Export]  // ✅ OK - MEFv1 is allowed
class Service
{
    [Import]  // ✅ OK
    public ILogger Logger { get; set; }
}
```

### Analyzer not configured

```cs
// No .editorconfig setting for VSMEF012

using System.Composition;

[Export]  // ✅ OK - analyzer is disabled by default
class Service { }
```

## How to fix violations

Replace the disallowed MEF attributes with the equivalent attributes from the allowed version.

### MEFv1 to MEFv2

| MEFv1 Attribute | MEFv2 Attribute |
|-----------------|-----------------|
| <xref:System.ComponentModel.Composition.ExportAttribute?displayProperty=fullName> | <xref:System.Composition.ExportAttribute?displayProperty=fullName> |
| <xref:System.ComponentModel.Composition.ImportAttribute?displayProperty=fullName> | <xref:System.Composition.ImportAttribute?displayProperty=fullName> |
| <xref:System.ComponentModel.Composition.ImportManyAttribute?displayProperty=fullName> | <xref:System.Composition.ImportManyAttribute?displayProperty=fullName> |
| <xref:System.ComponentModel.Composition.ImportingConstructorAttribute?displayProperty=fullName> | <xref:System.Composition.ImportingConstructorAttribute?displayProperty=fullName> |
| <xref:System.ComponentModel.Composition.PartCreationPolicyAttribute?displayProperty=fullName> | <xref:System.Composition.SharedAttribute?displayProperty=fullName> / <xref:System.Composition.PartNotDiscoverableAttribute?displayProperty=fullName> |
| <xref:System.ComponentModel.Composition.ExportMetadataAttribute?displayProperty=fullName> | Custom metadata attribute |

### MEFv2 to MEFv1

Use the reverse mapping of the table above.

## When to suppress warnings

Suppress this warning if:

- You intentionally need to use both MEF versions in a transitional codebase
- You're implementing adapters or bridges between MEFv1 and MEFv2 components

## Notes

- This analyzer is disabled by default. You must explicitly enable it and configure the allowed version.
- This analyzer is distinct from VSMEF002, which warns about *mixing* MEF versions on the same type. VSMEF013 disallows a version entirely.
- Some MEFv1-only features (like <xref:System.ComponentModel.Composition.InheritedExportAttribute>, <xref:System.ComponentModel.Composition.PartCreationPolicyAttribute>, <xref:System.ComponentModel.Composition.ExportMetadataAttribute>) have no direct MEFv2 equivalents.
