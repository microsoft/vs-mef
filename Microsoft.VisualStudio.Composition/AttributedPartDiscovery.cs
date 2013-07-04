namespace Microsoft.VisualStudio.Composition
{
    using System;
    using System.Collections.Generic;
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

            var exports = new List<ExportDefinition>();
            var imports = new Dictionary<MemberInfo, ImportDefinition>();

            foreach (var exportAttribute in partType.GetCustomAttributes<ExportAttribute>())
            {
                var contract = new CompositionContract(exportAttribute.ContractName, exportAttribute.ContractType ?? partType);
                var exportDefinition = new ExportDefinition(contract);
                exports.Add(exportDefinition);
            }

            foreach (var member in partType.GetProperties(BindingFlags.Instance | BindingFlags.Public))
            {
                var importAttribute = member.GetCustomAttribute<ImportAttribute>();
                var importManyAttribute = member.GetCustomAttribute<ImportManyAttribute>();
                Requires.Argument(!(importAttribute != null && importManyAttribute != null), "partType", "Member \"{0}\" contains both ImportAttribute and ImportManyAttribute.", member.Name);

                if (importAttribute != null)
                {
                    var contract = new CompositionContract(importAttribute.ContractName, member.PropertyType);
                    var importDefinition = new ImportDefinition(contract, importAttribute.AllowDefault ? ImportCardinality.OneOrZero : ImportCardinality.ExactlyOne);
                    imports.Add(member, importDefinition);
                }
                else if (importManyAttribute != null)
                {
                    var contractType = member.PropertyType.GetGenericArguments()[0];
                    var contract = new CompositionContract(importManyAttribute.ContractName, contractType);
                    var importDefinition = new ImportDefinition(contract, ImportCardinality.ZeroOrMore);
                    imports.Add(member, importDefinition);
                }
            }

            return new ComposablePartDefinition(partType, exports, imports);
        }
    }
}
