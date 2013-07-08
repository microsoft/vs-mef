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

            foreach (var exportAttribute in partType.GetCustomAttributes<ExportAttribute>())
            {
                var contract = new CompositionContract(exportAttribute.ContractName, exportAttribute.ContractType ?? partType);
                var exportDefinition = new ExportDefinition(contract);
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
                    Type lazyType = null;
                    if (contractType.IsGenericType)
                    {
                        var genericDefinition = propertyOrFieldType.GetGenericTypeDefinition();
                        if (genericDefinition.IsEquivalentTo(typeof(ILazy<>)) | genericDefinition.IsEquivalentTo(typeof(Lazy<>)))
                        {
                            lazyType = propertyOrFieldType;
                            contractType = contractType.GetGenericArguments()[0];
                        }
                    }

                    var contract = new CompositionContract(importAttribute.ContractName, contractType);
                    var importDefinition = new ImportDefinition(
                        contract,
                        importAttribute.AllowDefault ? ImportCardinality.OneOrZero : ImportCardinality.ExactlyOne,
                        lazyType);
                    imports.Add(member, importDefinition);
                }
                else if (importManyAttribute != null)
                {
                    Type contractType = propertyOrFieldType.GetGenericArguments()[0];
                    Type lazyType = null;
                    if (contractType.IsGenericType)
                    {
                        var genericDefinition = contractType.GetGenericTypeDefinition();
                        if (genericDefinition.IsEquivalentTo(typeof(ILazy<>)) | genericDefinition.IsEquivalentTo(typeof(Lazy<>)))
                        {
                            lazyType = contractType;
                            contractType = contractType.GetGenericArguments()[0];
                        }
                    }

                    var contract = new CompositionContract(importManyAttribute.ContractName, contractType);
                    var importDefinition = new ImportDefinition(contract, ImportCardinality.ZeroOrMore, lazyType);
                    imports.Add(member, importDefinition);
                }
                else if (exportAttribute != null)
                {
                    var contract = new CompositionContract(exportAttribute.ContractName, exportAttribute.ContractType ?? propertyOrFieldType);
                    var exportDefinition = new ExportDefinition(contract);
                    exportsOnMembers.Add(member, exportDefinition);
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
