# VSMEF014 Suppress CS8618 for MEF importing members on exported parts

This suppressor removes CS8618 ("Non-nullable member must contain a non-null value when exiting constructor") from MEF importing fields and properties that MEF will initialize after construction.

## Cause

The C# nullable flow analysis does not know that MEF assigns importing members during composition. As a result, code like this can produce CS8618 even though the member will be initialized by MEF:

```cs
using System.ComponentModel.Composition;

[Export]
class Service
{
    [Import]
    public ILogger Logger { get; set; }
}
```

Without this suppressor, you may need to write `= null!;` just to silence the compiler warning.

## Rule description

This suppressor applies to importing fields and properties when all of the following are true:

- The member has `[Import]` or `[ImportMany]`, including attributes derived from either MEF namespace
- The containing type is an exported part, either because the type itself is exported or because one of its instance members is exported
- The part is not marked `[PartNotDiscoverable]`
- The import does not specify `AllowDefault = true`
- If the member is a property, it has a setter

When those conditions are met, MEF is expected to initialize the member after construction, so CS8618 is suppressed.

## Example

```cs
using System.ComponentModel.Composition;

[Export]
class Service
{
    [Import]
    public ILogger Logger { get; set; }
}
```

With `VSMEF014`, the `Logger` property does not need `= null!;`.

## Cases that are not suppressed

### Optional import

```cs
using System.ComponentModel.Composition;

[Export]
class Service
{
    [Import(AllowDefault = true)]
    public ILogger Logger { get; set; }
}
```

Optional imports may legitimately remain `null`, so `VSMEF014` does not suppress CS8618 for them.

### Get-only property

```cs
using System.ComponentModel.Composition;

[Export]
class Service
{
    [Import]
    public ILogger Logger { get; }
}
```

MEF cannot assign a get-only property, so the suppressor does not apply.

### Non-exported type

```cs
using System.ComponentModel.Composition;

class Service
{
    [Import]
    public ILogger Logger { get; set; }
}
```

If the containing type is not an exported part, the suppressor does not apply.

## How to fix

No code change is required when this suppressor applies. You can remove redundant `= null!;` initializers from qualifying imports.

If CS8618 still appears, check whether one of the non-suppressed cases applies:

- The import is optional (`AllowDefault = true`)
- The property has no setter
- The containing type is not exported
- The part is `[PartNotDiscoverable]`
