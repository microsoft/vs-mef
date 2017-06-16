// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.VisualStudio.Composition
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Composition;
    using System.Linq;
    using System.Reflection;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.VisualStudio.Composition.Reflection;

    public class AttributedPartDiscovery : PartDiscovery
    {
        public AttributedPartDiscovery(Resolver resolver, bool isNonPublicSupported = false)
            : base(resolver)
        {
            this.IsNonPublicSupported = isNonPublicSupported;
        }

        /// <summary>
        /// Gets a value indicating whether non-public types and members will be explored.
        /// </summary>
        /// <remarks>
        /// The Microsoft.Composition NuGet package ignores non-publics.
        /// </remarks>
        public bool IsNonPublicSupported { get; }

        /// <summary>
        /// Gets the flags that select just public members or public and non-public as appropriate.
        /// </summary>
        protected BindingFlags PublicVsNonPublicFlags
        {
            get
            {
                var baseline = BindingFlags.Public;
                if (this.IsNonPublicSupported)
                {
                    baseline |= BindingFlags.NonPublic;
                }

                return baseline;
            }
        }

        protected override ComposablePartDefinition CreatePart(Type partType, bool typeExplicitlyRequested)
        {
            Requires.NotNull(partType, nameof(partType));

            var partTypeInfo = partType.GetTypeInfo();
            if (!typeExplicitlyRequested)
            {
                bool isPublic = partType.IsNested ? partTypeInfo.IsNestedPublic : partTypeInfo.IsPublic;
                if (!this.IsNonPublicSupported && !isPublic)
                {
                    // Skip non-public types.
                    return null;
                }
            }

            var declaredProperties = partTypeInfo.GetProperties(BindingFlags.Instance | this.PublicVsNonPublicFlags);
            var exportingProperties = from member in declaredProperties
                                      from export in member.GetAttributes<ExportAttribute>()
                                      select new KeyValuePair<MemberInfo, ExportAttribute>(member, export);
            var exportedTypes = from export in partTypeInfo.GetAttributes<ExportAttribute>()
                                select new KeyValuePair<MemberInfo, ExportAttribute>(partTypeInfo, export);
            var exportsByMember = (from export in exportingProperties.Concat(exportedTypes)
                                   group export.Value by export.Key into exportsByType
                                   select exportsByType).Select(g => new KeyValuePair<MemberInfo, ExportAttribute[]>(g.Key, g.ToArray())).ToArray();

            if (exportsByMember.Length == 0)
            {
                return null;
            }

            // Check for PartNotDiscoverable only after we've established it's an interesting part.
            // This optimizes for the fact that most types have no exports, in which case it's not a discoverable
            // part anyway. Checking for the PartNotDiscoverableAttribute first, which is rarely defined,
            // doesn't usually pay for itself in terms of short-circuiting. But it does add an extra
            // attribute to look for that we don't need to find for all the types that have no export attributes either.
            if (!typeExplicitlyRequested && partTypeInfo.IsAttributeDefined<PartNotDiscoverableAttribute>())
            {
                return null;
            }

            TypeRef partTypeRef = TypeRef.Get(partType, this.Resolver);
            Type partTypeAsGenericTypeDefinition = partTypeInfo.IsGenericType ? partType.GetGenericTypeDefinition() : null;

            string sharingBoundary = null;
            var sharedAttribute = partTypeInfo.GetFirstAttribute<SharedAttribute>();
            if (sharedAttribute != null)
            {
                sharingBoundary = sharedAttribute.SharingBoundary ?? string.Empty;
            }

            CreationPolicy partCreationPolicy = sharingBoundary != null ? CreationPolicy.Shared : CreationPolicy.NonShared;
            var allExportsMetadata = ImmutableDictionary.CreateRange(PartCreationPolicyConstraint.GetExportMetadata(partCreationPolicy));

            var exportsOnType = ImmutableList.CreateBuilder<ExportDefinition>();
            var exportsOnMembers = ImmutableDictionary.CreateBuilder<MemberRef, IReadOnlyCollection<ExportDefinition>>();
            var imports = ImmutableList.CreateBuilder<ImportDefinitionBinding>();

            foreach (var export in exportsByMember)
            {
                var member = export.Key;
                var memberExportMetadata = allExportsMetadata.AddRange(this.GetExportMetadata(member));

                if (member is TypeInfo)
                {
                    foreach (var exportAttribute in export.Value)
                    {
                        Type exportedType = exportAttribute.ContractType ?? partTypeAsGenericTypeDefinition ?? partType;
                        ExportDefinition exportDefinition = CreateExportDefinition(memberExportMetadata, exportAttribute, exportedType);
                        exportsOnType.Add(exportDefinition);
                    }
                }
                else // property
                {
                    var property = (PropertyInfo)member;
                    Verify.Operation(!partTypeInfo.IsGenericTypeDefinition, Strings.ExportsOnMembersNotAllowedWhenDeclaringTypeGeneric);
                    var exportDefinitions = ImmutableList.CreateBuilder<ExportDefinition>();
                    foreach (var exportAttribute in export.Value)
                    {
                        Type exportedType = exportAttribute.ContractType ?? property.PropertyType;
                        ExportDefinition exportDefinition = CreateExportDefinition(memberExportMetadata, exportAttribute, exportedType);
                        exportDefinitions.Add(exportDefinition);
                    }

                    exportsOnMembers.Add(MemberRef.Get(member, this.Resolver), exportDefinitions.ToImmutable());
                }
            }

            foreach (var member in declaredProperties)
            {
                var importAttribute = member.GetFirstAttribute<ImportAttribute>();
                var importManyAttribute = member.GetFirstAttribute<ImportManyAttribute>();
                Requires.Argument(!(importAttribute != null && importManyAttribute != null), "partType", Strings.MemberContainsBothImportAndImportMany, member.Name);

                var importConstraints = GetImportConstraints(member);
                ImportDefinition importDefinition;
                if (this.TryCreateImportDefinition(ReflectionHelpers.GetMemberType(member), member, importConstraints, out importDefinition))
                {
                    imports.Add(new ImportDefinitionBinding(importDefinition, TypeRef.Get(partType, this.Resolver), MemberRef.Get(member, this.Resolver)));
                }
            }

            MethodInfo onImportsSatisfied = null;
            foreach (var method in partTypeInfo.GetMethods(this.PublicVsNonPublicFlags | BindingFlags.Instance))
            {
                if (method.IsAttributeDefined<OnImportsSatisfiedAttribute>())
                {
                    Verify.Operation(method.GetParameters().Length == 0, Strings.OnImportsSatisfiedTakeNoParameters);
                    Verify.Operation(onImportsSatisfied == null, Strings.OnlyOneOnImportsSatisfiedMethodIsSupported);
                    onImportsSatisfied = method;
                }
            }

            var importingConstructorParameters = ImmutableList.CreateBuilder<ImportDefinitionBinding>();
            var importingCtor = GetImportingConstructor<ImportingConstructorAttribute>(partType, publicOnly: !this.IsNonPublicSupported);
            Verify.Operation(importingCtor != null, Strings.NoImportingConstructorFound);
            foreach (var parameter in importingCtor.GetParameters())
            {
                var import = this.CreateImport(parameter, GetImportConstraints(parameter));
                if (import.ImportDefinition.Cardinality == ImportCardinality.ZeroOrMore)
                {
                    Verify.Operation(PartDiscovery.IsImportManyCollectionTypeCreateable(import), Strings.CollectionMustBePublicAndPublicCtorWhenUsingImportingCtor);
                }

                importingConstructorParameters.Add(import);
            }

            var partMetadata = ImmutableDictionary.CreateBuilder<string, object>();
            foreach (var partMetadataAttribute in partTypeInfo.GetAttributes<PartMetadataAttribute>())
            {
                partMetadata[partMetadataAttribute.Name] = partMetadataAttribute.Value;
            }

            var assemblyNamesForMetadataAttributes = ImmutableHashSet.CreateBuilder<AssemblyName>();
            foreach (var export in exportsByMember)
            {
                GetAssemblyNamesFromMetadataAttributes<MetadataAttributeAttribute>(export.Key, assemblyNamesForMetadataAttributes);
            }

            return new ComposablePartDefinition(
                TypeRef.Get(partType, this.Resolver),
                partMetadata.ToImmutable(),
                exportsOnType.ToImmutable(),
                exportsOnMembers.ToImmutable(),
                imports.ToImmutable(),
                sharingBoundary,
                MethodRef.Get(onImportsSatisfied, this.Resolver),
                ConstructorRef.Get(importingCtor, this.Resolver),
                importingConstructorParameters.ToImmutable(),
                partCreationPolicy,
                assemblyNamesForMetadataAttributes);
        }

        public override bool IsExportFactoryType(Type type)
        {
            if (type != null && type.GetTypeInfo().IsGenericType)
            {
                var typeDefinition = type.GetGenericTypeDefinition();
                if (typeDefinition.Equals(typeof(ExportFactory<>)) || typeDefinition.Equals(typeof(ExportFactory<,>)))
                {
                    return true;
                }
            }

            return false;
        }

        protected override IEnumerable<Type> GetTypes(Assembly assembly)
        {
            Requires.NotNull(assembly, nameof(assembly));

            return this.IsNonPublicSupported ? assembly.GetTypes() : assembly.GetExportedTypes();
        }

        private ImmutableDictionary<string, object> GetExportMetadata(ICustomAttributeProvider member)
        {
            Requires.NotNull(member, nameof(member));

            var result = ImmutableDictionary.CreateBuilder<string, object>();
            var namesOfMetadataWithMultipleValues = new HashSet<string>(StringComparer.Ordinal);
            foreach (var attribute in member.GetAttributes<Attribute>())
            {
                var attrType = attribute.GetType().GetTypeInfo();
                var exportMetadataAttribute = attribute as ExportMetadataAttribute;
                if (exportMetadataAttribute != null)
                {
                    UpdateMetadataDictionary(result, namesOfMetadataWithMultipleValues, exportMetadataAttribute.Name, exportMetadataAttribute.Value, null);
                }
                else
                {
                    // Perf optimization, relies on short circuit evaluation, often a property attribute is an ExportAttribute
                    if (attrType != typeof(ExportAttribute).GetTypeInfo() && attrType.IsAttributeDefined<MetadataAttributeAttribute>())
                    {
                        var properties = attrType.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                        foreach (var property in properties.Where(p => p.DeclaringType != typeof(Attribute)))
                        {
                            UpdateMetadataDictionary(result, namesOfMetadataWithMultipleValues, property.Name, property.GetValue(attribute), ReflectionHelpers.GetMemberType(property));
                        }
                    }
                }
            }

            return result.ToImmutable();
        }

        private static void UpdateMetadataDictionary(IDictionary<string, object> result, HashSet<string> namesOfMetadataWithMultipleValues, string name, object value, Type elementType)
        {
            object priorValue;
            if (result.TryGetValue(name, out priorValue))
            {
                if (namesOfMetadataWithMultipleValues.Add(name))
                {
                    // This is exactly the second metadatum we've observed with this name.
                    // Convert the first value to an element in an array.
                    priorValue = AddElement(null, priorValue, elementType);
                }

                result[name] = AddElement((Array)priorValue, value, elementType);
            }
            else
            {
                result.Add(name, value);
            }
        }

        private bool TryCreateImportDefinition(Type importingType, ICustomAttributeProvider member, ImmutableHashSet<IImportSatisfiabilityConstraint> importConstraints, out ImportDefinition importDefinition)
        {
            Requires.NotNull(importingType, nameof(importingType));
            Requires.NotNull(member, nameof(member));

            var importAttribute = member.GetFirstAttribute<ImportAttribute>();
            var importManyAttribute = member.GetFirstAttribute<ImportManyAttribute>();

            // Importing constructors get implied attributes on their parameters.
            if (importAttribute == null && importManyAttribute == null && member is ParameterInfo)
            {
                importAttribute = new ImportAttribute();
            }

            var sharingBoundaries = ImmutableHashSet.Create<string>();
            var sharingBoundaryAttribute = member.GetFirstAttribute<SharingBoundaryAttribute>();
            if (sharingBoundaryAttribute != null)
            {
                Verify.Operation(importingType.IsExportFactoryTypeV2(), Strings.IsExpectedOnlyOnImportsOfExportFactoryOfT, typeof(SharingBoundaryAttribute).Name);
                sharingBoundaries = sharingBoundaries.Union(sharingBoundaryAttribute.SharingBoundaryNames);
            }

            if (importAttribute != null)
            {
                Type contractType = GetTypeIdentityFromImportingType(importingType, importMany: false);
                if (contractType.IsAnyLazyType() || contractType.IsExportFactoryTypeV2())
                {
                    contractType = contractType.GetTypeInfo().GetGenericArguments()[0];
                }

                importConstraints = importConstraints
                    .Union(this.GetMetadataViewConstraints(importingType, importMany: false))
                    .Union(GetExportTypeIdentityConstraints(contractType));
                importDefinition = new ImportDefinition(
                    string.IsNullOrEmpty(importAttribute.ContractName) ? GetContractName(contractType) : importAttribute.ContractName,
                    importAttribute.AllowDefault ? ImportCardinality.OneOrZero : ImportCardinality.ExactlyOne,
                    GetImportMetadataForGenericTypeImport(contractType),
                    importConstraints,
                    sharingBoundaries);
                return true;
            }
            else if (importManyAttribute != null)
            {
                Type contractType = GetTypeIdentityFromImportingType(importingType, importMany: true);
                importConstraints = importConstraints
                    .Union(this.GetMetadataViewConstraints(importingType, importMany: true))
                    .Union(GetExportTypeIdentityConstraints(contractType));
                importDefinition = new ImportDefinition(
                    string.IsNullOrEmpty(importManyAttribute.ContractName) ? GetContractName(contractType) : importManyAttribute.ContractName,
                    ImportCardinality.ZeroOrMore,
                    GetImportMetadataForGenericTypeImport(contractType),
                    importConstraints,
                    sharingBoundaries);
                return true;
            }
            else
            {
                importDefinition = null;
                return false;
            }
        }

        private ImportDefinitionBinding CreateImport(ParameterInfo parameter, ImmutableHashSet<IImportSatisfiabilityConstraint> importConstraints)
        {
            ImportDefinition result;
            Assumes.True(this.TryCreateImportDefinition(parameter.ParameterType, parameter, importConstraints, out result));
            return new ImportDefinitionBinding(result, TypeRef.Get(parameter.Member.DeclaringType, this.Resolver), ParameterRef.Get(parameter, this.Resolver));
        }

        /// <summary>
        /// Creates a set of import constraints for an import site.
        /// </summary>
        /// <param name="importSite">The importing member or parameter.</param>
        /// <returns>A set of import constraints.</returns>
        private static ImmutableHashSet<IImportSatisfiabilityConstraint> GetImportConstraints(ICustomAttributeProvider importSite)
        {
            Requires.NotNull(importSite, nameof(importSite));

            var constraints = ImmutableHashSet.CreateRange<IImportSatisfiabilityConstraint>(
                from importConstraint in importSite.GetAttributes<ImportMetadataConstraintAttribute>()
                select new ExportMetadataValueImportConstraint(importConstraint.Name, importConstraint.Value));

            return constraints;
        }

        private static ExportDefinition CreateExportDefinition(ImmutableDictionary<string, object> memberExportMetadata, ExportAttribute exportAttribute, Type exportedType)
        {
            string contractName = string.IsNullOrEmpty(exportAttribute.ContractName) ? GetContractName(exportedType) : exportAttribute.ContractName;
            var exportMetadata = memberExportMetadata
                .Add(CompositionConstants.ExportTypeIdentityMetadataName, ContractNameServices.GetTypeIdentity(exportedType));
            var exportDefinition = new ExportDefinition(contractName, exportMetadata);
            return exportDefinition;
        }
    }
}
