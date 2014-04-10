namespace Microsoft.VisualStudio.Composition
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Diagnostics;
    using System.Linq;
    using System.Reflection;
    using System.Text;
    using System.Threading.Tasks;
    using Validation;
    
    [DebuggerDisplay("{Contract.Type.Name,nq} (Lazy: {IsLazy}, {Cardinality})")]
    public class ImportDefinition : IEquatable<ImportDefinition>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ImportDefinition"/> class
        /// based on MEF v2 attributes.
        /// </summary>
        public ImportDefinition(CompositionContract contract, ImportCardinality cardinality, Type memberType, IReadOnlyCollection<IImportSatisfiabilityConstraint> additionalConstraints, IReadOnlyCollection<string> exportFactorySharingBoundaries)
        {
            Requires.NotNull(contract, "contract");
            Requires.NotNull(memberType, "memberType");
            Requires.NotNull(additionalConstraints, "additionalConstraints");
            Requires.NotNull(exportFactorySharingBoundaries, "exportFactorySharingBoundaries");

            this.Contract = contract;
            this.Cardinality = cardinality;
            this.MemberType = memberType;
            this.ExportContraints = additionalConstraints;
            this.RequiredCreationPolicy = CreationPolicy.Any;
            this.ExportFactorySharingBoundaries = exportFactorySharingBoundaries;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ImportDefinition"/> class
        /// based on MEF v1 attributes.
        /// </summary>
        public ImportDefinition(CompositionContract contract, ImportCardinality cardinality, Type memberType, IReadOnlyCollection<IImportSatisfiabilityConstraint> additionalConstraints, CreationPolicy requiredCreationPolicy)
            : this(contract, cardinality, memberType, additionalConstraints, ImmutableHashSet.Create<string>())
        {
            this.RequiredCreationPolicy = requiredCreationPolicy;
        }

        public ImportCardinality Cardinality { get; private set; }

        public CreationPolicy RequiredCreationPolicy { get; private set; }

        /// <summary>
        /// Gets the literal declared type of this member.
        /// </summary>
        public Type MemberType { get; private set; }

        public Type MemberWithoutManyWrapper
        {
            get
            {
                return this.Cardinality == ImportCardinality.ZeroOrMore
                    ? PartDiscovery.GetElementTypeFromMany(this.MemberType)
                    : this.MemberType;
            }
        }

        public Type ElementType
        {
            get
            {
                return PartDiscovery.GetElementFromImportingMemberType(this.MemberType, this.Cardinality == ImportCardinality.ZeroOrMore);
            }
        }

        public bool IsLazy
        {
            get { return this.MemberWithoutManyWrapper.IsAnyLazyType(); }
        }

        public bool IsLazyConcreteType
        {
            get { return this.MemberWithoutManyWrapper.IsConcreteLazyType(); }
        }

        public Type LazyType
        {
            get { return this.IsLazy ? this.MemberWithoutManyWrapper : null; }
        }

        public bool IsExportFactory
        {
            get { return this.MemberWithoutManyWrapper.IsExportFactoryTypeV1() || this.MemberWithoutManyWrapper.IsExportFactoryTypeV2(); }
        }

        public Type ExportFactoryType
        {
            get { return this.IsExportFactory ? this.MemberWithoutManyWrapper : null; }
        }

        /// <summary>
        /// Gets the sharing boundaries created when the export factory is used.
        /// </summary>
        public IReadOnlyCollection<string> ExportFactorySharingBoundaries { get; private set; }

        public Type MetadataType
        {
            get
            {
                if (this.IsLazy || this.IsExportFactory)
                {
                    var args = this.MemberWithoutManyWrapper.GetTypeInfo().GenericTypeArguments;
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
        /// Gets the actual type (without the Lazy{T} or ExportFactory{T} or collection wrappers) of the imported value.
        /// </summary>
        public Type CoercedValueType
        {
            get
            {
                // MEF v2 only allows for this to match the contract itself. MEF v1 was more flexible.
                return this.ElementType;
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
