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
        [MefFact(CompositionEngines.V1Compat)]
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

        [MefFact(CompositionEngines.V1Compat | CompositionEngines.V2Compat)]
        public void ExportedPropertyGenericType(IContainer container)
        {
            var actual = container.GetExportedValue<Comparer<int>>("PropertyGenericType");
            Assert.NotNull(actual);
        }

        [MefFact(CompositionEngines.V1Compat | CompositionEngines.V2Compat)]
        public void ExportedPropertyGenericTypeWrongTypeArgs(IContainer container)
        {
            try
            {
                container.GetExportedValue<Comparer<bool>>("PropertyGenericType");
                Assert.False(true, "Expected exception not thrown.");
            }
            catch (ArgumentException) { } // V3
            catch (MefV1.ImportCardinalityMismatchException)
            {
            }
            catch (System.Composition.Hosting.CompositionFailedException)
            {
            }
        }

        [MefFact(CompositionEngines.V1Compat)]
        public void ExportedMethodAction(IContainer container)
        {
            var actual = container.GetExportedValue<Action>("Method");
            Assert.NotNull(actual);
        }

        [MefFact(CompositionEngines.V1Compat)]
        public void ExportedMethodActionOf2(IContainer container)
        {
            var actual = container.GetExportedValue<Action<int, string>>("Method");
            Assert.NotNull(actual);
        }

        [MefFact(CompositionEngines.V1Compat)]
        public void ExportedMethodFunc(IContainer container)
        {
            var actual = container.GetExportedValue<Func<bool>>("Method");
            Assert.NotNull(actual);
        }

        [MefFact(CompositionEngines.V1Compat)]
        public void ExportedMethodFuncOf2(IContainer container)
        {
            var actual = container.GetExportedValue<Func<int, string, bool>>("Method");
            Assert.NotNull(actual);
        }

        [MefFact(CompositionEngines.V1Compat)]
        public void ExportedMethodFuncOf2WrongTypeArgs(IContainer container)
        {
            try
            {
                container.GetExportedValue<Func<string, string, bool>>("Method");
                Assert.False(true, "Expected exception not thrown.");
            }
            catch (MefV1.ImportCardinalityMismatchException) { }
            catch (ArgumentException) { } // V3
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

            [Export("PropertyGenericType")]
            [MefV1.Export("PropertyGenericType")]
            public Comparer<int> PropertyGenericType
            {
                get { return Comparer<int>.Default; }
            }

            [MefV1.Export("Method")]
            public void SomeAction()
            {
            }

            [MefV1.Export("Method")]
            public void SomeAction(int a, string b)
            {
            }

            [MefV1.Export("Method")]
            public bool SomeFunc()
            {
                return true;
            }

            [MefV1.Export("Method")]
            public bool SomeFunc(int a, string b)
            {
                return true;
            }
        }
    }
}
