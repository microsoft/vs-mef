namespace Microsoft.VisualStudio.Composition
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Validation;

    [DebuggerDisplay("{Contract.Type.Name,nq} (Lazy: {IsLazy}, {Cardinality})")]
    public class ImportDefinition : IEquatable<ImportDefinition>
    {
        public ImportDefinition(CompositionContract contract, ImportCardinality cardinality, Type lazyType)
        {
            Requires.NotNull(contract, "contract");

            this.Contract = contract;
            this.Cardinality = cardinality;
            this.LazyType = lazyType;
        }

        public ImportCardinality Cardinality { get; private set; }

        public bool IsLazy
        {
            get { return this.LazyType != null; }
        }

        public bool IsLazyConcreteType
        {
            get { return this.LazyType != null && this.LazyType.GetGenericTypeDefinition().IsEquivalentTo(typeof(Lazy<>)); }
        }

        public Type LazyType { get; private set; }

        public CompositionContract Contract { get; private set; }

        /// <summary>
        /// Gets the actual type (without the Lazy{T} wrapper) of the importing member.
        /// </summary>
        public Type CoercedValueType
        {
            get
            {
                // MEF v2 only allows for this to match the contract itself. MEF v1 was more flexible.
                return this.Contract.Type;
            }
        }

        public override int GetHashCode()
        {
            return this.Contract.GetHashCode();
        }

        public override bool Equals(object obj)
        {
            return this.Equals(obj as ImportDefinition);
        }

        public bool Equals(ImportDefinition other)
        {
            if (other == null)
            {
                return false;
            }

            return this.Contract.Equals(other.Contract)
                && this.Cardinality == other.Cardinality;
        }
    }
}
