# Authoring MEF parts

## Disposing MEF parts

.NET MEF and NuGet MEF recognize parts that implement <xref:System.IDisposable>.
Support for <xref:System.IAsyncDisposable> parts is unique to VS MEF.
Implement <xref:System.IAsyncDisposable> when cleanup requires asynchronous work instead of starting unobserved work from <xref:System.IDisposable.Dispose*>.

VS MEF disposes an instantiated part when its owning lifetime ends.
The owning lifetime is usually the <xref:Microsoft.VisualStudio.Composition.ExportProvider>, but a non-shared part created by an `ExportFactory<T>` is owned by the export lifetime returned from `CreateExport`.
Dispose that export lifetime promptly when the part is no longer needed.

When a lifetime is disposed asynchronously, VS MEF calls <xref:System.IAsyncDisposable.DisposeAsync*> and awaits it.
If a part implements both <xref:System.IDisposable> and <xref:System.IAsyncDisposable>, asynchronous disposal calls only `DisposeAsync`.
Synchronous lifetime disposal also cleans up parts that implement only <xref:System.IAsyncDisposable> by blocking until `DisposeAsync` completes.

See [Disposing an ExportProvider](hosting.md#disposing-an-exportprovider) for host configuration and disposal guidance.
