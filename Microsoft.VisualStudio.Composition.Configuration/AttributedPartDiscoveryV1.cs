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
        public override ComposablePartDefinition CreatePart(Type partType)
        {
            Requires.NotNull(partType, "partType");

            if (partType.IsAbstract)
            {
                return null;
            }

            var partCreationPolicy = CreationPolicy.Any;
            var partCreationPolicyAttribute = partType.GetCustomAttribute<PartCreationPolicyAttribute>();
            if (partCreationPolicyAttribute != null)
            {
                partCreationPolicy = (CreationPolicy)partCreationPolicyAttribute.CreationPolicy;
            }

            var allExportsMetadata = ImmutableDictionary.CreateRange(PartCreationPolicyConstraint.GetExportMetadata(partCreationPolicy));

            var exportsOnType = ImmutableList.CreateBuilder<ExportDefinition>();
            var exportsOnMembers = ImmutableDictionary.CreateBuilder<MemberInfo, IReadOnlyList<ExportDefinition>>();
            var imports = ImmutableDictionary.CreateBuilder<MemberInfo, ImportDefinition>();
            var exportMetadataOnType = allExportsMetadata.AddRange(GetExportMetadata(partType.GetCustomAttributes()));

            foreach (var exportAttributes in partType.GetCustomAttributesByType<ExportAttribute>())
            {
                foreach (var exportAttribute in exportAttributes)
                {
                    var partTypeAsGenericTypeDefinition = partType.IsGenericType ? partType.GetGenericTypeDefinition() : null;
                    var contract = new CompositionContract(exportAttribute.ContractName, exportAttribute.ContractType ?? partTypeAsGenericTypeDefinition ?? exportAttributes.Key);
                    var exportDefinition = new ExportDefinition(contract, exportMetadataOnType);
                    exportsOnType.Add(exportDefinition);
                }
            }

            var flags = BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            foreach (var member in Enumerable.Concat<MemberInfo>(partType.EnumProperties(), partType.EnumFields()))
            {
                var property = member as PropertyInfo;
                var field = member as FieldInfo;
                var propertyOrFieldType = property != null ? property.PropertyType : field.FieldType;
                var importAttribute = member.GetCustomAttribute<ImportAttribute>();
                var importManyAttribute = member.GetCustomAttribute<ImportManyAttribute>();
                var exportAttributes = member.GetCustomAttributes<ExportAttribute>();
                Requires.Argument(!(importAttribute != null && importManyAttribute != null), "partType", "Member \"{0}\" contains both ImportAttribute and ImportManyAttribute.", member.Name);
                Requires.Argument(!(exportAttributes.Any() && (importAttribute != null || importManyAttribute != null)), "partType", "Member \"{0}\" contains both import and export attributes.", member.Name);

                ImportDefinition importDefinition;
                if (TryCreateImportDefinition(propertyOrFieldType, member.GetCustomAttributes(), out importDefinition))
                {
                    imports.Add(member, importDefinition);
                }
                else if (exportAttributes.Any())
                {
                    Verify.Operation(!partType.IsGenericTypeDefinition, "Exports on members not allowed when the declaring type is generic.");
                    var exportMetadataOnMember = allExportsMetadata.AddRange(GetExportMetadata(member.GetCustomAttributes()));
                    var exportDefinitions = ImmutableList.Create<ExportDefinition>();
                    foreach (var exportAttribute in exportAttributes)
                    {
                        var contract = new CompositionContract(exportAttribute.ContractName, exportAttribute.ContractType ?? propertyOrFieldType);
                        var exportDefinition = new ExportDefinition(contract, exportMetadataOnMember);
                        exportDefinitions = exportDefinitions.Add(exportDefinition);
                    }

                    exportsOnMembers.Add(member, exportDefinitions);
                }
            }

            foreach (var method in partType.GetMethods(flags))
            {
                var exportAttributes = method.GetCustomAttributes<ExportAttribute>();
                if (exportAttributes.Any())
                {
                    var exportMetadataOnMember = allExportsMetadata.AddRange(GetExportMetadata(method.GetCustomAttributes()));
                    var exportDefinitions = ImmutableList.Create<ExportDefinition>();
                    foreach (var exportAttribute in exportAttributes)
                    {
                        Type contractType = exportAttribute.ContractType ?? Export.GetContractTypeForDelegate(method);
                        var contract = new CompositionContract(exportAttribute.ContractName, contractType);
                        var exportDefinition = new ExportDefinition(contract, exportMetadataOnMember);
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
                var importingConstructorParameters = ImmutableList.CreateBuilder<Import>();
                var importingCtor = GetImportingConstructor(partType, typeof(ImportingConstructorAttribute), publicOnly: false);
                if (importingCtor != null) // some parts have exports merely for metadata -- they can't be instantiated
                {
                    foreach (var parameter in importingCtor.GetParameters())
                    {
                        var import = CreateImport(parameter, parameter.GetCustomAttributes());
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
                    onImportsSatisfied,
                    importingCtor != null ? importingConstructorParameters.ToImmutable() : null,
                    partCreationPolicy);
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
                var contract = new CompositionContract(importAttribute.ContractName, contractType);
                var constraints = PartCreationPolicyConstraint.GetRequiredCreationPolicyConstraints(requiredCreationPolicy)
                    .Union(GetMetadataViewConstraints(importingType, importMany: false));
                importDefinition = new ImportDefinition(
                    contract,
                    importAttribute.AllowDefault ? ImportCardinality.OneOrZero : ImportCardinality.ExactlyOne,
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
                var contract = new CompositionContract(importManyAttribute.ContractName, contractType);
                var constraints = PartCreationPolicyConstraint.GetRequiredCreationPolicyConstraints(requiredCreationPolicy)
                   .Union(GetMetadataViewConstraints(importingType, importMany: true));
                importDefinition = new ImportDefinition(
                    contract,
                    ImportCardinality.ZeroOrMore,
                    constraints);
                return true;
            }
            else
            {
                importDefinition = null;
                return false;
            }
        }

        private static Import CreateImport(ParameterInfo parameter, IEnumerable<Attribute> attributes)
        {
            ImportDefinition definition;
            if (!TryCreateImportDefinition(parameter.ParameterType, attributes, out definition))
            {
                Assumes.True(TryCreateImportDefinition(parameter.ParameterType, attributes.Concat(new Attribute[] { new ImportAttribute() }), out definition));
            }

            return new Import(definition, parameter.Member.DeclaringType, parameter);
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
                        result[exportMetadataAttribute.Name] = AddElement(result.GetValueOrDefault(exportMetadataAttribute.Name) as Array, exportMetadataAttribute.Value);
                    }
                    else
                    {
                        result.Add(exportMetadataAttribute.Name, exportMetadataAttribute.Value);
                    }
                }
                else if (attribute.GetType().GetCustomAttribute<MetadataAttributeAttribute>() != null)
                {
                    var usage = attribute.GetType().GetCustomAttribute<AttributeUsageAttribute>();
                    var properties = attribute.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);
                    foreach (var property in properties.Where(p => p.DeclaringType != typeof(Attribute)))
                    {
                        if (usage != null && usage.AllowMultiple)
                        {
                            result[property.Name] = AddElement(result.GetValueOrDefault(property.Name) as Array, property.GetValue(attribute));
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

        public override IReadOnlyCollection<ComposablePartDefinition> CreateParts(Assembly assembly)
        {
            Requires.NotNull(assembly, "assembly");

            var parts = from type in assembly.GetTypes()
                        where type.GetCustomAttribute<PartNotDiscoverableAttribute>() == null
                        let part = this.CreatePart(type)
                        where part != null
                        select part;
            return parts.ToImmutableList();
        }
    }
}
