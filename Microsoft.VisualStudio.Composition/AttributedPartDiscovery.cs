namespace Microsoft.VisualStudio.Composition
{
    using System;
    using System.Collections.Generic;
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

            return new ComposablePart(partType, exports, imports);
        }
    }
}
