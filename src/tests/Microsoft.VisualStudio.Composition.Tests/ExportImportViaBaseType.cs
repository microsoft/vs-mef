// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.Composition.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Composition;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Xunit;

    public class ExportImportViaBaseType
    {
        [MefFact(CompositionEngines.V2Compat)]
        public void ImportViaExportedInterface(IContainer container)
        {
            Consumer consumer = container.GetExportedValue<Consumer>();
            Assert.NotNull(consumer);
            Assert.NotNull(consumer.Imported);
            Assert.IsAssignableFrom(typeof(Implementor), consumer.Imported);
        }

        public interface ISomeType { }

        [Export(typeof(ISomeType))]
        public class Implementor : ISomeType { }

        [Export]
        public class Consumer
        {
            [Import]
            public ISomeType Imported { get; set; } = null!;
        }
    }
}
