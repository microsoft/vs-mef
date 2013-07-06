﻿namespace Microsoft.VisualStudio.Composition.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

    public interface IContainer
    {
        T GetExport<T>();

        T GetExport<T>(string contractName);
    }
}
