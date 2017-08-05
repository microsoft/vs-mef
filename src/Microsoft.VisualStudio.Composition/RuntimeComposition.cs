// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.VisualStudio.Composition
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Collections.ObjectModel;
    using System.Diagnostics;
    using System.Globalization;
    using System.Linq;
    using System.Reflection;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.VisualStudio.Composition.Reflection;

    public class RuntimeComposition : IEquatable<RuntimeComposition>
    {
        private readonly ImmutableHashSet<RuntimePart> parts;
        private readonly IReadOnlyDictionary<TypeRef, RuntimePart> partsByType;
        private readonly IReadOnlyDictionary<string, IReadOnlyCollection<RuntimeExport>> exportsByContractName;
        private readonly IReadOnlyDictionary<TypeRef, RuntimeExport> metadataViewsAndProviders;

        private RuntimeComposition(IEnumerable<RuntimePart> parts, IReadOnlyDictionary<TypeRef, RuntimeExport> metadataViewsAndProviders, Resolver resolver)
        {
            Requires.NotNull(parts, nameof(parts));
            Requires.NotNull(metadataViewsAndProviders, nameof(metadataViewsAndProviders));
            Requires.NotNull(resolver, nameof(resolver));

            this.parts = ImmutableHashSet.CreateRange(parts);
            this.metadataViewsAndProviders = metadataViewsAndProviders;
            this.Resolver = resolver;

            this.partsByType = this.parts.ToDictionary(p => p.TypeRef, this.parts.Count);

            var exports =
                from part in this.parts
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

        public IReadOnlyDictionary<TypeRef, RuntimeExport> MetadataViewsAndProviders
        {
            get { return this.metadataViewsAndProviders; }
        }

        internal Resolver Resolver { get; }

        public static RuntimeComposition CreateRuntimeComposition(CompositionConfiguration configuration)
        {
            Requires.NotNull(configuration, nameof(configuration));

            // PERF/memory tip: We could create all RuntimeExports first, and then reuse them at each import site.
            var parts = configuration.Parts.Select(part => CreateRuntimePart(part, configuration));
            var metadataViewsAndProviders = ImmutableDictionary.CreateRange(
                from viewAndProvider in configuration.MetadataViewsAndProviders
                let viewTypeRef = TypeRef.Get(viewAndProvider.Key, configuration.Resolver)
                let runtimeExport = CreateRuntimeExport(viewAndProvider.Value, configuration.Resolver)
                select new KeyValuePair<TypeRef, RuntimeExport>(viewTypeRef, runtimeExport));
            return new RuntimeComposition(parts, metadataViewsAndProviders, configuration.Resolver);
        }

        public static RuntimeComposition CreateRuntimeComposition(IEnumerable<RuntimePart> parts, IReadOnlyDictionary<TypeRef, RuntimeExport> metadataViewsAndProviders, Resolver resolver)
        {
            return new RuntimeComposition(parts, metadataViewsAndProviders, resolver);
        }

        public IExportProviderFactory CreateExportProviderFactory()
        {
            return new RuntimeExportProviderFactory(this);
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
            Requires.NotNull(export, nameof(export));

            return this.partsByType[export.DeclaringTypeRef];
        }

        public RuntimePart GetPart(TypeRef partType)
        {
            Requires.NotNull(partType, nameof(partType));

            return this.partsByType[partType];
        }

        public override bool Equals(object obj)
        {
            return this.Equals(obj as RuntimeComposition);
        }

        public override int GetHashCode()
        {
            int hashCode = this.parts.Count;
            foreach (var part in this.parts)
            {
                hashCode += part.GetHashCode();
            }

            return hashCode;
        }

        public bool Equals(RuntimeComposition other)
        {
            if (other == null)
            {
                return false;
            }

            return this.parts.SetEquals(other.parts)
                && ByValueEquality.Dictionary<TypeRef, RuntimeExport>().Equals(this.metadataViewsAndProviders, other.metadataViewsAndProviders);
        }

        internal static string GetDiagnosticLocation(RuntimeImport import)
        {
            Requires.NotNull(import, nameof(import));

            return string.Format(
                CultureInfo.CurrentCulture,
                "{0}.{1}",
                import.DeclaringTypeRef.Resolve().FullName,
                import.ImportingMember == null ? ("ctor(" + import.ImportingParameter.Name + ")") : import.ImportingMember.Name);
        }

        internal static string GetDiagnosticLocation(RuntimeExport export)
        {
            Requires.NotNull(export, nameof(export));

            if (export.Member != null)
            {
                return string.Format(
                    CultureInfo.CurrentCulture,
                    "{0}.{1}",
                    export.DeclaringTypeRef.Resolve().FullName,
                    export.Member.Name);
            }
            else
            {
                return export.DeclaringTypeRef.Resolve().FullName;
            }
        }

        private static RuntimePart CreateRuntimePart(ComposedPart part, CompositionConfiguration configuration)
        {
            Requires.NotNull(part, nameof(part));

            var runtimePart = new RuntimePart(
                part.Definition.TypeRef,
                part.Definition.ImportingConstructorOrFactoryRef,
                part.GetImportingConstructorImports().Select(kvp => CreateRuntimeImport(kvp.Key, kvp.Value, part.Resolver)).ToImmutableArray(),
                part.Definition.ImportingMembers.Select(idb => CreateRuntimeImport(idb, part.SatisfyingExports[idb], part.Resolver)).ToImmutableArray(),
                part.Definition.ExportDefinitions.Select(ed => CreateRuntimeExport(ed.Value, part.Definition.Type, ed.Key, part.Resolver)).ToImmutableArray(),
                part.Definition.OnImportsSatisfiedRef,
                part.Definition.IsShared ? configuration.GetEffectiveSharingBoundary(part.Definition) : null);
            return runtimePart;
        }

        private static RuntimeImport CreateRuntimeImport(ImportDefinitionBinding importDefinitionBinding, IReadOnlyList<ExportDefinitionBinding> satisfyingExports, Resolver resolver)
        {
            Requires.NotNull(importDefinitionBinding, nameof(importDefinitionBinding));
            Requires.NotNull(satisfyingExports, nameof(satisfyingExports));

            var runtimeExports = satisfyingExports.Select(export => CreateRuntimeExport(export, resolver)).ToImmutableArray();
            if (!importDefinitionBinding.ImportingMemberRef.IsEmpty)
            {
                return new RuntimeImport(
                    importDefinitionBinding.ImportingMemberRef,
                    importDefinitionBinding.ImportingSiteTypeRef,
                    importDefinitionBinding.ImportDefinition.Cardinality,
                    runtimeExports,
                    PartCreationPolicyConstraint.IsNonSharedInstanceRequired(importDefinitionBinding.ImportDefinition),
                    importDefinitionBinding.IsExportFactory,
                    importDefinitionBinding.ImportDefinition.Metadata,
                    importDefinitionBinding.ImportDefinition.ExportFactorySharingBoundaries);
            }
            else
            {
                return new RuntimeImport(
                    importDefinitionBinding.ImportingParameterRef,
                    importDefinitionBinding.ImportingSiteTypeRef,
                    importDefinitionBinding.ImportDefinition.Cardinality,
                    runtimeExports,
                    PartCreationPolicyConstraint.IsNonSharedInstanceRequired(importDefinitionBinding.ImportDefinition),
                    importDefinitionBinding.IsExportFactory,
                    importDefinitionBinding.ImportDefinition.Metadata,
                    importDefinitionBinding.ImportDefinition.ExportFactorySharingBoundaries);
            }
        }

        private static RuntimeExport CreateRuntimeExport(ExportDefinition exportDefinition, Type partType, MemberRef exportingMemberRef, Resolver resolver)
        {
            Requires.NotNull(exportDefinition, nameof(exportDefinition));

            var exportingMember = exportingMemberRef.Resolve();
            return new RuntimeExport(
                exportDefinition.ContractName,
                TypeRef.Get(partType, resolver),
                exportingMemberRef,
                TypeRef.Get(ReflectionHelpers.GetExportedValueType(partType, exportingMember), resolver),
                exportDefinition.Metadata);
        }

        private static RuntimeExport CreateRuntimeExport(ExportDefinitionBinding exportDefinitionBinding, Resolver resolver)
        {
            Requires.NotNull(exportDefinitionBinding, nameof(exportDefinitionBinding));
            Requires.NotNull(resolver, nameof(resolver));

            var partDefinitionType = exportDefinitionBinding.PartDefinition.TypeRef.Resolve();
            return CreateRuntimeExport(
                exportDefinitionBinding.ExportDefinition,
                partDefinitionType,
                exportDefinitionBinding.ExportingMemberRef,
                resolver);
        }

        [DebuggerDisplay("{" + nameof(RuntimePart.TypeRef) + "." + nameof(Reflection.TypeRef.ResolvedType) + ".FullName,nq}")]
        public class RuntimePart : IEquatable<RuntimePart>
        {
            public RuntimePart(
                TypeRef type,
                MethodRef importingConstructor,
                IReadOnlyList<RuntimeImport> importingConstructorArguments,
                IReadOnlyList<RuntimeImport> importingMembers,
                IReadOnlyList<RuntimeExport> exports,
                MethodRef onImportsSatisfied,
                string sharingBoundary)
            {
                this.TypeRef = type;
                this.ImportingConstructorRef = importingConstructor;
                this.ImportingConstructorArguments = importingConstructorArguments;
                this.ImportingMembers = importingMembers;
                this.Exports = exports;
                this.OnImportsSatisfiedRef = onImportsSatisfied;
                this.SharingBoundary = sharingBoundary;
            }

            public TypeRef TypeRef { get; private set; }

            public MethodRef ImportingConstructorRef { get; private set; }

            public IReadOnlyList<RuntimeImport> ImportingConstructorArguments { get; private set; }

            public IReadOnlyList<RuntimeImport> ImportingMembers { get; private set; }

            public IReadOnlyList<RuntimeExport> Exports { get; set; }

            public MethodRef OnImportsSatisfiedRef { get; private set; }

            public string SharingBoundary { get; private set; }

            public bool IsShared
            {
                get { return this.SharingBoundary != null; }
            }

            public bool IsInstantiable
            {
                get { return !this.ImportingConstructorRef.IsEmpty; }
            }

            public MethodBase ImportingConstructor => this.ImportingConstructorRef.MethodBase;

            public MethodInfo OnImportsSatisfied => (MethodInfo)this.OnImportsSatisfiedRef.MethodBase;

            public override bool Equals(object obj)
            {
                return this.Equals(obj as RuntimePart);
            }

            public override int GetHashCode()
            {
                return this.TypeRef.GetHashCode();
            }

            public bool Equals(RuntimePart other)
            {
                if (other == null)
                {
                    return false;
                }

                bool result = this.TypeRef.Equals(other.TypeRef)
                    && this.ImportingConstructorRef.Equals(other.ImportingConstructorRef)
                    && this.ImportingConstructorArguments.SequenceEqual(other.ImportingConstructorArguments)
                    && ByValueEquality.EquivalentIgnoreOrder<RuntimeImport>().Equals(this.ImportingMembers, other.ImportingMembers)
                    && ByValueEquality.EquivalentIgnoreOrder<RuntimeExport>().Equals(this.Exports, other.Exports)
                    && this.OnImportsSatisfiedRef.Equals(other.OnImportsSatisfiedRef)
                    && this.SharingBoundary == other.SharingBoundary;
                return result;
            }
        }

        [DebuggerDisplay("{" + nameof(ImportingSiteElementType) + "}")]
        public class RuntimeImport : IEquatable<RuntimeImport>
        {
            private bool? isLazy;
            private Type importingSiteType;
            private Type importingSiteTypeWithoutCollection;
            private Type importingSiteElementType;
            private Func<Func<object>, object, object> lazyFactory;
            private ParameterInfo importingParameter;
            private MemberInfo importingMember;
            private volatile bool isMetadataTypeInitialized;
            private Type metadataType;

            private RuntimeImport(TypeRef importingSiteTypeRef, ImportCardinality cardinality, IReadOnlyList<RuntimeExport> satisfyingExports, bool isNonSharedInstanceRequired, bool isExportFactory, IReadOnlyDictionary<string, object> metadata, IReadOnlyCollection<string> exportFactorySharingBoundaries)
            {
                Requires.NotNull(importingSiteTypeRef, nameof(importingSiteTypeRef));
                Requires.NotNull(satisfyingExports, nameof(satisfyingExports));

                this.Cardinality = cardinality;
                this.SatisfyingExports = satisfyingExports;
                this.IsNonSharedInstanceRequired = isNonSharedInstanceRequired;
                this.IsExportFactory = isExportFactory;
                this.Metadata = metadata;
                this.ImportingSiteTypeRef = importingSiteTypeRef;
                this.ExportFactorySharingBoundaries = exportFactorySharingBoundaries;
            }

            public RuntimeImport(MemberRef importingMemberRef, TypeRef importingSiteTypeRef, ImportCardinality cardinality, IReadOnlyList<RuntimeExport> satisfyingExports, bool isNonSharedInstanceRequired, bool isExportFactory, IReadOnlyDictionary<string, object> metadata, IReadOnlyCollection<string> exportFactorySharingBoundaries)
                : this(importingSiteTypeRef, cardinality, satisfyingExports, isNonSharedInstanceRequired, isExportFactory, metadata, exportFactorySharingBoundaries)
            {
                this.ImportingMemberRef = importingMemberRef;
            }

            public RuntimeImport(ParameterRef importingParameterRef, TypeRef importingSiteTypeRef, ImportCardinality cardinality, IReadOnlyList<RuntimeExport> satisfyingExports, bool isNonSharedInstanceRequired, bool isExportFactory, IReadOnlyDictionary<string, object> metadata, IReadOnlyCollection<string> exportFactorySharingBoundaries)
                : this(importingSiteTypeRef, cardinality, satisfyingExports, isNonSharedInstanceRequired, isExportFactory, metadata, exportFactorySharingBoundaries)
            {
                this.ImportingParameterRef = importingParameterRef;
            }

            /// <summary>
            /// Gets the importing member. May be empty if the import site is an importing constructor parameter.
            /// </summary>
            public MemberRef ImportingMemberRef { get; private set; }

            /// <summary>
            /// Gets the importing parameter. May be empty if the import site is an importing field or property.
            /// </summary>
            public ParameterRef ImportingParameterRef { get; private set; }

            public TypeRef ImportingSiteTypeRef { get; private set; }

            public ImportCardinality Cardinality { get; private set; }

            public IReadOnlyCollection<RuntimeExport> SatisfyingExports { get; private set; }

            public bool IsExportFactory { get; private set; }

            public bool IsNonSharedInstanceRequired { get; private set; }

            public IReadOnlyDictionary<string, object> Metadata { get; private set; }

            public Type ExportFactory
            {
                get
                {
                    return this.IsExportFactory
                        ? this.ImportingSiteTypeWithoutCollection
                        : null;
                }
            }

            /// <summary>
            /// Gets the sharing boundaries created when the export factory is used.
            /// </summary>
            public IReadOnlyCollection<string> ExportFactorySharingBoundaries { get; private set; }

            public MemberInfo ImportingMember
            {
                get
                {
                    if (this.importingMember == null)
                    {
                        this.importingMember = this.ImportingMemberRef.Resolve();
                    }

                    return this.importingMember;
                }
            }

            public ParameterInfo ImportingParameter
            {
                get
                {
                    if (this.importingParameter == null)
                    {
                        this.importingParameter = this.ImportingParameterRef.Resolve();
                    }

                    return this.importingParameter;
                }
            }

            public bool IsLazy
            {
                get
                {
                    if (!this.isLazy.HasValue)
                    {
                        this.isLazy = this.ImportingSiteTypeWithoutCollection.IsAnyLazyType();
                    }

                    return this.isLazy.Value;
                }
            }

            public Type ImportingSiteType
            {
                get
                {
                    if (this.importingSiteType == null)
                    {
                        this.importingSiteType = this.ImportingSiteTypeRef.Resolve();
                    }

                    return this.importingSiteType;
                }
            }

            public Type ImportingSiteTypeWithoutCollection
            {
                get
                {
                    if (this.importingSiteTypeWithoutCollection == null)
                    {
                        this.importingSiteTypeWithoutCollection = this.Cardinality == ImportCardinality.ZeroOrMore
                            ? PartDiscovery.GetElementTypeFromMany(this.ImportingSiteType)
                            : this.ImportingSiteType;
                    }

                    return this.importingSiteTypeWithoutCollection;
                }
            }

            /// <summary>
            /// Gets the type of the member, with the ImportMany collection and Lazy/ExportFactory stripped off, when present.
            /// </summary>
            public Type ImportingSiteElementType
            {
                get
                {
                    if (this.importingSiteElementType == null)
                    {
                        this.importingSiteElementType = PartDiscovery.GetTypeIdentityFromImportingType(this.ImportingSiteType, this.Cardinality == ImportCardinality.ZeroOrMore);
                    }

                    return this.importingSiteElementType;
                }
            }

            public Type MetadataType
            {
                get
                {
                    if (!this.isMetadataTypeInitialized)
                    {
                        this.metadataType = this.IsLazy && this.ImportingSiteTypeWithoutCollection.GenericTypeArguments.Length == 2
                            ? this.ImportingSiteTypeWithoutCollection.GenericTypeArguments[1]
                            : null;
                        this.isMetadataTypeInitialized = true;
                    }

                    return this.metadataType;
                }
            }

            public TypeRef DeclaringTypeRef
            {
                get
                {
                    return
                        this.ImportingParameterRef.IsEmpty ? this.ImportingMemberRef.DeclaringType :
                        this.ImportingParameterRef.DeclaringType;
                }
            }

            internal Func<Func<object>, object, object> LazyFactory
            {
                get
                {
                    if (this.lazyFactory == null && this.IsLazy)
                    {
                        Type[] lazyTypeArgs = this.ImportingSiteTypeWithoutCollection.GenericTypeArguments;
                        this.lazyFactory = LazyServices.CreateStronglyTypedLazyFactory(this.ImportingSiteElementType, lazyTypeArgs.Length > 1 ? lazyTypeArgs[1] : null);
                    }

                    return this.lazyFactory;
                }
            }

            public override int GetHashCode()
            {
                return this.ImportingMemberRef.GetHashCode();
            }

            public override bool Equals(object obj)
            {
                return this.Equals(obj as RuntimeImport);
            }

            public bool Equals(RuntimeImport other)
            {
                if (other == null)
                {
                    return false;
                }

                bool result = EqualityComparer<TypeRef>.Default.Equals(this.ImportingSiteTypeRef, other.ImportingSiteTypeRef)
                    && this.Cardinality == other.Cardinality
                    && ByValueEquality.EquivalentIgnoreOrder<RuntimeExport>().Equals(this.SatisfyingExports, other.SatisfyingExports)
                    && this.IsNonSharedInstanceRequired == other.IsNonSharedInstanceRequired
                    && ByValueEquality.Metadata.Equals(this.Metadata, other.Metadata)
                    && ByValueEquality.EquivalentIgnoreOrder<string>().Equals(this.ExportFactorySharingBoundaries, other.ExportFactorySharingBoundaries)
                    && this.ImportingMemberRef.Equals(other.ImportingMemberRef)
                    && this.ImportingParameterRef.Equals(other.ImportingParameterRef);
                return result;
            }
        }

        public class RuntimeExport : IEquatable<RuntimeExport>
        {
            private MemberInfo member;

            public RuntimeExport(string contractName, TypeRef declaringTypeRef, MemberRef memberRef, TypeRef exportedValueTypeRef, IReadOnlyDictionary<string, object> metadata)
            {
                Requires.NotNull(metadata, nameof(metadata));
                Requires.NotNullOrEmpty(contractName, nameof(contractName));

                this.ContractName = contractName;
                this.DeclaringTypeRef = declaringTypeRef;
                this.MemberRef = memberRef;
                this.ExportedValueTypeRef = exportedValueTypeRef;
                this.Metadata = metadata;
            }

            public string ContractName { get; private set; }

            public TypeRef DeclaringTypeRef { get; private set; }

            public MemberRef MemberRef { get; private set; }

            public TypeRef ExportedValueTypeRef { get; private set; }

            public IReadOnlyDictionary<string, object> Metadata { get; private set; }

            public MemberInfo Member
            {
                get
                {
                    if (this.member == null)
                    {
                        this.member = this.MemberRef.Resolve();
                    }

                    return this.member;
                }
            }

            public override int GetHashCode()
            {
                return this.ContractName.GetHashCode() + this.DeclaringTypeRef.GetHashCode();
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

                bool result = this.ContractName == other.ContractName
                    && EqualityComparer<TypeRef>.Default.Equals(this.DeclaringTypeRef, other.DeclaringTypeRef)
                    && EqualityComparer<MemberRef>.Default.Equals(this.MemberRef, other.MemberRef)
                    && EqualityComparer<TypeRef>.Default.Equals(this.ExportedValueTypeRef, other.ExportedValueTypeRef)
                    && ByValueEquality.Metadata.Equals(this.Metadata, other.Metadata);
                return result;
            }
        }
    }
}
