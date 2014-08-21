namespace Microsoft.VisualStudio.Composition
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using System.Text;
    using System.Threading.Tasks;

    internal interface IAssemblyLoader
    {
        Assembly LoadAssembly(string assemblyFullName, string codeBasePath);
    }
}
