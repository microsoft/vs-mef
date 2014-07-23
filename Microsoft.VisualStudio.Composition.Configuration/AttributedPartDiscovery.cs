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
    using Validation;

    public class AttributedPartDiscovery : PartDiscovery
    {
        /// <summary>
        /// Gets or sets a value indicating whether non-public types and members will be explored.
        /// </summary>
        /// <remarks>
        /// The Microsoft.Composition NuGet package ignores non-publics.
        /// </remarks>
        public bool IsNonPublicSupported { get; set; }

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
            Requires.NotNull(partType, "partType");

            if (!typeExplicitlyRequested && partType.GetCustomAttributesCached<PartNotDiscoverableAttribute>().Any())
            {
                return null;
            }

            var sharedAttribute = partType.GetCustomAttributesCached<SharedAttribute>().FirstOrDefault();
            string sharingBoundary = null;
            if (sharedAttribute != null)
            {
                sharingBoundary = sharedAttribute.SharingBoundary ?? string.Empty;
            }

            CreationPolicy partCreationPolicy = sharingBoundary != null ? CreationPolicy.Shared : CreationPolicy.NonShared;
            var allExportsMetadata = ImmutableDictionary.CreateRange(PartCreationPolicyConstraint.GetExportMetadata(partCreationPolicy));

            var exportsOnType = ImmutableList.CreateBuilder<ExportDefinition>();
            var exportsOnMembers = ImmutableDictionary.CreateBuilder<MemberInfo, IReadOnlyList<ExportDefinition>>();
            var imports = ImmutableList.CreateBuilder<ImportDefinitionBinding>();
            var exportMetadataOnType = allExportsMetadata.AddRange(this.GetExportMetadata(partType.GetCustomAttributesCached()));

            foreach (var exportAttribute in partType.GetCustomAttributesCached<ExportAttribute>())
            {
                var partTypeAsGenericTypeDefinition = partType.IsGenericType ? partType.GetGenericTypeDefinition() : null;
                Type exportedType = exportAttribute.ContractType ?? partTypeAsGenericTypeDefinition ?? partType;
                string contractName = string.IsNullOrEmpty(exportAttribute.ContractName) ? GetContractName(exportedType) : exportAttribute.ContractName;
                var exportMetadata = exportMetadataOnType
                    .Add(CompositionConstants.ExportTypeIdentityMetadataName, ContractNameServices.GetTypeIdentity(exportedType));
                var exportDefinition = new ExportDefinition(contractName, exportMetadata);
                exportsOnType.Add(exportDefinition);
            }

            foreach (var member in partType.GetProperties(BindingFlags.Instance | this.PublicVsNonPublicFlags))
            {
                var importAttribute = member.GetCustomAttributesCached<ImportAttribute>().FirstOrDefault();
                var importManyAttribute = member.GetCustomAttributesCached<ImportManyAttribute>().FirstOrDefault();
                var exportAttributes = member.GetCustomAttributesCached<ExportAttribute>();
                Requires.Argument(!(importAttribute != null && importManyAttribute != null), "partType", "Member \"{0}\" contains both ImportAttribute and ImportManyAttribute.", member.Name);
                Requires.Argument(!(exportAttributes.Any() && (importAttribute != null || importManyAttribute != null)), "partType", "Member \"{0}\" contains both import and export attributes.", member.Name);

                var importConstraints = GetImportConstraints(member.GetCustomAttributesCached<ImportMetadataConstraintAttribute>());
                ImportDefinition importDefinition;
                if (TryCreateImportDefinition(ReflectionHelpers.GetMemberType(member), member.GetCustomAttributesCached(), importConstraints, out importDefinition))
                {
                    imports.Add(new ImportDefinitionBinding(importDefinition, partType, member));
                }
                else if (exportAttributes.Any())
                {
                    Verify.Operation(!partType.IsGenericTypeDefinition, "Exports on members not allowed when the declaring type is generic.");
                    var exportMetadataOnMember = allExportsMetadata.AddRange(this.GetExportMetadata(member.GetCustomAttributesCached()));
                    var exportDefinitions = ImmutableList.Create<ExportDefinition>();
                    foreach (var exportAttribute in exportAttributes)
                    {
                        Type exportedType = exportAttribute.ContractType ?? ReflectionHelpers.GetMemberType(member);
                        string contractName = string.IsNullOrEmpty(exportAttribute.ContractName) ? GetContractName(exportedType) : exportAttribute.ContractName;
                        var exportMetadata = exportMetadataOnMember
                            .Add(CompositionConstants.ExportTypeIdentityMetadataName, ContractNameServices.GetTypeIdentity(exportedType));
                        var exportDefinition = new ExportDefinition(contractName, exportMetadata);
                        exportDefinitions = exportDefinitions.Add(exportDefinition);
                    }

                    exportsOnMembers.Add(member, exportDefinitions);
                }
            }

            MethodInfo onImportsSatisfied = null;
            foreach (var method in partType.GetMethods(this.PublicVsNonPublicFlags | BindingFlags.Instance))
            {
                if (method.GetCustomAttributesCached<OnImportsSatisfiedAttribute>().Any())
                {
                    Verify.Operation(method.GetParameters().Length == 0, "OnImportsSatisfied method should take no parameters.");
                    Verify.Operation(onImportsSatisfied == null, "Only one OnImportsSatisfied method is supported.");
                    onImportsSatisfied = method;
                }
            }

            if (exportsOnMembers.Count > 0 || exportsOnType.Count > 0)
            {
                var importingConstructorParameters = ImmutableList.CreateBuilder<ImportDefinitionBinding>();
                var importingCtor = GetImportingConstructor<ImportingConstructorAttribute>(partType, publicOnly: !this.IsNonPublicSupported);
                Verify.Operation(importingCtor != null, "No importing constructor found.");
                foreach (var parameter in importingCtor.GetParameters())
                {
                    var import = CreateImport(
                            parameter,
                            parameter.GetCustomAttributesCached(),
                            GetImportConstraints(parameter.GetCustomAttributesCached()));
                    if (import.ImportDefinition.Cardinality == ImportCardinality.ZeroOrMore)
                    {
                        Verify.Operation(PartDiscovery.IsImportManyCollectionTypeCreateable(import), "Collection must be public with a public constructor when used with an [ImportingConstructor].");
                    }

                    importingConstructorParameters.Add(import);
                }

                return new ComposablePartDefinition(partType, exportsOnType.ToImmutable(), exportsOnMembers.ToImmutable(), imports.ToImmutable(), sharingBoundary, onImportsSatisfied, importingConstructorParameters.ToImmutable(), partCreationPolicy);
            }
            else
            {
                return null;
            }
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
            Requires.NotNull(assembly, "assembly");

            return this.IsNonPublicSupported ? assembly.GetTypes() : assembly.GetExportedTypes();
        }

        private ImmutableDictionary<string, object> GetExportMetadata(IEnumerable<Attribute> attributes)
        {
            Requires.NotNull(attributes, "attributes");

            var result = ImmutableDictionary.CreateBuilder<string, object>();
            var namesOfMetadataWithMultipleValues = new HashSet<string>(StringComparer.Ordinal);
            foreach (var attribute in attributes)
            {
                var exportMetadataAttribute = attribute as ExportMetadataAttribute;
                if (exportMetadataAttribute != null)
                {
                    UpdateMetadataDictionary(result, namesOfMetadataWithMultipleValues, exportMetadataAttribute.Name, exportMetadataAttribute.Value, null);
                }
                else if (attribute.GetType().GetCustomAttributesCached<MetadataAttributeAttribute>().Any())
                {
                    var properties = attribute.GetType().GetProperties(this.PublicVsNonPublicFlags | BindingFlags.Instance);
                    foreach (var property in properties.Where(p => p.DeclaringType != typeof(Attribute)))
                    {
                        UpdateMetadataDictionary(result, namesOfMetadataWithMultipleValues, property.Name, property.GetValue(attribute), ReflectionHelpers.GetMemberType(property));
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

        private static bool TryCreateImportDefinition(Type importingType, IEnumerable<Attribute> attributes, ImmutableHashSet<IImportSatisfiabilityConstraint> importConstraints, out ImportDefinition importDefinition)
        {
            Requires.NotNull(importingType, "importingType");

            var importAttribute = attributes.OfType<ImportAttribute>().SingleOrDefault();
            var importManyAttribute = attributes.OfType<ImportManyAttribute>().SingleOrDefault();
            var sharingBoundaryAttribute = attributes.OfType<SharingBoundaryAttribute>().SingleOrDefault();

            var sharingBoundaries = ImmutableHashSet.Create<string>();
            if (sharingBoundaryAttribute != null)
            {
                Verify.Operation(importingType.IsExportFactoryTypeV2(), "{0} is expected only on imports of ExportFactory<T>", typeof(SharingBoundaryAttribute).Name);
                sharingBoundaries = sharingBoundaries.Union(sharingBoundaryAttribute.SharingBoundaryNames);
            }

            if (importAttribute != null)
            {
                Type contractType = GetTypeIdentityFromImportingType(importingType, importMany: false);
                if (contractType.IsAnyLazyType() || contractType.IsExportFactoryTypeV2())
                {
                    contractType = contractType.GetGenericArguments()[0];
                }

                importConstraints = importConstraints
                    .Union(GetMetadataViewConstraints(importingType, importMany: false))
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
                    .Union(GetMetadataViewConstraints(importingType, importMany: true))
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

        private static ImportDefinitionBinding CreateImport(ParameterInfo parameter, IEnumerable<Attribute> attributes, ImmutableHashSet<IImportSatisfiabilityConstraint> importConstraints)
        {
            ImportDefinition result;
            if (!TryCreateImportDefinition(parameter.ParameterType, attributes, importConstraints, out result))
            {
                Assumes.True(TryCreateImportDefinition(parameter.ParameterType, attributes.Concat(new Attribute[] { new ImportAttribute() }), importConstraints, out result));
            }

            return new ImportDefinitionBinding(result, parameter.Member.DeclaringType, parameter);
        }

        /// <summary>
        /// Creates a set of import constraints for an import site.
        /// </summary>
        /// <param name="attributes">The attributes applied to the importing member or parameter.</param>
        /// <returns>A set of import constraints.</returns>
        private static ImmutableHashSet<IImportSatisfiabilityConstraint> GetImportConstraints(IEnumerable<Attribute> attributes)
        {
            Requires.NotNull(attributes, "attributes");

            var constraints = ImmutableHashSet.CreateRange<IImportSatisfiabilityConstraint>(
                from importConstraint in attributes.OfType<ImportMetadataConstraintAttribute>()
                select new ExportMetadataValueImportConstraint(importConstraint.Name, importConstraint.Value));

            return constraints;
        }
    }
}
