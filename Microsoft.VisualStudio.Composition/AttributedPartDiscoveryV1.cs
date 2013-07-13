namespace Microsoft.VisualStudio.Composition
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.ComponentModel.Composition;
    using System.Linq;
    using System.Reflection;
    using System.Text;
    using System.Threading.Tasks;
    using Validation;

    public class AttributedPartDiscoveryV1 : PartDiscovery
    {
        public override ComposablePartDefinition CreatePart(Type partType)
        {
            Requires.NotNull(partType, "partType");

            var exportsOnType = ImmutableList.CreateBuilder<ExportDefinition>();
            var exportsOnMembers = ImmutableDictionary.CreateBuilder<MemberInfo, ExportDefinition>();
            var imports = ImmutableDictionary.CreateBuilder<MemberInfo, ImportDefinition>();
            var exportMetadataOnType = GetExportMetadata(partType.GetCustomAttributes());

            foreach (var exportAttribute in partType.GetCustomAttributes<ExportAttribute>())
            {
                var partTypeAsGenericTypeDefinition = partType.IsGenericType ? partType.GetGenericTypeDefinition() : null;
                var contract = new CompositionContract(exportAttribute.ContractName, exportAttribute.ContractType ?? partTypeAsGenericTypeDefinition ?? partType);
                var exportDefinition = new ExportDefinition(contract, exportMetadataOnType);
                exportsOnType.Add(exportDefinition);
            }

            var partCreationPolicyAttribute = partType.GetCustomAttribute<PartCreationPolicyAttribute>();
            string sharingBoundary = string.Empty;
            if (partCreationPolicyAttribute != null)
            {
                if (partCreationPolicyAttribute.CreationPolicy == CreationPolicy.NonShared)
                {
                    sharingBoundary = null;
                }
            }

            foreach (var member in Enumerable.Concat<MemberInfo>(partType.GetProperties(BindingFlags.Instance | BindingFlags.Public), partType.GetFields(BindingFlags.Instance | BindingFlags.Public)))
            {
                var property = member as PropertyInfo;
                var field = member as FieldInfo;
                var propertyOrFieldType = property != null ? property.PropertyType : field.FieldType;
                var importAttribute = member.GetCustomAttribute<ImportAttribute>();
                var importManyAttribute = member.GetCustomAttribute<ImportManyAttribute>();
                var exportAttribute = member.GetCustomAttribute<ExportAttribute>();
                Requires.Argument(!(importAttribute != null && importManyAttribute != null), "partType", "Member \"{0}\" contains both ImportAttribute and ImportManyAttribute.", member.Name);
                Requires.Argument(!(exportAttribute != null && (importAttribute != null || importManyAttribute != null)), "partType", "Member \"{0}\" contains both import and export attributes.", member.Name);

                if (importAttribute != null)
                {
                    Type contractType = propertyOrFieldType;
                    Type wrapperType = null;
                    if (contractType.IsAnyLazyType())
                    {
                        wrapperType = propertyOrFieldType;
                        contractType = contractType.GetGenericArguments()[0];
                    }
                    else if (contractType.IsExportFactoryTypeV1())
                    {
                        wrapperType = propertyOrFieldType;
                        contractType = contractType.GetGenericArguments()[0];
                    }

                    var contract = new CompositionContract(importAttribute.ContractName, contractType);
                    var importDefinition = new ImportDefinition(
                        contract,
                        importAttribute.AllowDefault ? ImportCardinality.OneOrZero : ImportCardinality.ExactlyOne,
                        wrapperType,
                        ImmutableList.Create<IImportSatisfiabilityConstraint>());
                    imports.Add(member, importDefinition);
                }
                else if (importManyAttribute != null)
                {
                    Type contractType = propertyOrFieldType.GetGenericArguments()[0];
                    Type lazyType = null;
                    if (contractType.IsAnyLazyType())
                    {
                        lazyType = contractType;
                        contractType = contractType.GetGenericArguments()[0];
                    }

                    var contract = new CompositionContract(importManyAttribute.ContractName, contractType);
                    var importDefinition = new ImportDefinition(
                        contract,
                        ImportCardinality.ZeroOrMore,
                        lazyType,
                        ImmutableList.Create<IImportSatisfiabilityConstraint>());
                    imports.Add(member, importDefinition);
                }
                else if (exportAttribute != null)
                {
                    var exportMetadataOnMember = GetExportMetadata(member.GetCustomAttributes());
                    var contract = new CompositionContract(exportAttribute.ContractName, exportAttribute.ContractType ?? propertyOrFieldType);
                    var exportDefinition = new ExportDefinition(contract, exportMetadataOnMember);
                    exportsOnMembers.Add(member, exportDefinition);
                }
            }

            foreach (var method in partType.GetMethods(BindingFlags.Public | BindingFlags.Instance))
            {
                var exportAttribute = method.GetCustomAttribute<ExportAttribute>();
                if (exportAttribute != null)
                {
                    var exportMetadataOnMember = GetExportMetadata(method.GetCustomAttributes());
                    Type contractType = exportAttribute.ContractType ?? GetContractTypeForDelegate(method);
                    var contract = new CompositionContract(exportAttribute.ContractName, contractType);
                    var exportDefinition = new ExportDefinition(contract, exportMetadataOnMember);
                    exportsOnMembers.Add(method, exportDefinition);
                }
            }

            MethodInfo onImportsSatisfied = null;
            if (typeof(IPartImportsSatisfiedNotification).IsAssignableFrom(partType))
            {
                onImportsSatisfied = typeof(IPartImportsSatisfiedNotification).GetMethod("OnImportsSatisfied", BindingFlags.Public | BindingFlags.Instance);
            }

            return exportsOnMembers.Count > 0 || exportsOnType.Count > 0
                ? new ComposablePartDefinition(partType, exportsOnType.ToImmutable(), exportsOnMembers.ToImmutable(), imports.ToImmutable(), sharingBoundary, onImportsSatisfied)
                : null;
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
                    result.Add(exportMetadataAttribute.Name, exportMetadataAttribute.Value);
                }
                else if (attribute.GetType().GetCustomAttribute<MetadataAttributeAttribute>() != null)
                {
                    var properties = attribute.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);
                    foreach (var property in properties.Where(p => p.DeclaringType != typeof(Attribute)))
                    {
                        result.Add(property.Name, property.GetValue(attribute));
                    }
                }
            }

            return result.ToImmutable();
        }

        private static Type GetContractTypeForDelegate(MethodInfo method)
        {
            Type genericTypeDefinition;
            int parametersCount = method.GetParameters().Length;
            var typeArguments = method.GetParameters().Select(p => p.ParameterType).ToList();
            var voidResult = method.ReturnType.IsEquivalentTo(typeof(void));
            if (voidResult)
            {
                if (typeArguments.Count == 0)
                {
                    return typeof(Action);
                }

                genericTypeDefinition = Type.GetType("System.Action`" + typeArguments.Count);
            }
            else
            {
                typeArguments.Add(method.ReturnType);
                genericTypeDefinition = Type.GetType("System.Func`" + typeArguments.Count);
            }

            return genericTypeDefinition.MakeGenericType(typeArguments.ToArray());
        }

        public override IReadOnlyCollection<ComposablePartDefinition> CreateParts(Assembly assembly)
        {
            Requires.NotNull(assembly, "assembly");

            var parts = from type in assembly.GetTypes()
                        where type.GetCustomAttribute<PartNotDiscoverableAttribute>() == null
                        let part = this.CreatePart(type)
                        where part != null
                        select part;
            return parts.ToImmutableArray();
        }
    }
}
