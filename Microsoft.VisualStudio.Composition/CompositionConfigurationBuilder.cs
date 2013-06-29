namespace Microsoft.VisualStudio.Composition
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

    public class CompositionConfigurationBuilder
    {
        public CompositionConfigurationBuilder()
        {
            this.Types = new List<Type>();
        }

        internal List<Type> Types { get; private set; }

        public void AddType(Type type)
        {
            this.Types.Add(type);
        }

        public CompositionConfiguration CreateConfiguration()
        {
            PartDiscovery discovery = new AttributedPartDiscovery();
            var parts = this.Types.Select(discovery.CreatePart).ToList();

            // Use snapshots of all our present values.
            return new CompositionConfiguration(parts);
        }
    }
}
