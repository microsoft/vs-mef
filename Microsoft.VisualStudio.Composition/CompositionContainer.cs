namespace Microsoft.VisualStudio.Composition
{
    using System;
    using System.Collections.Generic;
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

        public IEnumerable<ILazy<T>> GetExports<T>()
        {
            return this.exportFactory.GetExports<T>();
        }

        public IEnumerable<ILazy<T>> GetExports<T>(string contractName)
        {
            return this.exportFactory.GetExports<T>(contractName);
        }

        public IEnumerable<ILazy<T, TMetadataView>> GetExports<T, TMetadataView>()
        {
            return this.exportFactory.GetExports<T, TMetadataView>();
        }

        public IEnumerable<ILazy<T, TMetadataView>> GetExports<T, TMetadataView>(string contractName)
        {
            return this.exportFactory.GetExports<T, TMetadataView>(contractName);
        }

        public IEnumerable<T> GetExportedValues<T>()
        {
            return this.exportFactory.GetExportedValues<T>();
        }

        public IEnumerable<T> GetExportedValues<T>(string contractName)
        {
            return this.exportFactory.GetExportedValues<T>(contractName);
        }

        public void Dispose()
        {
            this.exportFactory.Dispose();
        }
    }
}
