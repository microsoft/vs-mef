namespace Microsoft.VisualStudio.Composition
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

    /// <summary>
    /// A non-generic interface implemented by <see cref="LazyPart{T, TMetadata}"/>.
    /// </summary>
    /// <remarks>
    /// This interface allows downcasting from Object to this interface to get the
    /// metadata and value. Without this interface, an instance of LazyPart`2 cannot
    /// be cast from Object to anything to extract the value or metadata unless the
    /// exact type of T is known. For instance, one cannot downcast to ILazy{object} 
    /// because LazyPart{int} cannot be assigned to LazyPart{object}.
    /// </remarks>
    internal interface IHasValueAndMetadata
    {
        object Value { get; }

        IReadOnlyDictionary<string, object> Metadata { get; }
    }
}
