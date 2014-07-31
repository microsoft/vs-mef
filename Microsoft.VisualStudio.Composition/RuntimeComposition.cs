namespace Microsoft.VisualStudio.Composition
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Collections.ObjectModel;
    using System.Linq;
    using System.Reflection;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.VisualStudio.Composition.Reflection;
    using Validation;

    public class RuntimeComposition
    {
        private readonly IReadOnlyCollection<RuntimePart> parts;
        private readonly IReadOnlyDictionary<TypeRef, RuntimePart> partsByType;
        private readonly IReadOnlyDictionary<string, IReadOnlyCollection<RuntimeExport>> exportsByContractName;

        private RuntimeComposition(IEnumerable<RuntimePart> parts)
        {
            Requires.NotNull(parts, "parts");

            this.parts = ImmutableHashSet.CreateRange(parts);
            this.partsByType = this.parts.ToDictionary(p => p.Type);

            var exports =
                from part in this.parts
                where part.IsInstantiable // TODO: why are we limiting these to instantiable ones? Why not make static exports available?
                from export in part.Exports
                group export by export.ContractName into exportsByContract
                select exportsByContract;
            this.exportsByContractName = exports.ToDictionary(
                e => e.Key,
                e => (IReadOnlyCollection<RuntimeExport>)e.ToImmutableArray());
        }

        public IReadOnlyCollection<RuntimePart> Parts
        {
            get { return this.parts; }
        }

        public static RuntimeComposition CreateRuntimeComposition(CompositionConfiguration configuration)
        {
            Requires.NotNull(configuration, "configuration");

            // TODO: create all RuntimeExports first, and then reuse them at each import site.
            var parts = configuration.Parts.Select(part => CreateRuntimePart(part, configuration));
            return new RuntimeComposition(parts);
        }

        public static RuntimeComposition CreateRuntimeComposition(IEnumerable<RuntimePart> parts)
        {
            return new RuntimeComposition(parts);
        }

        public IReadOnlyCollection<RuntimeExport> GetExports(string contractName)
        {
            IReadOnlyCollection<RuntimeExport> exports;
            if (this.exportsByContractName.TryGetValue(contractName, out exports))
            {
                return exports;
            }

            return ImmutableList<RuntimeExport>.Empty;
        }

        public RuntimePart GetPart(RuntimeExport export)
        {
            return this.partsByType[export.DeclaringType];
        }

        private static RuntimePart CreateRuntimePart(ComposedPart part, CompositionConfiguration configuration)
        {
            Requires.NotNull(part, "part");

            var runtimePart = new RuntimePart(
                TypeRef.Get(part.Definition.Type),
                part.Definition.ImportingConstructorInfo != null ? new ConstructorRef(part.Definition.ImportingConstructorInfo) : default(ConstructorRef),
                part.GetImportingConstructorImports().Select(kvp => CreateRuntimeImport(kvp.Key, kvp.Value)).ToImmutableArray(),
                part.Definition.ImportingMembers.Select(idb => CreateRuntimeImport(idb, part.SatisfyingExports[idb])).ToImmutableArray(),
                part.Definition.ExportDefinitions.Select(ed => CreateRuntimeExport(ed.Value, part.Definition.Type, ed.Key)).ToImmutableArray(),
                part.Definition.OnImportsSatisfied != null ? new MethodRef(part.Definition.OnImportsSatisfied) : new MethodRef(),
                part.Definition.IsShared ? configuration.GetEffectiveSharingBoundary(part.Definition) : null);
            return runtimePart;
        }

        private static RuntimeImport CreateRuntimeImport(ImportDefinitionBinding importDefinitionBinding, IReadOnlyList<ExportDefinitionBinding> satisfyingExports)
        {
            Requires.NotNull(importDefinitionBinding, "importDefinitionBinding");
            Requires.NotNull(satisfyingExports, "satisfyingExports");

            var runtimeExports = satisfyingExports.Select(export => CreateRuntimeExport(export.ExportDefinition, export.PartDefinition.Type, export.ExportingMember)).ToImmutableArray();
            if (importDefinitionBinding.ImportingMember != null)
            {
                return new RuntimeImport(
                    new MemberRef(importDefinitionBinding.ImportingMember),
                    importDefinitionBinding.ImportDefinition.Cardinality,
                    runtimeExports,
                    PartCreationPolicyConstraint.IsNonSharedInstanceRequired(importDefinitionBinding.ImportDefinition),
                    importDefinitionBinding.ImportDefinition.Metadata,
                    TypeRef.Get(importDefinitionBinding.ExportFactoryType),
                    importDefinitionBinding.ImportDefinition.ExportFactorySharingBoundaries);
            }
            else
            {
                return new RuntimeImport(
                    new ParameterRef(importDefinitionBinding.ImportingParameter),
                    importDefinitionBinding.ImportDefinition.Cardinality,
                    runtimeExports,
                    PartCreationPolicyConstraint.IsNonSharedInstanceRequired(importDefinitionBinding.ImportDefinition),
                    importDefinitionBinding.ImportDefinition.Metadata,
                    TypeRef.Get(importDefinitionBinding.ExportFactoryType),
                    importDefinitionBinding.ImportDefinition.ExportFactorySharingBoundaries);
            }
        }

        private static RuntimeExport CreateRuntimeExport(ExportDefinition exportDefinition, Type partType, MemberInfo exportingMember)
        {
            Requires.NotNull(exportDefinition, "exportDefinition");

            return new RuntimeExport(
                exportDefinition.ContractName,
                TypeRef.Get(partType),
                exportingMember != null ? new MemberRef(exportingMember) : default(MemberRef),
                TypeRef.Get(ReflectionHelpers.GetExportedValueType(partType, exportingMember)),
                exportDefinition.Metadata);
        }

        public class RuntimePart
        {
            public RuntimePart(
                TypeRef type,
                ConstructorRef importingConstructor,
                IReadOnlyList<RuntimeImport> importingConstructorArguments,
                IReadOnlyList<RuntimeImport> importingMembers,
                IReadOnlyList<RuntimeExport> exports,
                MethodRef onImportsSatisfied,
                string sharingBoundary)
            {
                this.Type = type;
                this.ImportingConstructor = importingConstructor;
                this.ImportingConstructorArguments = importingConstructorArguments;
                this.ImportingMembers = importingMembers;
                this.Exports = exports;
                this.OnImportsSatisfied = onImportsSatisfied;
                this.SharingBoundary = sharingBoundary;
            }

            public TypeRef Type { get; private set; }

            public ConstructorRef ImportingConstructor { get; private set; }

            public IReadOnlyList<RuntimeImport> ImportingConstructorArguments { get; private set; }

            public IReadOnlyList<RuntimeImport> ImportingMembers { get; private set; }

            public IReadOnlyList<RuntimeExport> Exports { get; set; }

            public MethodRef OnImportsSatisfied { get; private set; }

            public string SharingBoundary { get; private set; }

            public bool IsShared
            {
                get { return this.SharingBoundary != null; }
            }

            public bool IsInstantiable
            {
                get { return !this.ImportingConstructor.IsEmpty; }
            }
        }

        public class RuntimeImport
        {
            private RuntimeImport(ImportCardinality cardinality, IReadOnlyList<RuntimeExport> satisfyingExports, bool isNonSharedInstanceRequired, IReadOnlyDictionary<string, object> metadata, TypeRef exportFactory, IReadOnlyCollection<string> exportFactorySharingBoundaries)
            {
                Requires.NotNull(satisfyingExports, "satisfyingExports");

                this.Cardinality = cardinality;
                this.SatisfyingExports = satisfyingExports;
                this.IsNonSharedInstanceRequired = isNonSharedInstanceRequired;
                this.Metadata = metadata;
                this.ExportFactory = exportFactory;
                this.ExportFactorySharingBoundaries = exportFactorySharingBoundaries;
            }

            public RuntimeImport(MemberRef importingMember, ImportCardinality cardinality, IReadOnlyList<RuntimeExport> satisfyingExports, bool isNonSharedInstanceRequired, IReadOnlyDictionary<string, object> metadata, TypeRef exportFactory, IReadOnlyCollection<string> exportFactorySharingBoundaries)
                : this(cardinality, satisfyingExports, isNonSharedInstanceRequired, metadata, exportFactory, exportFactorySharingBoundaries)
            {
                this.ImportingMemberRef = importingMember;
            }

            public RuntimeImport(ParameterRef importingParameter, ImportCardinality cardinality, IReadOnlyList<RuntimeExport> satisfyingExports, bool isNonSharedInstanceRequired, IReadOnlyDictionary<string, object> metadata, TypeRef exportFactory, IReadOnlyCollection<string> exportFactorySharingBoundaries)
                : this(cardinality, satisfyingExports, isNonSharedInstanceRequired, metadata, exportFactory, exportFactorySharingBoundaries)
            {
                this.ImportingParameterRef = importingParameter;
            }

            /// <summary>
            /// Gets the importing member. May be empty if the import site is an importing constructor parameter.
            /// </summary>
            public MemberRef ImportingMemberRef { get; private set; }

            /// <summary>
            /// Gets the importing parameter. May be empty if the import site is an importing field or property.
            /// </summary>
            public ParameterRef ImportingParameterRef { get; private set; }

            public ImportCardinality Cardinality { get; private set; }

            public IReadOnlyList<RuntimeExport> SatisfyingExports { get; private set; }

            public bool IsNonSharedInstanceRequired { get; private set; }

            public IReadOnlyDictionary<string, object> Metadata { get; private set; }

            public TypeRef ExportFactory { get; private set; }

            /// <summary>
            /// Gets the sharing boundaries created when the export factory is used.
            /// </summary>
            public IReadOnlyCollection<string> ExportFactorySharingBoundaries { get; private set; }

            public bool IsExportFactory
            {
                get { return this.ExportFactory != null; }
            }

            public bool IsLazy
            {
                get { return this.ImportingSiteTypeWithoutCollection.IsAnyLazyType(); }
            }

            public Type ImportingSiteType
            {
                get
                {
                    if (!this.ImportingParameterRef.IsEmpty)
                    {
                        return this.ImportingParameterRef.Resolve().ParameterType;
                    }

                    if (this.ImportingMemberRef.IsField)
                    {
                        return this.ImportingMemberRef.Field.Resolve().FieldType;
                    }

                    if (this.ImportingMemberRef.IsProperty)
                    {
                        return this.ImportingMemberRef.Property.Resolve().PropertyType;
                    }

                    throw new NotSupportedException();
                }
            }

            public Type ImportingSiteTypeWithoutCollection
            {
                get
                {
                    return this.Cardinality == ImportCardinality.ZeroOrMore
                        ? PartDiscovery.GetElementTypeFromMany(this.ImportingSiteType)
                        : this.ImportingSiteType;
                }
            }

            /// <summary>
            /// Gets the type of the member, with the ImportMany collection and Lazy/ExportFactory stripped off, when present.
            /// </summary>
            public Type ImportingSiteElementType
            {
                get
                {
                    return PartDiscovery.GetTypeIdentityFromImportingType(this.ImportingSiteType, this.Cardinality == ImportCardinality.ZeroOrMore);
                }
            }

            public Type DeclaringType
            {
                get
                {
                    return
                        this.ImportingMemberRef.IsField ? this.ImportingMemberRef.Field.Resolve().DeclaringType :
                        this.ImportingMemberRef.IsProperty ? this.ImportingMemberRef.Property.Resolve().DeclaringType :
                        this.ImportingParameterRef.Resolve().Member.DeclaringType;
                }
            }
        }

        public class RuntimeExport : IEquatable<RuntimeExport>
        {
            public RuntimeExport(string contractName, TypeRef declaringType, MemberRef member, TypeRef exportedValueType, IReadOnlyDictionary<string, object> metadata)
            {
                Requires.NotNull(metadata, "metadata");
                Requires.NotNullOrEmpty(contractName, "contractName");

                this.ContractName = contractName;
                this.DeclaringType = declaringType;
                this.Member = member;
                this.ExportedValueType = exportedValueType;
                this.Metadata = metadata;
            }

            public string ContractName { get; private set; }

            public TypeRef DeclaringType { get; private set; }

            public MemberRef Member { get; private set; }

            public TypeRef ExportedValueType { get; private set; }

            public IReadOnlyDictionary<string, object> Metadata { get; private set; }

            public override int GetHashCode()
            {
                return this.ContractName.GetHashCode() + this.DeclaringType.GetHashCode();
            }

            public override bool Equals(object obj)
            {
                return this.Equals(obj as RuntimeExport);
            }

            public bool Equals(RuntimeExport other)
            {
                if (other == null)
                {
                    return false;
                }

                return this.ContractName == other.ContractName
                    && EqualityComparer<TypeRef>.Default.Equals(this.DeclaringType, other.DeclaringType)
                    && EqualityComparer<MemberRef>.Default.Equals(this.Member, other.Member)
                    && EqualityComparer<TypeRef>.Default.Equals(this.ExportedValueType, other.ExportedValueType)
                    && ByValueEquality.Metadata.Equals(this.Metadata, other.Metadata);
            }
        }
    }
}
