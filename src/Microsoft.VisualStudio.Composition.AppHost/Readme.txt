# Microsoft.VisualStudio.Composition.AppHost

## How to initialize MEF at runtime

Initialize your IoC container and extract your first export using code such as the following:

    var exportProviderFactory = await ExportProviderFactory.LoadDefaultAsync();
    var exportProvider = exportProviderFactory.CreateExportProvider();
    var program = exportProvider.GetExportedValue<Program>();

## Customizing your MEF catalog

To include assembly or project references in the MEF catalog,
set the MEFAssembly metadata on those references to "true", such as:

  <ItemGroup>
    <ProjectReference Include="..\library\library.csproj">
      <MEFAssembly>true</MEFAssembly>
    </ProjectReference>
  </ItemGroup>

Alternatively, include assemblies in the `MEFCatalogAssembly` item list.
If doing this from a target, add the target to the $(GenerateMEFCompositionCacheDependsOn) property.

## Troubleshooting your MEF catalog

If build-time catalog creation produces MEF discovery errors due to the inability to find
assemblies referenced from your MEF catalog assemblies, include the referenced assemblies
in the `MEFReferenceAssembly` MSBuild item list.
