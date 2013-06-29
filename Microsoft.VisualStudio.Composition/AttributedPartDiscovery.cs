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
        public override ComposablePart CreatePart(Type partType)
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
                if (importAttribute != null)
                {
                    var contract = new CompositionContract(importAttribute.ContractName, member.PropertyType);
                    var importDefinition = new ImportDefinition(contract, importAttribute.AllowDefault);
                    imports.Add(member, importDefinition);
                }
            }

            return new ComposablePart(partType, exports, imports);
        }
    }
}
