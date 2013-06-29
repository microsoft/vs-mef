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
            this.Parts = new List<Type>();
        }

        internal List<Type> Parts { get; private set; }

        public void AddPart(Type part)
        {
            this.Parts.Add(part);
        }

        public CompositionConfiguration CreateConfiguration()
        {
            // Use snapshots of all our present values.
            return new CompositionConfiguration(this.Parts.ToList());
        }
    }
}
