namespace Microsoft.VisualStudio.Composition
{
    using System;

    public class CompositionContainer
    {
        public T GetExport<T>() where T : new()
        {
            return new T();
        }
    }
}
