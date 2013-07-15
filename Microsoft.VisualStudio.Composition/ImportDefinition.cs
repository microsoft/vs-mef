namespace Microsoft.VisualStudio.Composition
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Validation;
    using MefV1 = System.ComponentModel.Composition;

    [DebuggerDisplay("{Contract.Type.Name,nq} (Lazy: {IsLazy}, {Cardinality})")]
    public class ImportDefinition : IEquatable<ImportDefinition>
    {
        private readonly Type wrapperType;

        public ImportDefinition(CompositionContract contract, ImportCardinality cardinality, Type wrapperType, IReadOnlyCollection<IImportSatisfiabilityConstraint> additionalConstraints)
        {
            Requires.NotNull(contract, "contract");
            Requires.NotNull(additionalConstraints, "additionalConstraints");

            this.Contract = contract;
            this.Cardinality = cardinality;
            this.wrapperType = wrapperType;
            this.ExportContraints = additionalConstraints;
            this.RequiredCreationPolicy = MefV1.CreationPolicy.Any;
        }

        public ImportDefinition(CompositionContract contract, ImportCardinality cardinality, Type wrapperType, IReadOnlyCollection<IImportSatisfiabilityConstraint> additionalConstraints, MefV1.CreationPolicy requiredCreationPolicy)
            : this(contract, cardinality, wrapperType, additionalConstraints)
        {
            this.RequiredCreationPolicy = requiredCreationPolicy;
        }

        public ImportCardinality Cardinality { get; private set; }

        public MefV1.CreationPolicy RequiredCreationPolicy { get; private set; }

        public bool IsLazy
        {
            get { return this.wrapperType.IsAnyLazyType(); }
        }

        public bool IsLazyConcreteType
        {
            get { return this.wrapperType.IsConcreteLazyType(); }
        }

        public Type LazyType
        {
            get { return this.IsLazy ? this.wrapperType : null; }
        }

        public bool IsExportFactory
        {
            get { return this.wrapperType.IsExportFactoryTypeV1() || this.wrapperType.IsExportFactoryTypeV2(); }
        }

        public Type ExportFactoryType
        {
            get { return this.IsExportFactory ? this.wrapperType : null; }
        }

        public Type MetadataType
        {
            get
            {
                if (this.IsLazy || this.IsExportFactory)
                {
                    var args = this.wrapperType.GetGenericArguments();
                    if (args.Length == 2)
                    {
                        return args[1];
                    }
                }

                return null;
            }
        }

        public CompositionContract Contract { get; private set; }

        public IReadOnlyCollection<IImportSatisfiabilityConstraint> ExportContraints { get; private set; }

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
