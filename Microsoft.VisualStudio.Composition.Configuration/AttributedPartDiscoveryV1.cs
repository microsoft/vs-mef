namespace Microsoft.VisualStudio.Composition
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.ComponentModel.Composition;
    using System.Diagnostics;
    using System.Linq;
    using System.Reflection;
    using System.Text;
    using System.Threading.Tasks;
    using Validation;
    using MefV1 = System.ComponentModel.Composition;

    public class AttributedPartDiscoveryV1 : PartDiscovery
    {
        protected override ComposablePartDefinition CreatePart(Type partType, bool typeExplicitlyRequested)
        {
            Requires.NotNull(partType, "partType");

            // We want to ignore abstract classes, but we want to consider static classes.
            // Static classes claim to be both abstract and sealed. So to ignore just abstract
            // ones, we check that they are not sealed.
            if (partType.IsAbstract && !partType.IsSealed)
            {
                return null;
            }

            if (!typeExplicitlyRequested && partType.GetCustomAttributesCached<PartNotDiscoverableAttribute>().Any())
            {
                return null;
            }

            var partCreationPolicy = CreationPolicy.Any;
            var partCreationPolicyAttribute = partType.GetCustomAttributesCached<PartCreationPolicyAttribute>().FirstOrDefault();
            if (partCreationPolicyAttribute != null)
            {
                partCreationPolicy = (CreationPolicy)partCreationPolicyAttribute.CreationPolicy;
            }

            var allExportsMetadata = ImmutableDictionary.CreateRange(PartCreationPolicyConstraint.GetExportMetadata(partCreationPolicy));

            var inheritedExportContractNamesFromNonInterfaces = ImmutableHashSet.CreateBuilder<string>();
            var exportsOnType = ImmutableList.CreateBuilder<ExportDefinition>();
            var exportsOnMembers = ImmutableDictionary.CreateBuilder<MemberInfo, ImmutableHashSet<ExportDefinition>>();
            var imports = ImmutableList.CreateBuilder<ImportDefinitionBinding>();

            foreach (var exportAttributes in partType.GetCustomAttributesByType<ExportAttribute>())
            {
                var exportMetadataOnType = allExportsMetadata.AddRange(GetExportMetadata(exportAttributes.Key.GetCustomAttributesCached()));
                foreach (var exportAttribute in exportAttributes)
                {
                    if (exportAttributes.Key != partType && !(exportAttribute is InheritedExportAttribute))
                    {
                        // We only look at base types when the attribute we're considering is
                        // or derives from InheritedExportAttribute.
                        // Not it isn't the AttributeUsage.Inherits property.
                        // To match MEFv1 behavior, it's these two special attributes themselves that define the semantics.
                        continue;
                    }

                    var partTypeAsGenericTypeDefinition = partType.IsGenericType ? partType.GetGenericTypeDefinition() : null;
                    Type exportedType = exportAttribute.ContractType ?? partTypeAsGenericTypeDefinition ?? exportAttributes.Key;
                    string contractName = string.IsNullOrEmpty(exportAttribute.ContractName) ? GetContractName(exportedType) : exportAttribute.ContractName;
                    if (exportAttribute is InheritedExportAttribute)
                    {
                        if (inheritedExportContractNamesFromNonInterfaces.Contains(contractName))
                        {
                            // We already have an export with this contract name on this type (from a more derived type)
                            // using InheritedExportAttribute.
                            continue;
                        }

                        if (!exportAttributes.Key.IsInterface)
                        {
                            inheritedExportContractNamesFromNonInterfaces.Add(contractName);
                        }
                    }

                    var exportMetadata = exportMetadataOnType
                        .Add(CompositionConstants.ExportTypeIdentityMetadataName, ContractNameServices.GetTypeIdentity(exportedType));
                    var exportDefinition = new ExportDefinition(contractName, exportMetadata);
                    exportsOnType.Add(exportDefinition);
                }
            }

            var flags = BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            foreach (var member in Enumerable.Concat<MemberInfo>(partType.EnumProperties(), partType.EnumFields()))
            {
                var property = member as PropertyInfo;
                var field = member as FieldInfo;
                var propertyOrFieldType = ReflectionHelpers.GetMemberType(member);
                var importAttribute = member.GetCustomAttributesCached<ImportAttribute>().FirstOrDefault();
                var importManyAttribute = member.GetCustomAttributesCached<ImportManyAttribute>().FirstOrDefault();
                var exportAttributes = member.GetCustomAttributesCached<ExportAttribute>();
                Requires.Argument(!(importAttribute != null && importManyAttribute != null), "partType", "Member \"{0}\" contains both ImportAttribute and ImportManyAttribute.", member.Name);
                Requires.Argument(!(exportAttributes.Any() && (importAttribute != null || importManyAttribute != null)), "partType", "Member \"{0}\" contains both import and export attributes.", member.Name);

                ImportDefinition importDefinition;
                if (TryCreateImportDefinition(propertyOrFieldType, member.GetCustomAttributesCached(), out importDefinition))
                {
                    imports.Add(new ImportDefinitionBinding(importDefinition, partType, member));
                }
                else if (exportAttributes.Any())
                {
                    Verify.Operation(!partType.IsGenericTypeDefinition, "Exports on members not allowed when the declaring type is generic.");
                    var exportMetadataOnMember = allExportsMetadata.AddRange(GetExportMetadata(member.GetCustomAttributesCached()));
                    var exportDefinitions = ImmutableHashSet.Create<ExportDefinition>();
                    foreach (var exportAttribute in exportAttributes)
                    {
                        Type exportedType = exportAttribute.ContractType ?? propertyOrFieldType;
                        string contractName = string.IsNullOrEmpty(exportAttribute.ContractName) ? GetContractName(exportedType) : exportAttribute.ContractName;
                        var exportMetadata = exportMetadataOnMember
                            .Add(CompositionConstants.ExportTypeIdentityMetadataName, ContractNameServices.GetTypeIdentity(exportedType));
                        var exportDefinition = new ExportDefinition(contractName, exportMetadata);
                        exportDefinitions = exportDefinitions.Add(exportDefinition);
                    }

                    exportsOnMembers.Add(member, exportDefinitions);
                }
            }

            foreach (var method in partType.GetMethods(flags))
            {
                var exportAttributes = method.GetCustomAttributesCached<ExportAttribute>();
                if (exportAttributes.Any())
                {
                    var exportMetadataOnMember = allExportsMetadata.AddRange(GetExportMetadata(method.GetCustomAttributesCached()));
                    var exportDefinitions = ImmutableHashSet.Create<ExportDefinition>();
                    foreach (var exportAttribute in exportAttributes)
                    {
                        Type exportedType = exportAttribute.ContractType ?? ReflectionHelpers.GetContractTypeForDelegate(method);
                        string contractName = string.IsNullOrEmpty(exportAttribute.ContractName) ? GetContractName(exportedType) : exportAttribute.ContractName;
                        var exportMetadata = exportMetadataOnMember
                            .Add(CompositionConstants.ExportTypeIdentityMetadataName, ContractNameServices.GetTypeIdentity(exportedType));
                        var exportDefinition = new ExportDefinition(contractName, exportMetadata);
                        exportDefinitions = exportDefinitions.Add(exportDefinition);
                    }

                    exportsOnMembers.Add(method, exportDefinitions);
                }
            }

            MethodInfo onImportsSatisfied = null;
            if (typeof(IPartImportsSatisfiedNotification).IsAssignableFrom(partType))
            {
                onImportsSatisfied = typeof(IPartImportsSatisfiedNotification).GetMethod("OnImportsSatisfied", BindingFlags.Public | BindingFlags.Instance);
            }

            if (exportsOnMembers.Count > 0 || exportsOnType.Count > 0)
            {
                var importingConstructorParameters = ImmutableList.CreateBuilder<ImportDefinitionBinding>();
                var importingCtor = GetImportingConstructor<ImportingConstructorAttribute>(partType, publicOnly: false);
                if (importingCtor != null) // some parts have exports merely for metadata -- they can't be instantiated
                {
                    foreach (var parameter in importingCtor.GetParameters())
                    {
                        var import = CreateImport(parameter, parameter.GetCustomAttributesCached());
                        if (import.ImportDefinition.Cardinality == ImportCardinality.ZeroOrMore)
                        {
                            Verify.Operation(PartDiscovery.IsImportManyCollectionTypeCreateable(import), "Collection must be public with a public constructor when used with an [ImportingConstructor].");
                        }

                        importingConstructorParameters.Add(import);
                    }
                }

                return new ComposablePartDefinition(
                    partType,
                    exportsOnType.ToImmutable(),
                    exportsOnMembers.ToImmutable(),
                    imports.ToImmutable(),
                    partCreationPolicy != CreationPolicy.NonShared ? string.Empty : null,
                    onImportsSatisfied,
                    importingCtor != null ? importingConstructorParameters.ToImmutable() : null, // some MEF parts are only for metadata
                    partCreationPolicy,
                    partCreationPolicy != Composition.CreationPolicy.NonShared);
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
                if (typeDefinition.Equals(typeof(ExportFactory<>)) || typeDefinition.IsEquivalentTo(typeof(ExportFactory<,>)))
                {
                    return true;
                }
            }

            return false;
        }

        protected override IEnumerable<Type> GetTypes(Assembly assembly)
        {
            Requires.NotNull(assembly, "assembly");

            return assembly.GetTypes();
        }

        private static bool TryCreateImportDefinition(Type importingType, IEnumerable<Attribute> attributes, out ImportDefinition importDefinition)
        {
            Requires.NotNull(importingType, "importingType");

            var importAttribute = attributes.OfType<ImportAttribute>().SingleOrDefault();
            var importManyAttribute = attributes.OfType<ImportManyAttribute>().SingleOrDefault();

            if (importAttribute != null)
            {
                if (importAttribute.Source != ImportSource.Any)
                {
                    throw new NotSupportedException("Custom import sources are not yet supported.");
                }

                var requiredCreationPolicy = importingType.IsExportFactoryTypeV1()
                    ? CreationPolicy.NonShared
                    : (CreationPolicy)importAttribute.RequiredCreationPolicy;

                Type contractType = importAttribute.ContractType ?? GetTypeIdentityFromImportingType(importingType, importMany: false);
                var constraints = PartCreationPolicyConstraint.GetRequiredCreationPolicyConstraints(requiredCreationPolicy)
                    .Union(GetMetadataViewConstraints(importingType, importMany: false))
                    .Union(GetExportTypeIdentityConstraints(contractType));
                importDefinition = new ImportDefinition(
                    string.IsNullOrEmpty(importAttribute.ContractName) ? GetContractName(contractType) : importAttribute.ContractName,
                    importAttribute.AllowDefault ? ImportCardinality.OneOrZero : ImportCardinality.ExactlyOne,
                    GetImportMetadataForGenericTypeImport(contractType),
                    constraints);
                return true;
            }
            else if (importManyAttribute != null)
            {
                if (importManyAttribute.Source != ImportSource.Any)
                {
                    throw new NotSupportedException("Custom import sources are not yet supported.");
                }

                var requiredCreationPolicy = GetElementTypeFromMany(importingType).IsExportFactoryTypeV1()
                    ? CreationPolicy.NonShared
                    : (CreationPolicy)importManyAttribute.RequiredCreationPolicy;

                Type contractType = importManyAttribute.ContractType ?? GetTypeIdentityFromImportingType(importingType, importMany: true);
                var constraints = PartCreationPolicyConstraint.GetRequiredCreationPolicyConstraints(requiredCreationPolicy)
                    .Union(GetMetadataViewConstraints(importingType, importMany: true))
                    .Union(GetExportTypeIdentityConstraints(contractType));
                importDefinition = new ImportDefinition(
                    string.IsNullOrEmpty(importManyAttribute.ContractName) ? GetContractName(contractType) : importManyAttribute.ContractName,
                    ImportCardinality.ZeroOrMore,
                    GetImportMetadataForGenericTypeImport(contractType),
                    constraints);
                return true;
            }
            else
            {
                importDefinition = null;
                return false;
            }
        }

        private static ImportDefinitionBinding CreateImport(ParameterInfo parameter, IEnumerable<Attribute> attributes)
        {
            ImportDefinition definition;
            if (!TryCreateImportDefinition(parameter.ParameterType, attributes, out definition))
            {
                Assumes.True(TryCreateImportDefinition(parameter.ParameterType, attributes.Concat(new Attribute[] { new ImportAttribute() }), out definition));
            }

            return new ImportDefinitionBinding(definition, parameter.Member.DeclaringType, parameter);
        }

        private static IReadOnlyDictionary<string, object> GetExportMetadata(IEnumerable<Attribute> attributes)
        {
            Requires.NotNull(attributes, "attributes");

            var result = ImmutableDictionary.CreateBuilder<string, object>();
            foreach (var attribute in attributes)
            {
                var exportMetadataAttribute = attribute as ExportMetadataAttribute;
                if (exportMetadataAttribute != null)
                {
                    if (exportMetadataAttribute.IsMultiple)
                    {
                        result[exportMetadataAttribute.Name] = AddElement(result.GetValueOrDefault(exportMetadataAttribute.Name) as Array, exportMetadataAttribute.Value, null);
                    }
                    else
                    {
                        result.Add(exportMetadataAttribute.Name, exportMetadataAttribute.Value);
                    }
                }
                else if (attribute.GetType().EnumTypeAndBaseTypes().Any(t => t.GetCustomAttributesCached<MetadataAttributeAttribute>().Any()))
                {
                    var usage = ReflectionHelpers.GetAttributeUsage(attribute.GetType());
                    var properties = attribute.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);
                    foreach (var property in properties.Where(p => p.DeclaringType != typeof(Attribute)))
                    {
                        if (usage != null && usage.AllowMultiple)
                        {
                            result[property.Name] = AddElement(result.GetValueOrDefault(property.Name) as Array, property.GetValue(attribute), ReflectionHelpers.GetMemberType(property));
                        }
                        else
                        {
                            result.Add(property.Name, property.GetValue(attribute));
                        }
                    }
                }
            }

            return result.ToImmutable();
        }
    }
}
