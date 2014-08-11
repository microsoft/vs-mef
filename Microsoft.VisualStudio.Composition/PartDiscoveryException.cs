namespace Microsoft.VisualStudio.Composition
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using System.Text;
    using System.Threading.Tasks;

    public class PartDiscoveryException : Exception
    {
        public PartDiscoveryException() { }

        public PartDiscoveryException(string message) : base(message) { }

        public PartDiscoveryException(string message, Exception inner) : base(message, inner) { }

        public string AssemblyPath { get; set; }

        public Type ScannedType { get; set; }
    }
}
