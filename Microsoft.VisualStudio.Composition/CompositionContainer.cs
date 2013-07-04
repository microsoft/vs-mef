namespace Microsoft.VisualStudio.Composition
{
    using System;
    using Validation;

    public class CompositionContainer : IDisposable
    {
        private readonly ExportFactory exportFactory;

        internal CompositionContainer(ExportFactory exportFactory)
        {
            Requires.NotNull(exportFactory, "exportFactory");

            this.exportFactory = exportFactory;
        }

        public T GetExport<T>() where T : class
        {
            return this.exportFactory.GetExport<T>();
        }

        public T GetExport<T>(string contractName) where T : class
        {
            return this.exportFactory.GetExport<T>(contractName);
        }

        public void Dispose()
        {
            // TODO: dispose of any instantiated, disposable values in the container.
        }
    }
}
