namespace Microsoft.VisualStudio.Composition
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Validation;

    public class ExportTypeIdentityConstraint : IImportSatisfiabilityConstraint
    {
        public ExportTypeIdentityConstraint(Type typeIdentity)
        {
            Requires.NotNull(typeIdentity, "typeIdentity");
            this.TypeIdentityName = ContractNameServices.GetTypeIdentity(typeIdentity);
        }

        public ExportTypeIdentityConstraint(string typeIdentityName)
        {
            Requires.NotNullOrEmpty(typeIdentityName, "typeIdentityName");
            this.TypeIdentityName = typeIdentityName;
        }

        public string TypeIdentityName { get; private set; }

        public static ImmutableDictionary<string, object> GetExportMetadata(Type type)
        {
            Requires.NotNull(type, "type");

            return GetExportMetadata(ContractNameServices.GetTypeIdentity(type));
        }

        public static ImmutableDictionary<string, object> GetExportMetadata(string typeIdentity)
        {
            Requires.NotNullOrEmpty(typeIdentity, "typeIdentity");

            return ImmutableDictionary<string, object>.Empty.Add(CompositionConstants.ExportTypeIdentityMetadataName, typeIdentity);
        }

        public bool IsSatisfiedBy(ExportDefinition exportDefinition)
        {
            Requires.NotNull(exportDefinition, "exportDefinition");

            string value;
            if (exportDefinition.Metadata.TryGetValue(CompositionConstants.ExportTypeIdentityMetadataName, out value))
            {
                return this.TypeIdentityName == value;
            }

            return false;
        }
    }
}
