namespace Microsoft.VisualStudio.Composition
{
    using System;
    using Validation;

    public class CompositionContainer
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
    }
}
