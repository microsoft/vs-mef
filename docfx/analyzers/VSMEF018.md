# VSMEF018 Suppress CS0649 for MEF imported fields on exported parts

`VSMEF018` suppresses `CS0649` for imported fields on exported MEF parts.

## Why this matters

MEF assigns imported fields during composition, not in the constructor or initializer that the C# compiler analyzes. That can make valid imports look like permanently unassigned fields and produce `CS0649` incorrectly.

## When it applies

The suppressor applies to fields on exported parts when the field is decorated with either flavor of `Import`-style attribute that this library recognizes.

MEFv2 does not support field imports, so in practice field suppression only applies to MEFv1 field imports.

## Example

```cs
using System;
using System.ComponentModel.Composition;

[Export]
public class SomeService
{
    [Import(AllowDefault = true)]
    private Lazy<IDependency>? dependency;
}

public interface IDependency { }
```
