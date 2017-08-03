// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.VisualStudio.Composition.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Composition;
    using System.Linq;
    using System.Reflection;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.VisualStudio.Composition.AssemblyDiscoveryTests;
    using Xunit;
    using MefV1 = System.ComponentModel.Composition;

    [Trait("Obsolete", "BuildBreak")]
    public class ObsoleteAttributeTests
    {
        [MefFact(CompositionEngines.V1Compat | CompositionEngines.V2Compat, typeof(PartWithObsoleteConstructor))]
        public void ObsoleteConstructor(IContainer container)
        {
            var export = container.GetExportedValue<PartWithObsoleteConstructor>();
            Assert.NotNull(export);
        }

        [MefV1.Export, Export]
        public class PartWithObsoleteConstructor
        {
            [Obsolete("This part is activated by MEF. You should not call this directly.", true)]
            public PartWithObsoleteConstructor()
            {
            }
        }

        [MefFact(CompositionEngines.V1Compat | CompositionEngines.V2Compat, typeof(PartWithObsoleteExportingProperty))]
        public void ObsoleteExportingProperty(IContainer container)
        {
            var export = container.GetExportedValue<string>();
            Assert.Equal("PASS", export);
        }

        public class PartWithObsoleteExportingProperty
        {
            [Obsolete("This part is activated by MEF. You should not call this directly.", true)]
            [MefV1.Export, Export]
            public string Foo
            {
                get { return "PASS"; }
            }
        }

        [MefFact(CompositionEngines.V1Compat | CompositionEngines.V2Compat, typeof(PartWithObsoleteImportingProperty), typeof(SomeExportedValue))]
        public void ObsoleteImportingProperty(IContainer container)
        {
            var export = container.GetExportedValue<PartWithObsoleteImportingProperty>();
            Assert.NotNull(export.NonObsoleteAccessor);
        }

        [MefV1.Export, Export]
        public class PartWithObsoleteImportingProperty
        {
            [Obsolete("This part is activated by MEF. You should not call this directly.", true)]
            [MefV1.Import, Import]
            public SomeExportedValue ObsoleteProperty
            {
                get { return this.NonObsoleteAccessor; }
                set { this.NonObsoleteAccessor = value; }
            }

            public SomeExportedValue NonObsoleteAccessor { get; set; }
        }

        [MefFact(CompositionEngines.V1Compat, typeof(PartWithObsoleteExportingField))]
        public void ObsoleteExportingField(IContainer container)
        {
            var export = container.GetExportedValue<string>();
            Assert.Equal("PASS", export);
        }

        public class PartWithObsoleteExportingField
        {
            [Obsolete("This part is activated by MEF. You should not call this directly.", true)]
            [MefV1.Export]
            public string Foo = "PASS";
        }

        [MefFact(CompositionEngines.V1Compat, typeof(PartWithObsoleteImportingField), typeof(SomeExportedValue))]
        public void ObsoleteImportingField(IContainer container)
        {
            var export = container.GetExportedValue<PartWithObsoleteImportingField>();
            Assert.NotNull(typeof(PartWithObsoleteImportingField).GetTypeInfo().GetField("ObsoleteField").GetValue(export));
        }

        [MefV1.Export]
        public class PartWithObsoleteImportingField
        {
            [Obsolete("This part is activated by MEF. You should not call this directly.", true)]
            [MefV1.Import]
            public SomeExportedValue ObsoleteField;
        }

        [MefFact(CompositionEngines.V1Compat, typeof(PartWithObsoleteExportingMethod))]
        public void ObsoleteExportingMethod(IContainer container)
        {
            var export = container.GetExportedValue<Func<string>>();
            Assert.Equal("PASS", export());
        }

        public class PartWithObsoleteExportingMethod
        {
            [Obsolete("This part is activated by MEF. You should not call this directly.", true)]
            [MefV1.Export]
            public string Foo()
            {
                return "PASS";
            }
        }

        [MefV1.Export, Export]
        public class SomeExportedValue { }
    }
}
