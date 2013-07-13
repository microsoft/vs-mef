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

            var sharedAttribute = partType.GetCustomAttribute<SharedAttribute>();
            string sharingBoundary = null;
            if (sharedAttribute != null)
            {
                sharingBoundary = sharedAttribute.SharingBoundary ?? string.Empty;
            }

            foreach (var member in partType.GetProperties(BindingFlags.Instance | BindingFlags.Public))
            {
                var importAttribute = member.GetCustomAttribute<ImportAttribute>();
                var importManyAttribute = member.GetCustomAttribute<ImportManyAttribute>();
                var exportAttribute = member.GetCustomAttribute<ExportAttribute>();
                Requires.Argument(!(importAttribute != null && importManyAttribute != null), "partType", "Member \"{0}\" contains both ImportAttribute and ImportManyAttribute.", member.Name);
                Requires.Argument(!(exportAttribute != null && (importAttribute != null || importManyAttribute != null)), "partType", "Member \"{0}\" contains both import and export attributes.", member.Name);

                var importConstraints = ImmutableList.CreateBuilder<IImportSatisfiabilityConstraint>();
                foreach (var importConstraint in member.GetCustomAttributes<ImportMetadataConstraintAttribute>())
                {
                    importConstraints.Add(new ImportMetadataValueConstraint(importConstraint.Name, importConstraint.Value));
                }

                if (importAttribute != null)
                {
                    Type contractType = member.PropertyType;
                    Type lazyType = null;
                    if (contractType.IsAnyLazyType())
                    {
                        lazyType = member.PropertyType;
                        contractType = contractType.GetGenericArguments()[0];
                    }

                    var contract = new CompositionContract(importAttribute.ContractName, contractType);
                    var importDefinition = new ImportDefinition(
                        contract,
                        importAttribute.AllowDefault ? ImportCardinality.OneOrZero : ImportCardinality.ExactlyOne,
                        lazyType,
                        importConstraints.ToImmutable());
                    imports.Add(member, importDefinition);
                }
                else if (importManyAttribute != null)
                {
                    Type contractType = member.PropertyType.GetGenericArguments()[0];
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
                        importConstraints.ToImmutable());
                    imports.Add(member, importDefinition);
                }
                else if (exportAttribute != null)
                {
                    var exportMetadataOnMember = GetExportMetadata(member.GetCustomAttributes());
                    var contract = new CompositionContract(exportAttribute.ContractName, exportAttribute.ContractType ?? member.PropertyType);
                    var exportDefinition = new ExportDefinition(contract, exportMetadataOnMember);
                    exportsOnMembers.Add(member, exportDefinition);
                }
            }

            MethodInfo onImportsSatisfied = null;
            foreach (var method in partType.GetMethods(BindingFlags.Public | BindingFlags.Instance))
            {
                if (method.GetCustomAttribute<OnImportsSatisfiedAttribute>() != null)
                {
                    Verify.Operation(method.GetParameters().Length == 0, "OnImportsSatisfied method should take no parameters.");
                    Verify.Operation(onImportsSatisfied == null, "Only one OnImportsSatisfied method is supported.");
                    onImportsSatisfied = method;
                }
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

        public override IReadOnlyCollection<ComposablePartDefinition> CreateParts(Assembly assembly)
        {
            Requires.NotNull(assembly, "assembly");

            var parts = from type in assembly.GetExportedTypes()
                        where type.GetCustomAttribute<PartNotDiscoverableAttribute>() == null
                        let part = this.CreatePart(type)
                        where part != null
                        select part;
            return parts.ToImmutableArray();
        }
    }
}
