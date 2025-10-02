# VSMEF006 Import nullability and AllowDefault mismatch

When using nullable reference types, the nullability of an import should match its `AllowDefault` setting. This analyzer detects mismatches between nullable annotations and the `AllowDefault` property in import attributes.

## Two types of mismatches detected

### 1. Nullable import without AllowDefault = true

The following property definition would produce a diagnostic from this rule:

```cs
#nullable enable
using System.ComponentModel.Composition;

class Foo
{
    [Import]
    public string? SomeProperty { get; set; }
}
```

This property is nullable (can accept `null` values) but doesn't have `AllowDefault = true`, which means MEF will throw an exception if no matching export is found instead of setting the property to `null`.

Fix the diagnostic by adding `AllowDefault = true`:

```cs
#nullable enable
using System.ComponentModel.Composition;

class Foo
{
    [Import(AllowDefault = true)]
    public string? SomeProperty { get; set; }
}
```

### 2. AllowDefault = true without nullable type

The following property definition would also produce a diagnostic:

```cs
#nullable enable
using System.ComponentModel.Composition;

class Foo
{
    [Import(AllowDefault = true)]
    public string SomeProperty { get; set; }
}
```

This property has `AllowDefault = true` (meaning it can be `null` if no export is found) but is declared as non-nullable, creating a potential null reference exception.

## Fixing the diagnostic

### Option 1: Make the type nullable

```cs
#nullable enable
using System.ComponentModel.Composition;

class Foo
{
    [Import(AllowDefault = true)]
    public string? SomeProperty { get; set; }
}
```

### Option 2: Remove AllowDefault

```cs
#nullable enable
using System.ComponentModel.Composition;

class Foo
{
    [Import]
    public string SomeProperty { get; set; }
}
```

## Additional examples

### Constructor parameters

The same rules apply to constructor parameters:

```cs
#nullable enable
using System.ComponentModel.Composition;

[Export]
class Foo
{
    [ImportingConstructor]
    public Foo([Import(AllowDefault = true)] string? optionalService)
    {
        // optionalService can be null
    }
}
```

### ImportMany with nullable collections

For `ImportMany`, the collection itself typically shouldn't be nullable, but the element type might be:

```cs
#nullable enable
using System.ComponentModel.Composition;

class Foo
{
    [ImportMany]
    public IEnumerable<IService?> Services { get; set; } = null!;
}
```

## Benefits

Following this analyzer's recommendations helps ensure:

1. **Consistency**: Nullable annotations accurately reflect the runtime behavior
2. **Safety**: Reduces null reference exceptions at runtime
3. **Clarity**: Makes the intent clear to other developers reading the code
4. **Tooling support**: Enables better IntelliSense and static analysis
