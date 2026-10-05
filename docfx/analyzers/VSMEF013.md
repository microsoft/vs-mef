# VSMEF013 Suppress IDE0044 for MEF imported fields

This suppressor removes IDE0044 ("Make field readonly") from MEF-imported fields.

## Cause

IDE0044 can suggest making a field `readonly` when it is only assigned outside the declaring type's constructors. That suggestion is incorrect for MEF-imported fields because MEF assigns them after construction via reflection.

## Rule description

This suppressor applies to fields decorated with `[Import]` or `[ImportMany]`, including attributes derived from those MEFv1 attributes.

When one of these attributes is present, `readonly` is not a valid fix because MEF needs to assign the field after the object has been constructed. The suppressor removes IDE0044 so the editor does not recommend a change that would break composition.

This suppressor only applies to field imports from <xref:System.ComponentModel.Composition>. MEFv2 does not support field imports.

## Example

```cs
using System.ComponentModel.Composition;

[Export]
class Service
{
    [Import]
    private ILogger logger;
}
```

Without this suppressor, IDE0044 may suggest making `logger` `readonly`. With `VSMEF013`, that suggestion is suppressed.

## When the suggestion is not suppressed

`VSMEF013` does not apply when:

- The field is not a MEF import
- The member is a property instead of a field
- The attribute comes from MEFv2, which does not support field imports

## How to fix

No code change is required when this suppressor applies. Keep the field mutable so MEF can assign it during composition.

If you want to avoid mutable imported fields entirely, prefer constructor injection or property imports where that better fits your part design.
