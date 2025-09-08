# VSMEF002 Avoid mixing MEF attribute libraries

Two varieties of MEF attributes exist, and are found in the following namespaces:

- `System.ComponentModel.Composition` (aka MEF v1, or ".NET MEF")
- `System.Composition` (aka MEF v2 or "NuGet MEF")

An application or library must use the attributes that match the MEF engine used in the application.

If the VS-MEF engine (Microsoft.VisualStudio.Composition) is used, both sets of attributes are allowed in the MEF catalog.
During composition, the engine will create edges between MEF parts that use different attribute sets.

It is important that only one variety of attributes be applied to any given MEF part (i.e. class or struct).
MEF parts have one or more exporting attributes applied to them, and that MEF part will be defined in the catalog only with the imports that are defined using attributes from the same variety as the exporting attribute.
As a result, exporting with one variety and importing with another will cause the MEF part to be defined in the catalog without any imports.

Note that the focus on just one variety of MEF attributes is honored at the scope of a single type.
Nested types are types in their own right and *may* use a different variety of MEF attributes than its containing type.

## Example violations found by this analyzer

### Simple example

The following example makes the mix of attribute varieties clear:

```cs
[System.ComponentModel.Composition.Export]
class Foo
{
    [System.Composition.Import]
    public Bar Bar { get; set; }
}
```

### Custom export attribute example

Other equally problematic but more subtle violations are possible.
Exporting attributes may be declared that derive from one variety but itself be defined in its own namespace.

Consider the following custom export attribute, which happens to derive from the MEFv1 `ExportAttribute` yet lives in its own namespace:

```cs
using System.ComponentModel.Composition;

namespace MyVendor;

[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public class ExportFireAttribute : ExportAttribute
{
    public ExportFireAttribute()
        : base(typeof(IFire))
    { }
}
```

Now consider a MEF part declared elsewhere that uses the above custom attribute:

```cs
using System.Composition;
using MyVendor;

namespace MyUser;

[ExportFireAttribute]
public class FastFire : IFire
{
    [Import]
    public IWater Water { get; set; }
}
```

The above MEF part is defective because it uses an export attribute that derives from the MEFv1 `ExportAttribute` but is implicitly using the mismatched MEFv2 `ImportAttribute` due to its `using` directive.

## How to fix violations

Consolidate on just one variety of MEF attributes for each MEF part.

Note that each variety of MEF attributes have subtle but important semantic differences.
For example, MEFv1 exported parts default to being instantiated once and shared with all importers,
whereas MEFv2 exported parts default to being instantiated once per importer.

The simple example above may be fixed by using just one namespace for all MEF attributes:

```cs
using System.ComponentModel.Composition;

[Export]
class Foo
{
    [Import]
    public Bar Bar { get; set; }
}
```

The other example that uses a custom export attribute may be fixed by verifying the variety that the custom attribute(s) belong to, and using that same variety for all other MEF attributes on that MEF part.

```diff
-using System.Composition;
+using System.ComponentModel.Composition;
 using MyVendor;
 
 namespace MyUser;
 
 [ExportFireAttribute]
 public class FastFire : IFire
 {
     [Import]
     public IWater Water { get; set; }
 }
```

## Very advanced scenarios

You might be in a very advanced scenario where a single type intentionally uses both MEF attribute varieties.
Such a case would presumably include at least `Export` attributes from both namespaces in order to define a MEF part in each world.
In such a case, you can suppress this diagnostic on the type in question.
You would need to manually confirm that each importing property has an `Import` attribute applied from both varieties.
