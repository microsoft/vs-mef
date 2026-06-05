# VSMEF016 Referenced metadata view interface should be source-generated

`VSMEF016` warns when a C# import uses a metadata view interface declared in another assembly, but that referenced assembly did not emit <xref:System.ComponentModel.Composition.MetadataViewImplementationAttribute> for the interface.

## Why this matters

VS MEF now generates metadata view implementations only in the assembly that declares the interface. Consuming assemblies do not generate implementations for referenced interfaces.

## How to fix it

Recompile the assembly that declares the interface:

1. Add a reference to the `Microsoft.VisualStudio.Composition` package to that project, if missing.
2. Apply <xref:Microsoft.VisualStudio.Composition.MetadataViewAttribute> to the interface.
3. Declare the interface `partial`.

After that assembly is rebuilt, consumers will observe the generated <xref:System.ComponentModel.Composition.MetadataViewImplementationAttribute> automatically.
