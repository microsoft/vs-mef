namespace Microsoft.VisualStudio.Composition
{
    using System;
    using Validation;

    public class CompositionContainer : IDisposable
    {
        private readonly ExportProvider exportFactory;

        internal CompositionContainer(ExportProvider exportFactory)
        {
            Requires.NotNull(exportFactory, "exportFactory");

            this.exportFactory = exportFactory;
        }

        public ILazy<T> GetExport<T>()
        {
            return this.exportFactory.GetExport<T>();
        }

        public ILazy<T> GetExport<T>(string contractName)
        {
            return this.exportFactory.GetExport<T>(contractName);
        }

        public T GetExportedValue<T>()
        {
            return this.exportFactory.GetExportedValue<T>();
        }

        public T GetExportedValue<T>(string contractName)
        {
            return this.exportFactory.GetExportedValue<T>(contractName);
        }

        public void Dispose()
        {
            this.exportFactory.Dispose();
        }
    }
}
