namespace Microsoft.VisualStudio.Composition
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

    internal class SerializationContextBase
    {
        protected BinaryReader reader;

        protected BinaryWriter writer;

        protected Dictionary<object, uint> serializingObjectTable;

        protected Dictionary<uint, object> deserializingObjectTable;

    }
}
