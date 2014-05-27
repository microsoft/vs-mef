Microsoft.VisualStudio.Composition
==================================

Initialize your IoC container and extract your first export using code such as the following:

    var exportProviderFactory = ExportProviderFactory.LoadDefault();
    var exportProvider = exportProviderFactory.CreateExportProvider();
    var program = exportProvider.GetExportedValue<Program>();
