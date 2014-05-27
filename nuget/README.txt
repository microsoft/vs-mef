Microsoft.VisualStudio.Composition
==================================

Initialize your IoC container and extract your first export using code such as the following:

    var exportProviderFactory = ExportProviderFactory.LoadDefault();
    var exportProvider = exportProviderFactory.CreateExportProvider();
    var program = exportProvider.GetExportedValue<Program>();

To include assembly or project references in the MEF catalog,
set the MEFAssembly metadata on those references to "true", such as:

  <ItemGroup>
    <ProjectReference Include="..\library\library.csproj">
      <MEFAssembly>true</MEFAssembly>
    </ProjectReference>
  </ItemGroup>
