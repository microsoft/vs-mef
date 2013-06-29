namespace Microsoft.VisualStudio.Composition
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

    public abstract class PartDiscovery
    {
        public abstract ComposablePart CreatePart(Type partType);
    }
}
