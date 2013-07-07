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

            foreach (var member in partType.GetProperties(BindingFlags.Instance | BindingFlags.Public))
            {
                var importAttribute = member.GetCustomAttribute<ImportAttribute>();
                var importManyAttribute = member.GetCustomAttribute<ImportManyAttribute>();
                var exportAttribute = member.GetCustomAttribute<ExportAttribute>();
                Requires.Argument(!(importAttribute != null && importManyAttribute != null), "partType", "Member \"{0}\" contains both ImportAttribute and ImportManyAttribute.", member.Name);
                Requires.Argument(!(exportAttribute != null && (importAttribute != null || importManyAttribute != null)), "partType", "Member \"{0}\" contains both import and export attributes.", member.Name);

                if (importAttribute != null)
                {
                    Type contractType = member.PropertyType;
                    bool isLazy = false;
                    if (contractType.IsGenericType)
                    {
                        var genericDefinition = member.PropertyType.GetGenericTypeDefinition();
                        if (genericDefinition.IsEquivalentTo(typeof(ILazy<>)))
                        {
                            isLazy = true;
                            contractType = contractType.GetGenericArguments()[0];
                        }
                    }

                    var contract = new CompositionContract(importAttribute.ContractName, contractType);
                    var importDefinition = new ImportDefinition(
                        contract,
                        importAttribute.AllowDefault ? ImportCardinality.OneOrZero : ImportCardinality.ExactlyOne,
                        isLazy);
                    imports.Add(member, importDefinition);
                }
                else if (importManyAttribute != null)
                {
                    Type contractType = member.PropertyType.GetGenericArguments()[0];
                    bool isLazy = false;
                    if (contractType.IsGenericType)
                    {
                        var genericDefinition = contractType.GetGenericTypeDefinition();
                        if (genericDefinition.IsEquivalentTo(typeof(ILazy<>)))
                        {
                            isLazy = true;
                            contractType = contractType.GetGenericArguments()[0];
                        }
                    }

                    var contract = new CompositionContract(importManyAttribute.ContractName, contractType);
                    var importDefinition = new ImportDefinition(contract, ImportCardinality.ZeroOrMore, isLazy);
                    imports.Add(member, importDefinition);
                }
                else if (exportAttribute != null)
                {
                    var contract = new CompositionContract(exportAttribute.ContractName, exportAttribute.ContractType ?? partType);
                    var exportDefinition = new ExportDefinition(contract);
                    exportsOnMembers.Add(member, exportDefinition);
                }
            }

            return new ComposablePartDefinition(partType, exportsOnType.ToImmutable(), exportsOnMembers.ToImmutable(), imports.ToImmutable(), sharingBoundary);
        }
    }
}
