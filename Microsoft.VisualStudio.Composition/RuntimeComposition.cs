namespace Microsoft.VisualStudio.Composition
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Linq;
    using System.Reflection;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.VisualStudio.Composition.Reflection;
    using Validation;

    public class RuntimeComposition
    {
        private readonly IReadOnlyDictionary<string, IReadOnlyCollection<RuntimeExport>> exportsByContractName;

        private readonly IReadOnlyDictionary<int, RuntimePart> partsBySurrogate;

        private RuntimeComposition(IReadOnlyDictionary<string, IReadOnlyCollection<RuntimeExport>> exportsByContractName, IReadOnlyDictionary<int, RuntimePart> partsBySurrogate)
        {
            Requires.NotNull(exportsByContractName, "exportsByContractName");
            Requires.NotNull(partsBySurrogate, "partsBySurrogate");

            this.exportsByContractName = exportsByContractName;
            this.partsBySurrogate = partsBySurrogate;
        }

        public static RuntimeComposition CreateRuntimeComposition(CompositionConfiguration configuration)
        {
            Requires.NotNull(configuration, "configuration");

            int surrogateId = 0;
            IReadOnlyDictionary<ComposablePartDefinition, int> partSurrogates = configuration.Parts.ToDictionary(part => part.Definition, part => ++surrogateId);

            var exports =
                from part in configuration.Parts
                where part.Definition.IsInstantiable // TODO: why are we limiting these to instantiable ones? Why not make static exports available?
                from exportingMemberAndDefinition in part.Definition.ExportDefinitions
                let exportDefinitionBinding = new ExportDefinitionBinding(exportingMemberAndDefinition.Value, part.Definition, exportingMemberAndDefinition.Key)
                let runtimeExport = CreateRuntimeExport(exportDefinitionBinding, partSurrogates)
                group runtimeExport by runtimeExport.ContractName into exportsByContract
                select exportsByContract;
            var runtimeExportsByContract = exports.ToDictionary(e => e.Key, e => (IReadOnlyCollection<RuntimeExport>)e.ToImmutableArray());

            var partsBySurrogateQuery =
                from composedPart in configuration.Parts
                let surrogate = partSurrogates[composedPart.Definition]
                let runtimePart = CreateRuntimePart(composedPart, configuration, partSurrogates)
                select new KeyValuePair<int, RuntimePart>(surrogate, runtimePart);
            var partsBySurrogate = partsBySurrogateQuery.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
            return new RuntimeComposition(runtimeExportsByContract, partsBySurrogate);
        }

        internal IReadOnlyCollection<RuntimeExport> GetExports(string contractName)
        {
            IReadOnlyCollection<RuntimeExport> exports;
            if (this.exportsByContractName.TryGetValue(contractName, out exports))
            {
                return exports;
            }

            return ImmutableList<RuntimeExport>.Empty;
        }

        internal RuntimePart GetPart(int partSurrogate)
        {
            return this.partsBySurrogate[partSurrogate];
        }

        private static RuntimePart CreateRuntimePart(ComposedPart part, CompositionConfiguration configuration, IReadOnlyDictionary<ComposablePartDefinition, int> partSurrogates)
        {
            Requires.NotNull(part, "part");

            var runtimePart = new RuntimePart(
                new TypeRef(part.Definition.Type),
                part.Definition.ImportingConstructorInfo != null ? new ConstructorRef(part.Definition.ImportingConstructorInfo) : default(ConstructorRef),
                part.GetImportingConstructorImports().Select(kvp => CreateRuntimeImport(kvp.Key, kvp.Value, partSurrogates)).ToImmutableArray(),
                part.Definition.ImportingMembers.Select(idb => CreateRuntimeImport(idb, part.SatisfyingExports[idb], partSurrogates)).ToImmutableArray(),
                part.Definition.OnImportsSatisfied != null ? new MethodRef(part.Definition.OnImportsSatisfied) : new MethodRef(),
                part.Definition.IsShared ? configuration.GetEffectiveSharingBoundary(part.Definition) : null);
            return runtimePart;
        }

        private static RuntimeImport CreateRuntimeImport(ImportDefinitionBinding importDefinitionBinding, IReadOnlyList<ExportDefinitionBinding> satisfyingExports, IReadOnlyDictionary<ComposablePartDefinition, int> partSurrogates)
        {
            Requires.NotNull(importDefinitionBinding, "importDefinitionBinding");
            Requires.NotNull(satisfyingExports, "satisfyingExports");
            Requires.NotNull(partSurrogates, "partSurrogates");

            var runtimeExports = satisfyingExports.Select(export => CreateRuntimeExport(export, partSurrogates)).ToImmutableArray();
            if (importDefinitionBinding.ImportingMember != null)
            {
                return new RuntimeImport(
                    new MemberRef(importDefinitionBinding.ImportingMember),
                    importDefinitionBinding.ImportDefinition.Cardinality,
                    runtimeExports,
                    PartCreationPolicyConstraint.IsNonSharedInstanceRequired(importDefinitionBinding.ImportDefinition),
                    importDefinitionBinding.ImportDefinition.Metadata,
                    importDefinitionBinding.IsExportFactory ? new TypeRef(importDefinitionBinding.ExportFactoryType) : new TypeRef(),
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
                    importDefinitionBinding.IsExportFactory ? new TypeRef(importDefinitionBinding.ExportFactoryType) : new TypeRef(),
                    importDefinitionBinding.ImportDefinition.ExportFactorySharingBoundaries);
            }
        }

        private static RuntimeExport CreateRuntimeExport(ExportDefinitionBinding exportDefinitionBinding, IReadOnlyDictionary<ComposablePartDefinition, int> partSurrogates)
        {
            Requires.NotNull(exportDefinitionBinding, "exportDefinitionBinding");

            return new RuntimeExport(
                partSurrogates[exportDefinitionBinding.PartDefinition],
                exportDefinitionBinding.ExportDefinition.ContractName,
                exportDefinitionBinding.ExportingMember != null ? new MemberRef(exportDefinitionBinding.ExportingMember) : default(MemberRef),
                new TypeRef(exportDefinitionBinding.ExportedValueType),
                exportDefinitionBinding.ExportDefinition.Metadata);
        }

        internal class RuntimePart
        {
            public RuntimePart(
                TypeRef type,
                ConstructorRef importingConstructor,
                IReadOnlyList<RuntimeImport> importingConstructorArguments,
                IReadOnlyList<RuntimeImport> importingMembers,
                MethodRef onImportsSatisfied,
                string sharingBoundary)
            {
                this.Type = type;
                this.ImportingConstructor = importingConstructor;
                this.ImportingConstructorArguments = importingConstructorArguments;
                this.ImportingMembers = importingMembers;
                this.OnImportsSatisfied = onImportsSatisfied;
                this.SharingBoundary = sharingBoundary;
            }

            public TypeRef Type { get; private set; }

            public ConstructorRef ImportingConstructor { get; private set; }

            public IReadOnlyList<RuntimeImport> ImportingConstructorArguments { get; private set; }

            public IReadOnlyList<RuntimeImport> ImportingMembers { get; private set; }

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

        internal class RuntimeImport
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
                get { return !(this.ExportFactory == null || this.ExportFactory.IsEmpty); }
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

        internal class RuntimeExport
        {
            public RuntimeExport(int partSurrogate, string contractName, MemberRef member, TypeRef exportedValueType, IReadOnlyDictionary<string, object> metadata)
            {
                Requires.NotNull(metadata, "metadata");
                Requires.NotNullOrEmpty(contractName, "contractName");

                this.PartSurrogate = partSurrogate;
                this.ContractName = contractName;
                this.Member = member;
                this.ExportedValueType = exportedValueType;
                this.Metadata = metadata;
            }

            public int PartSurrogate { get; private set; }

            public string ContractName { get; private set; }

            public MemberRef Member { get; private set; }

            public TypeRef ExportedValueType { get; private set; }

            public IReadOnlyDictionary<string, object> Metadata { get; private set; }
        }
    }
}
