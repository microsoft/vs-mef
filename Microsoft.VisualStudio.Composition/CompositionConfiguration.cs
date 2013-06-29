namespace Microsoft.VisualStudio.Composition
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

    public class CompositionConfiguration
    {
        public void AddPart(Type part)
        {

        }

        public CompositionContainer CreateContainer()
        {
            return new CompositionContainer();
        }
    }
}
