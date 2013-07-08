namespace Microsoft.VisualStudio.Composition.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Composition;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Xunit;
    using MefV1 = System.ComponentModel.Composition;

    public class ExportingMembersTests
    {
        [MefFact(CompositionEngines.V1)]
        public void ExportedField(IContainer container)
        {
            string actual = container.GetExportedValue<string>("Field");
            Assert.Equal("Andrew", actual);
        }

        [MefFact(CompositionEngines.V1Compat | CompositionEngines.V2Compat)]
        public void ExportedProperty(IContainer container)
        {
            string actual = container.GetExportedValue<string>("Property");
            Assert.Equal("Andrew", actual);
        }

        [MefFact(CompositionEngines.V1)]
        public void ExportedMethod(IContainer container)
        {
            var actual = container.GetExportedValue<Action>("Method");
            Assert.NotNull(actual);
        }

        public class ExportingMembersClass
        {
            [MefV1.Export("Field")]
            public string Field = "Andrew";

            [Export("Property")]
            [MefV1.Export("Property")]
            public string Property
            {
                get { return "Andrew"; }
            }

            [MefV1.Export("Method")]
            public void SomeAction()
            {
            }
        }
    }
}
