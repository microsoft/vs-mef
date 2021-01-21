// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.Composition.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Composition;
    using System.Composition.Hosting;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Xunit;
    using CompositionFailedException = Microsoft.VisualStudio.Composition.CompositionFailedException;
    using MefV1 = System.ComponentModel.Composition;

    public class ExportingMembersTests
    {
        [MefFact(CompositionEngines.V1Compat, typeof(ExportingMembersClass))]
        public void ExportedField(IContainer container)
        {
            string actual = container.GetExportedValue<string>("Field");
            Assert.Equal("Andrew", actual);

            actual = container.GetExportedValue<string>("Field_Extra_Export");
            Assert.Equal("Andrew", actual);
        }

        [MefFact(CompositionEngines.V1Compat | CompositionEngines.V2Compat, typeof(ExportingMembersClass))]
        public void ExportedProperty(IContainer container)
        {
            string actual = container.GetExportedValue<string>("Property");
            Assert.Equal("Andrew", actual);

            actual = container.GetExportedValue<string>("Property_Extra_Export");
            Assert.Equal("Andrew", actual);
        }

        [MefFact(CompositionEngines.V1Compat | CompositionEngines.V2Compat, typeof(ExportingMembersClass))]
        [Trait("Container.GetExport", "Plural")]
        public void GetExportsFromProperty(IContainer container)
        {
            IEnumerable<string> result = container.GetExportedValues<string>("Property");
            Assert.Equal(new[] { "Andrew" }, result);
        }

        [MefFact(CompositionEngines.V1Compat | CompositionEngines.V2Compat, typeof(ExportingMembersClass))]
        [Trait("GenericExports", "Closed")]
        public void ExportedPropertyGenericType(IContainer container)
        {
            var actual = container.GetExportedValue<Comparer<int>>("PropertyGenericType");
            Assert.NotNull(actual);
        }

        [MefFact(CompositionEngines.V1Compat | CompositionEngines.V2Compat, typeof(ExportingMembersClass))]
        [Trait("GenericExports", "Closed")]
        public void ExportedPropertyGenericTypeWrongTypeArgs(IContainer container)
        {
            Assert.Throws<CompositionFailedException>(() => container.GetExportedValue<Comparer<bool>>("PropertyGenericType"));
        }

        /// <summary>
        /// Verifies that properties that return delegates result in exports that
        /// are *not* wrapped by ExportedDelegate.
        /// </summary>
        [MefFact(CompositionEngines.V3EmulatingV1, typeof(ExportingMembersClass))]
        public void ImportDefinition_ExportedPropertyReturnsDelegate(IContainer container)
        {
            var v3ExportProvider = ((TestUtilities.V3ContainerWrapper)container).ExportProvider;
            var importDefinition = new ImportDefinition(
                "DelegateReturningProperty",
                ImportCardinality.ZeroOrMore,
                ImmutableDictionary.Create<string, object?>(),
                ImmutableHashSet.Create<IImportSatisfiabilityConstraint>());
            List<Export> exports = v3ExportProvider.GetExports(importDefinition).ToList();
            Assert.Equal(1, exports.Count);
            Assert.Equal("b", exports[0].Metadata["A"]);
            Assert.IsAssignableFrom(typeof(Func<string, string>), exports[0].Value);
        }

        /// <summary>
        /// Verifies that exported methods result in exports that
        /// *are* wrapped by ExportedDelegate.
        /// </summary>
        [MefFact(CompositionEngines.V3EmulatingV1, typeof(ExportingMembersClass))]
        public void ImportDefinition_ExportedDelegate(IContainer container)
        {
            var v3ExportProvider = ((TestUtilities.V3ContainerWrapper)container).ExportProvider;
            var importDefinition = new ImportDefinition(
                "Method",
                ImportCardinality.ZeroOrMore,
                ImmutableDictionary.Create<string, object?>(),
                ImmutableHashSet.Create<IImportSatisfiabilityConstraint>());
            List<Export> exports = v3ExportProvider.GetExports(importDefinition).ToList();
            Assert.NotEqual(0, exports.Count);
            foreach (var export in exports)
            {
                Assert.Equal("b", export.Metadata["A"]);
                Assert.IsAssignableFrom(typeof(ExportedDelegate), export.Value);
            }
        }

        [MefFact(CompositionEngines.V1Compat, typeof(ExportingMembersClass))]
        public void ExportedMethodAction(IContainer container)
        {
            var actual = container.GetExportedValue<Action>("Method");
            Assert.NotNull(actual);

            actual = container.GetExportedValue<Action>("Method_Extra_Export");
            Assert.NotNull(actual);
        }

        [MefFact(CompositionEngines.V1Compat, typeof(ExportingMembersClass))]
        [Trait("GenericExports", "Closed")]
        public void ExportedMethodActionOf2(IContainer container)
        {
            var actual = container.GetExportedValue<Action<int, string>>("Method");
            Assert.NotNull(actual);
        }

        [MefFact(CompositionEngines.V1Compat, typeof(ExportingMembersClass))]
        [Trait("GenericExports", "Closed")]
        public void ExportedMethodFunc(IContainer container)
        {
            var actual = container.GetExportedValue<Func<bool>>("Method");
            Assert.NotNull(actual);
        }

        [MefFact(CompositionEngines.V1Compat, typeof(ExportingMembersClass))]
        [Trait("GenericExports", "Closed")]
        public void ExportedMethodFuncOf2(IContainer container)
        {
            var actual = container.GetExportedValue<Func<int, string, bool>>("Method");
            Assert.NotNull(actual);
        }

        [MefFact(CompositionEngines.V1Compat, typeof(ExportingMembersClass))]
        [Trait("GenericExports", "Closed")]
        public void ExportedMethodFuncOf2WrongTypeArgs(IContainer container)
        {
            try
            {
                container.GetExportedValue<Func<string, string, bool>>("Method");
                Assert.False(true, "Expected exception not thrown.");
            }
            catch (MefV1.ImportCardinalityMismatchException) { }
            catch (Microsoft.VisualStudio.Composition.CompositionFailedException) { } // V2/V3
        }

        [MefFact(CompositionEngines.V1Compat, typeof(ExportingMembersClass), typeof(ImportingClass))]
        [Trait("GenericExports", "Closed")]
        public void ImportOfExportedMethodFuncOf2(IContainer container)
        {
            var importer = container.GetExportedValue<ImportingClass>();
            Assert.NotNull(importer.FuncOf2);
        }

        [MefFact(CompositionEngines.V1Compat, typeof(ExportingMembersClass), typeof(ImportingClass))]
        [Trait("GenericExports", "Closed")]
        public void ImportOfExportedMethodFuncOf2WrongTypeArgs(IContainer container)
        {
            var importer = container.GetExportedValue<ImportingClass>();
            Assert.Null(importer.FuncOf2WrongTypeArgs);
        }

        [MefFact(CompositionEngines.V1Compat, typeof(ExportingMembersDerivedClass))]
        public void ExportedDerivedMember(IContainer container)
        {
            var actual = container.GetExportedValue<string>("Property");
            Assert.Equal("Derived", actual);
        }

        [MefV1.Export]
        public class ImportingClass
        {
            [MefV1.Import("Method")]
            public Func<int, string, bool> FuncOf2 { get; set; } = null!;

            [MefV1.Import("Method", AllowDefault = true)]
            public Func<string, string, bool>? FuncOf2WrongTypeArgs { get; set; }
        }

        public abstract class ExportingMembersBaseClass<T>
        {
            protected abstract T Property { get; }
        }

        public class ExportingMembersDerivedClass : ExportingMembersBaseClass<string>
        {
            [Export("Property")]
            [MefV1.Export("Property")]
            protected override string Property { get { return "Derived"; } }
        }

        public class ExportingMembersClass
        {
            [MefV1.Export("Field")]
            [MefV1.Export("Field_Extra_Export")]
            public string Field = "Andrew";

            [Export("Property")]
            [MefV1.Export("Property")]
            [Export("Property_Extra_Export")]
            [MefV1.Export("Property_Extra_Export")]
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
            [MefV1.Export("Method_Extra_Export")]
            [MefV1.ExportMetadata("A", "b")]
            public void SomeAction()
            {
            }

            [MefV1.Export("Method")]
            [MefV1.ExportMetadata("A", "b")]
            public void SomeAction(int a, string b)
            {
            }

            [MefV1.Export("Method")]
            [MefV1.ExportMetadata("A", "b")]
            public bool SomeFunc()
            {
                return true;
            }

            [MefV1.Export("Method")]
            [MefV1.ExportMetadata("A", "b")]
            public bool SomeFunc(int a, string b)
            {
                return true;
            }

            [MefV1.Export("DelegateReturningProperty")]
            [MefV1.ExportMetadata("A", "b")]
            public Func<string, string> DelegateReturningProperty
            {
                get { return v => v == "TEST" ? "PASS" : "FAIL"; }
            }
        }

        #region Custom delegate type tests

        [MefFact(CompositionEngines.V1Compat, typeof(PartImportingDelegatesOfCustomType), typeof(PartExportingDelegateOfCustomType))]
        public void EventHandlerAsExports(IContainer container)
        {
            var importer = container.GetExportedValue<PartImportingDelegatesOfCustomType>();
            Assert.Equal(1, importer.Handlers.Count);
            importer.Handlers[0](null, null!);
            Assert.Equal(1, container.GetExportedValue<PartExportingDelegateOfCustomType>().InvocationCount);
        }

        [MefFact(CompositionEngines.V1Compat, typeof(PartImportingDelegatesOfCustomType), typeof(PartExportingIncompatibleDelegateType), InvalidConfiguration = true)]
        public void IncompatibleDelegateExported(IContainer container)
        {
            container.GetExportedValue<PartImportingDelegatesOfCustomType>();
        }

        [MefV1.Export]
        public class PartImportingDelegatesOfCustomType
        {
            [MefV1.ImportMany]
            public List<EventHandler> Handlers { get; set; } = null!;
        }

        [MefV1.Export]
        public class PartExportingDelegateOfCustomType
        {
            public int InvocationCount { get; set; }

            [MefV1.Export(typeof(EventHandler))]
            public void Handler(object sender, EventArgs e)
            {
                this.InvocationCount++;
            }
        }

        public class PartExportingIncompatibleDelegateType
        {
            [MefV1.Export(typeof(EventHandler))]
            public void Handler()
            {
            }
        }

        #endregion

        #region Automatic delegate type casting tests

        [MefFact(CompositionEngines.V1Compat, typeof(EventHandlerExportingPart), typeof(EventHandlerImportingPart))]
        public void EventHandlerImportExport(IContainer container)
        {
            var part = container.GetExportedValue<EventHandlerImportingPart>();
            Assert.Equal(1, part.Handlers.Count);
            Assert.Equal(1, part.LazyHandlers.Count);
            part.Handlers[0](this, new MyEventArgs());
            part.LazyHandlers[0].Value(this, new MyEventArgs());
        }

        internal class EventHandlerExportingPart
        {
            /// <summary>
            /// MEF will see this as an Action{object, EventArgs} export.
            /// </summary>
            [MefV1.Export]
            internal void SomeHandler(object sender, MyEventArgs e) { }
        }

        [MefV1.Export]
        internal class EventHandlerImportingPart
        {
            [MefV1.ImportMany]
            internal List<EventHandler<MyEventArgs>> Handlers { get; set; } = null!;

            [MefV1.ImportMany]
            internal List<Lazy<EventHandler<MyEventArgs>>> LazyHandlers { get; set; } = null!;
        }

        internal class MyEventArgs : EventArgs { }

        #endregion
    }
}
