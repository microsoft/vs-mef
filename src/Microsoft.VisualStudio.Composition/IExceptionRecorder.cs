using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.VisualStudio.Composition
{
    public interface IExceptionRecorder
    {
        void RecordException(Exception e, RuntimeComposition.RuntimeImport import, RuntimeComposition.RuntimeExport export);
    }
}
