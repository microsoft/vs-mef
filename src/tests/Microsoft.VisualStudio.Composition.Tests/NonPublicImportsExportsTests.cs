// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.Composition.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Xunit;
    using MefV1 = System.ComponentModel.Composition;
    using MefV2 = System.Composition;

    [Trait("Access", "NonPublic")]
    public class NonPublicImportsExportsTests
    {
        [MefFact(CompositionEngines.V1Compat | CompositionEngines.V3EmulatingV2WithNonPublic, typeof(InternalExport), typeof(PublicExport))]
        public void InternalExportedType(IContainer container)
        {
            var result = container.GetExportedValue<InternalExport>();
            Assert.NotNull(result);
            Assert.NotNull(result.InternalImportingProperty);
            Assert.NotNull(result.PublicImportingProperty);
        }

        [MefFact(CompositionEngines.V1Compat, typeof(InternalExportNonShared), typeof(PublicExport))]
        public void InternalExportedTypeNonShared(IContainer container)
        {
            var result = container.GetExportedValue<InternalExportNonShared>();
            Assert.NotNull(result);
            Assert.NotNull(result.InternalImportingProperty);
            Assert.NotNull(result.PublicImportingProperty);
        }

        [MefFact(CompositionEngines.V1Compat, typeof(InternalExport), typeof(PublicExport), typeof(InternalPartImportingInternalPart))]
        public void InternalLazyImportOfInternalExport(IContainer container)
        {
            var result = container.GetExportedValue<InternalPartImportingInternalPart>();
            Assert.NotNull(result);
            Assert.NotNull(result.InternalImportingProperty);
            Assert.NotNull(result.InternalImportingProperty.Value);
            Assert.NotNull(result.InternalImportingProperty.Value.InternalImportingProperty);
            Assert.NotNull(result.InternalImportingProperty.Value.PublicImportingProperty);
        }

        [MefFact(CompositionEngines.V1Compat, typeof(InternalExport), typeof(PublicExport), typeof(InternalPartWithImportManyOfInternalPart))]
        public void InternalLazyImportManyOfInternalExport(IContainer container)
        {
            var result = container.GetExportedValue<InternalPartWithImportManyOfInternalPart>();
            Assert.NotNull(result);
            Assert.NotNull(result.InternalImportingProperty);
            Assert.NotNull(result.InternalImportingProperty.First().Value);
            Assert.NotNull(result.InternalImportingProperty.First().Value.InternalImportingProperty);
            Assert.NotNull(result.InternalImportingProperty.First().Value.PublicImportingProperty);
        }

        [MefFact(CompositionEngines.V1Compat, typeof(InternalGenericExport<>), typeof(PublicExport))]
        public void InternalGenericExportedType(IContainer container)
        {
            var result = container.GetExportedValue<InternalGenericExport<IInternalInterface>>();
            Assert.NotNull(result);
            Assert.NotNull(result.InternalImportingProperty);
            Assert.NotNull(result.PublicImportingProperty);
        }

        [MefFact(CompositionEngines.V1Compat, typeof(PublicExportOfInternalInterface), typeof(PublicExport))]
        public void PublicExportOfInternalType(IContainer container)
        {
            var result = container.GetExportedValue<IInternalInterface>();
            Assert.NotNull(result);
            Assert.IsType<PublicExportOfInternalInterface>(result);
            var cast = (PublicExportOfInternalInterface)result;
            Assert.NotNull(cast.InternalImportingProperty);
            Assert.NotNull(cast.PublicImportingProperty);
        }

        [MefFact(CompositionEngines.V1Compat, typeof(InternalTypeWithExportingMember))]
        public void GetExportsFromInternalTypeWithExportingMembers(IContainer container)
        {
            Assert.Equal(3, container.GetExportedValue<int>());
            Assert.Equal("Hi", container.GetExportedValue<string>());
        }

        [MefFact(CompositionEngines.V1Compat, typeof(InternalTypeWithExportingMember), typeof(PartThatImportsInternalMemberExports))]
        public void ImportFromInternalTypeWithExportingMembers(IContainer container)
        {
            var importer = container.GetExportedValue<PartThatImportsInternalMemberExports>();
            Assert.Equal(3, importer.ImportedInt);
            Assert.Equal("Hi", importer.ImportedString);
        }

        [MefFact(CompositionEngines.V1Compat, typeof(PublicExport), typeof(ExportWithPrivateImportingProperty))]
        public void PrivateImportingProperty(IContainer container)
        {
            var result = container.GetExportedValue<ExportWithPrivateImportingProperty>();
            Assert.NotNull(result.InternalAccessor);
        }

        [MefFact(CompositionEngines.V1Compat, typeof(PublicExport), typeof(ExportWithPrivateImportingPropertySetter))]
        public void PrivateImportingPropertySetter(IContainer container)
        {
            var result = container.GetExportedValue<ExportWithPrivateImportingPropertySetter>();
            Assert.NotNull(result.ImportingProperty);
        }

        [MefFact(CompositionEngines.V1Compat, typeof(PublicExport), typeof(OpenGenericPartWithPrivateImportingProperty<>))]
        public void PrivateImportingPropertyOnOpenGenericPart(IContainer container)
        {
            var result = container.GetExportedValue<OpenGenericPartWithPrivateImportingProperty<int>>();
            Assert.NotNull(result.InternalAccessor);
        }

        [MefFact(CompositionEngines.V1Compat, typeof(PublicExport), typeof(ExportWithPrivateImportingField))]
        public void PrivateImportingField(IContainer container)
        {
            var result = container.GetExportedValue<ExportWithPrivateImportingField>();
            Assert.NotNull(result.InternalAccessor);
        }

        [MefFact(CompositionEngines.V1Compat, typeof(PublicExport), typeof(OpenGenericPartWithPrivateImportingField<>))]
        public void PrivateImportingFieldOnOpenGenericPart(IContainer container)
        {
            var result = container.GetExportedValue<OpenGenericPartWithPrivateImportingField<int>>();
            Assert.NotNull(result.InternalAccessor);
        }

        [MefFact(CompositionEngines.V1Compat, typeof(PartWithPrivateExportingProperty))]
        public void PrivateExportingProperty(IContainer container)
        {
            string result = container.GetExportedValue<string>();
            Assert.Equal("Success", result);
        }

        [MefFact(CompositionEngines.V1Compat, typeof(PartWithPrivateExportingProperty), typeof(PartThatImportsPrivateExportingProperty))]
        public void ImportOfPrivateExportingProperty(IContainer container)
        {
            var importer = container.GetExportedValue<PartThatImportsPrivateExportingProperty>();
            Assert.Equal("Success", importer.Value);
        }

        [MefFact(CompositionEngines.V1Compat, typeof(PartWithPrivateExportingField))]
        public void PrivateExportingField(IContainer container)
        {
            string result = container.GetExportedValue<string>();
            Assert.Equal("Success", result);
        }

        [MefFact(CompositionEngines.V1Compat, typeof(PartWithPrivateExportingField), typeof(PartThatImportsPrivateExportingField))]
        public void ImportOfPrivateExportingField(IContainer container)
        {
            var importer = container.GetExportedValue<PartThatImportsPrivateExportingField>();
            Assert.Equal("Success", importer.Value);
        }

        [MefFact(CompositionEngines.V1Compat, typeof(PartWithPrivateExportedMethod))]
        public void PrivateExportedMethod(IContainer container)
        {
            Func<string> result = container.GetExportedValue<Func<string>>();
            Assert.Equal("Success", result());
        }

        [MefFact(CompositionEngines.V1Compat, typeof(PartWithPrivateExportedMethod), typeof(PartWithImportOfPrivateExportedMethod))]
        public void ImportOfPrivateExportedMethod(IContainer container)
        {
            var importer = container.GetExportedValue<PartWithImportOfPrivateExportedMethod>();
            Assert.Equal("Success", importer.Value());
        }

        [MefFact(CompositionEngines.V1Compat, typeof(PartWithPrivateImportManyFieldArray), typeof(PublicExport))]
        public void PrivateImportManyFieldArray(IContainer container)
        {
            var part = container.GetExportedValue<PartWithPrivateImportManyFieldArray>();
            Assert.Equal(1, part.ImportManyFieldAccessor.Length);
            Assert.NotNull(part.ImportManyFieldAccessor[0]);
        }

        [MefFact(CompositionEngines.V1Compat, typeof(PartWithPrivateImportManyFieldSpecialCollection), typeof(PublicExport))]
        public void PrivateImportManyFieldSpecialCollection(IContainer container)
        {
            var part = container.GetExportedValue<PartWithPrivateImportManyFieldSpecialCollection>();
            Assert.Equal(1, part.ImportManyFieldAccessor.Count);
            Assert.NotNull(part.ImportManyFieldAccessor[0]);
        }

        [MefFact(CompositionEngines.V1Compat, typeof(PartWithPrivateImportManyPropertySpecialCollection), typeof(PublicExport))]
        public void PrivateImportManyPropertySpecialCollection(IContainer container)
        {
            var part = container.GetExportedValue<PartWithPrivateImportManyPropertySpecialCollection>();
            Assert.Equal(1, part.ImportManyPropertyAccessor.Count);
            Assert.NotNull(part.ImportManyPropertyAccessor[0]);
        }

        [MefFact(CompositionEngines.V1Compat, typeof(PartWithPrivateImportManyFieldPrivateCollection), typeof(PublicExport))]
        public void PrivateImportManyFieldPrivateCollection(IContainer container)
        {
            var part = container.GetExportedValue<PartWithPrivateImportManyFieldPrivateCollection>();
            Assert.Equal(1, part.ImportManyFieldAccessor.Count);
            Assert.NotNull(part.ImportManyFieldAccessor.Single());
        }

        [MefFact(CompositionEngines.V1Compat, typeof(PartWithPrivateImportManyPropertyPrivateCollection), typeof(PublicExport))]
        public void PrivateImportManyPropertyPrivateCollection(IContainer container)
        {
            var part = container.GetExportedValue<PartWithPrivateImportManyPropertyPrivateCollection>();
            Assert.Equal(1, part.ImportManyPropertyAccessor.Count);
            Assert.NotNull(part.ImportManyPropertyAccessor.Single());
        }

        [MefFact(CompositionEngines.V1Compat, typeof(PartWithLazyImportOfInternalPartViaPublicInterface), typeof(InternalPartWithPublicExport))]
        public void LazyImportOfInternalPartViaPublicInterface(IContainer container)
        {
            var importingPart = container.GetExportedValue<PartWithLazyImportOfInternalPartViaPublicInterface>();
            Assert.NotNull(importingPart.ImportingProperty);
            Assert.NotNull(importingPart.ImportingProperty.Value);
        }

        [MefFact(CompositionEngines.V1Compat, typeof(PartWithImportOfInternalPartViaPublicInterface), typeof(InternalPartWithPublicExport))]
        public void ImportOfInternalPartViaPublicInterface(IContainer container)
        {
            var importingPart = container.GetExportedValue<PartWithImportOfInternalPartViaPublicInterface>();
            Assert.NotNull(importingPart.ImportingProperty);
        }

        [MefFact(CompositionEngines.V1Compat, typeof(PartWithImportManyOfInternalPartViaPublicInterface), typeof(InternalPartWithPublicExport))]
        public void ImportManyOfInternalPartViaPublicInterface(IContainer container)
        {
            var importingPart = container.GetExportedValue<PartWithImportManyOfInternalPartViaPublicInterface>();
            Assert.NotNull(importingPart.ImportingProperty);
            Assert.Equal(1, importingPart.ImportingProperty.Count);
        }

        [MefFact(CompositionEngines.V1Compat, typeof(PartWithImportManyOfInternalPartViaInternalInterface), typeof(InternalPartWithPublicExport))]
        public void ImportManyOfInternalPartViaInternalInterface(IContainer container)
        {
            var importingPart = container.GetExportedValue<PartWithImportManyOfInternalPartViaInternalInterface>();
            Assert.NotNull(importingPart.ImportingProperty);
            Assert.Equal(1, importingPart.ImportingProperty.Count);
        }

        [MefFact(CompositionEngines.V1Compat, typeof(PartWithImportManyOfInternalPartViaInternalInterfaceInitialized), typeof(InternalPartWithPublicExport))]
        public void ImportManyOfInternalPartViaInternalInterfaceInitialized(IContainer container)
        {
            var importingPart = container.GetExportedValue<PartWithImportManyOfInternalPartViaInternalInterfaceInitialized>();
            Assert.NotNull(importingPart.ImportingProperty);
            Assert.Equal(1, importingPart.ImportingProperty.Count);
        }

        [MefFact(CompositionEngines.V1Compat, typeof(PartWithLazyImportManyOfInternalPartViaInternalInterface), typeof(InternalPartWithPublicExport))]
        public void LazyImportManyOfInternalPartViaInternalInterface(IContainer container)
        {
            var importingPart = container.GetExportedValue<PartWithLazyImportManyOfInternalPartViaInternalInterface>();
            Assert.NotNull(importingPart.ImportingProperty);
            Assert.Equal(1, importingPart.ImportingProperty.Count);
            Assert.IsType<InternalPartWithPublicExport>(importingPart.ImportingProperty[0].Value);
        }

        [MefFact(CompositionEngines.V1Compat, typeof(PartWithLazyImportOfInternalPartViaInternalInterface), typeof(InternalPartWithPublicExport))]
        public void LazyImportOfInternalPartViaInternalInterface(IContainer container)
        {
            var importingPart = container.GetExportedValue<PartWithLazyImportOfInternalPartViaInternalInterface>();
            Assert.NotNull(importingPart.ImportingProperty);
            Assert.NotNull(importingPart.ImportingProperty.Value);
        }

        [MefFact(CompositionEngines.V1Compat, typeof(PartWithLazyImportMetadataOfInternalPartViaPublicInterface), typeof(InternalPartWithPublicExport))]
        public void LazyImportMetadataOfInternalPartViaPublicInterface(IContainer container)
        {
            var importingPart = container.GetExportedValue<PartWithLazyImportMetadataOfInternalPartViaPublicInterface>();
            Assert.NotNull(importingPart.ImportingProperty);
            Assert.NotNull(importingPart.ImportingProperty.Metadata);
            Assert.NotNull(importingPart.ImportingProperty.Value);
        }

        [MefFact(CompositionEngines.V1Compat, typeof(PartWithLazyImportMetadataOfInternalPartViaInternalInterface), typeof(InternalPartWithPublicExport))]
        public void LazyImportMetadataOfInternalPartViaInternalInterface(IContainer container)
        {
            var importingPart = container.GetExportedValue<PartWithLazyImportMetadataOfInternalPartViaInternalInterface>();
            Assert.NotNull(importingPart.ImportingProperty);
            Assert.NotNull(importingPart.ImportingProperty.Metadata);
            Assert.NotNull(importingPart.ImportingProperty.Value);
        }

        [MefFact(CompositionEngines.V1Compat, typeof(InternalPartDerivingFromPublicClass), typeof(PublicExport))]
        public void InternalPartDerivesFromPublicBaseClassWithImports(IContainer container)
        {
            var part = container.GetExportedValue<InternalPartDerivingFromPublicClass>();
            Assert.NotNull(part.ImportingProperty);
        }

        #region PrivateExportedDelegate

        [MefFact(CompositionEngines.V1Compat, typeof(DelegateExportingPart), typeof(DelegateImportingPart))]
        public void PrivateExportedDelegate(IContainer container)
        {
            var part = container.GetExportedValue<DelegateImportingPart>();
            Assert.NotNull(part.SomeMethod);
        }

        [MefFact(CompositionEngines.V1Compat, typeof(DelegateExportingPart), typeof(LazyDelegateImportingPart))]
        public void PrivateExportedDelegateLazy(IContainer container)
        {
            var part = container.GetExportedValue<LazyDelegateImportingPart>();
            Assert.NotNull(part.SomeMethod);
        }

        [MefFact(CompositionEngines.V1Compat, typeof(DelegateExportingPart), typeof(ManyLazyDelegateImportingPart))]
        public void PrivateExportedDelegateManyLazy(IContainer container)
        {
            var part = container.GetExportedValue<ManyLazyDelegateImportingPart>();
            Assert.Equal(1, part.SomeMethod.Count);
            Assert.NotNull(part.SomeMethod[0].Value);
        }

        #region PartWithBaseClassHavingPrivateImportingField

        [MefFact(CompositionEngines.V1Compat, typeof(DerivedFromPartWithPrivateImportingField), typeof(PublicExport))]
        public void PartWithBaseClassHavingPrivateImportingField(IContainer container)
        {
            var part = container.GetExportedValue<DerivedFromPartWithPrivateImportingField>();
            Assert.NotNull(part.ImportingFieldAccessor);
        }

        public class PartWithPrivateImportingField
        {
            [MefV1.Import]
            private PublicExport importingField = null!;

            public PublicExport ImportingFieldAccessor
            {
                get { return this.importingField; }
            }
        }

        [MefV1.Export]
        public class DerivedFromPartWithPrivateImportingField : PartWithPrivateImportingField { }

        #endregion

        public class DelegateExportingPart
        {
            [MefV1.Export]
            private void SomeMethod() { }
        }

        [MefV1.Export]
        public class DelegateImportingPart
        {
            [MefV1.Import]
            public Action SomeMethod { get; set; } = null!;
        }

        [MefV1.Export]
        public class LazyDelegateImportingPart
        {
            [MefV1.Import]
            public Lazy<Action> SomeMethod { get; set; } = null!;
        }

        [MefV1.Export]
        public class ManyLazyDelegateImportingPart
        {
            [MefV1.ImportMany]
            public List<Lazy<Action>> SomeMethod { get; set; } = null!;
        }

        #endregion

        public interface IPublicInterface { }

        [MefV1.Export(typeof(IPublicInterface))]
        [MefV1.Export(typeof(IInternalInterface))]
        internal class InternalPartWithPublicExport : IPublicInterface, IInternalInterface { }

        [MefV1.Export]
        public class PartWithLazyImportOfInternalPartViaPublicInterface
        {
            [MefV1.Import]
            public Lazy<IPublicInterface> ImportingProperty { get; set; } = null!;
        }

        [MefV1.Export]
        public class PartWithImportOfInternalPartViaPublicInterface
        {
            [MefV1.Import]
            public IPublicInterface ImportingProperty { get; set; } = null!;
        }

        [MefV1.Export]
        public class PartWithImportManyOfInternalPartViaPublicInterface
        {
            [MefV1.ImportMany]
            public List<IPublicInterface> ImportingProperty { get; set; } = null!;
        }

        [MefV1.Export]
        public class PartWithImportManyOfInternalPartViaInternalInterfaceInitialized
        {
            public PartWithImportManyOfInternalPartViaInternalInterfaceInitialized()
            {
                this.ImportingProperty = new List<IInternalInterface>();
            }

            [MefV1.ImportMany]
            internal List<IInternalInterface> ImportingProperty { get; set; } = null!;
        }

        [MefV1.Export]
        public class PartWithImportManyOfInternalPartViaInternalInterface
        {
            [MefV1.ImportMany]
            internal List<IInternalInterface> ImportingProperty { get; set; } = null!;
        }

        [MefV1.Export]
        internal class PartWithLazyImportOfInternalPartViaInternalInterface
        {
            [MefV1.Import]
            internal Lazy<IInternalInterface> ImportingProperty { get; set; } = null!;
        }

        [MefV1.Export]
        internal class PartWithLazyImportManyOfInternalPartViaInternalInterface
        {
            [MefV1.ImportMany]
            internal List<Lazy<IInternalInterface>> ImportingProperty { get; set; } = null!;
        }

        [MefV1.Export]
        public class PartWithLazyImportMetadataOfInternalPartViaPublicInterface
        {
            [MefV1.Import]
            public Lazy<IPublicInterface, IDictionary<string, object>> ImportingProperty { get; set; } = null!;
        }

        [MefV1.Export]
        internal class PartWithLazyImportMetadataOfInternalPartViaInternalInterface
        {
            [MefV1.Import]
            internal Lazy<IInternalInterface, IDictionary<string, object>> ImportingProperty { get; set; } = null!;
        }

        internal interface IInternalInterface { }

        [MefV1.Export]
        [MefV2.Export, MefV2.Shared]
        internal class InternalExport
        {
            [MefV1.Import]
            [MefV2.Import]
            public PublicExport PublicImportingProperty { get; set; } = null!;

            [MefV1.Import]
            [MefV2.Import]
            internal PublicExport InternalImportingProperty { get; set; } = null!;
        }

        [MefV1.Export, MefV1.PartCreationPolicy(MefV1.CreationPolicy.NonShared)]
        internal class InternalExportNonShared
        {
            [MefV1.Import]
            public PublicExport PublicImportingProperty { get; set; } = null!;

            [MefV1.Import]
            internal PublicExport InternalImportingProperty { get; set; } = null!;
        }

        [MefV1.Export]
        internal class InternalPartImportingInternalPart
        {
            [MefV1.Import]
            internal Lazy<InternalExport> InternalImportingProperty { get; set; } = null!;
        }

        [MefV1.Export]
        internal class InternalPartWithImportManyOfInternalPart
        {
            [MefV1.ImportMany]
            internal IEnumerable<Lazy<InternalExport>> InternalImportingProperty { get; set; } = null!;
        }

        [MefV1.Export]
        internal class InternalGenericExport<T>
        {
            [MefV1.Import]
            public PublicExport PublicImportingProperty { get; set; } = null!;

            [MefV1.Import]
            internal PublicExport InternalImportingProperty { get; set; } = null!;
        }

        [MefV1.Export(typeof(IInternalInterface))]
        public class PublicExportOfInternalInterface : IInternalInterface
        {
            [MefV1.Import]
            public PublicExport PublicImportingProperty { get; set; } = null!;

            [MefV1.Import]
            internal PublicExport InternalImportingProperty { get; set; } = null!;
        }

        internal class InternalTypeWithExportingMember
        {
            [MefV1.Export]
            internal int InternalExportingInt
            {
                get { return 3; }
            }

            [MefV1.Export]
            public string PublicExportingString
            {
                get { return "Hi"; }
            }
        }

        [MefV1.Export]
        public class PartThatImportsInternalMemberExports
        {
            [MefV1.Import]
            public int ImportedInt { get; set; }

            [MefV1.Import]
            public string ImportedString { get; set; } = null!;
        }

        [MefV1.Export]
        [MefV2.Export, MefV2.Shared]
        public class PublicExport { }

        [MefV1.Export]
        public class ExportWithPrivateImportingProperty
        {
            [MefV1.Import]
            private PublicExport ImportingProperty { get; set; } = null!;

            internal PublicExport InternalAccessor
            {
                get { return this.ImportingProperty; }
            }
        }

        public class ExportWithPrivateImportingPropertySetterBase
        {
            [MefV1.Import]
            public PublicExport ImportingProperty { get; private set; } = null!;
        }

        [MefV1.Export]
        public class ExportWithPrivateImportingPropertySetter : ExportWithPrivateImportingPropertySetterBase
        {
        }

        [MefV1.Export]
        public class OpenGenericPartWithPrivateImportingProperty<T>
        {
            [MefV1.Import]
            private PublicExport ImportingProperty { get; set; } = null!;

            internal PublicExport InternalAccessor
            {
                get { return this.ImportingProperty; }
            }
        }

        [MefV1.Export]
        public class ExportWithPrivateImportingField
        {
            [MefV1.Import]
            private PublicExport importingField = null!;

            internal PublicExport InternalAccessor
            {
                get { return this.importingField; }
            }
        }

        [MefV1.Export]
        public class OpenGenericPartWithPrivateImportingField<T>
        {
            [MefV1.Import]
            private PublicExport importingField = null!;

            internal PublicExport InternalAccessor
            {
                get { return this.importingField; }
            }
        }

        public class PartWithPrivateExportingProperty
        {
            [MefV1.Export]
            private string ExportingProperty
            {
                get { return "Success"; }
            }
        }

        [MefV1.Export]
        public class PartThatImportsPrivateExportingProperty
        {
            [MefV1.Import]
            public string Value { get; set; } = null!;
        }

        public class PartWithPrivateExportingField
        {
            [MefV1.Export]
            private string exportingField = "Success";

            public string FieldAccessor
            {
                get { return this.exportingField; }
            }
        }

        [MefV1.Export]
        public class PartThatImportsPrivateExportingField
        {
            [MefV1.Import]
            public string Value { get; set; } = null!;
        }

        public class PartWithPrivateExportedMethod
        {
            [MefV1.Export]
            private string GetValue()
            {
                return "Success";
            }
        }

        [MefV1.Export]
        public class PartWithImportOfPrivateExportedMethod
        {
            [MefV1.Import]
            public Func<string> Value { get; set; } = null!;
        }

        public class PublicBaseClassWithImports
        {
            [MefV1.Import]
            public PublicExport ImportingProperty { get; set; } = null!;
        }

        [MefV1.Export]
        internal class InternalPartDerivingFromPublicClass : PublicBaseClassWithImports { }

        [MefV1.Export]
        public class PartWithPrivateImportManyFieldArray
        {
            [MefV1.ImportMany]
            private PublicExport[] importManyField = null!;

            internal PublicExport[] ImportManyFieldAccessor
            {
                get { return this.importManyField; }
            }
        }

        [MefV1.Export]
        public class PartWithPrivateImportManyFieldSpecialCollection
        {
            [MefV1.ImportMany]
            private List<PublicExport> importManyField = null!;

            internal List<PublicExport> ImportManyFieldAccessor
            {
                get { return this.importManyField; }
            }
        }

        [MefV1.Export]
        public class PartWithPrivateImportManyPropertySpecialCollection
        {
            [MefV1.ImportMany]
            private List<PublicExport> ImportManyProperty { get; set; } = null!;

            internal List<PublicExport> ImportManyPropertyAccessor
            {
                get { return this.ImportManyProperty; }
            }
        }

        [MefV1.Export]
        public class PartWithPrivateImportManyFieldPrivateCollection
        {
            [MefV1.ImportMany]
            private CustomCollection<PublicExport> importManyField = null!;

            internal CustomCollection<PublicExport> ImportManyFieldAccessor
            {
                get { return this.importManyField; }
            }
        }

        [MefV1.Export]
        public class PartWithPrivateImportManyPropertyPrivateCollection
        {
            [MefV1.ImportMany]
            private CustomCollection<PublicExport> ImportManyProperty { get; set; } = null!;

            internal CustomCollection<PublicExport> ImportManyPropertyAccessor
            {
                get { return this.ImportManyProperty; }
            }
        }

        internal class CustomCollection<T> : ICollection<T>
        {
            private List<T> inner = new List<T>();

            public CustomCollection()
            {
            }

            internal CustomCollection(object arg)
            {
                this.ConstructorArg = arg;
            }

            public object? ConstructorArg { get; private set; }

            public bool Cleared { get; set; }

            public void Add(T item)
            {
                this.inner.Add(item);
            }

            public void Clear()
            {
                this.inner.Clear();
                this.Cleared = true;
            }

            public bool Contains(T item)
            {
                throw new NotImplementedException();
            }

            public void CopyTo(T[] array, int arrayIndex)
            {
                throw new NotImplementedException();
            }

            public int Count
            {
                get { return this.inner.Count; }
            }

            public bool IsReadOnly
            {
                get { return false; }
            }

            public bool Remove(T item)
            {
                throw new NotImplementedException();
            }

            public IEnumerator<T> GetEnumerator()
            {
                return this.inner.GetEnumerator();
            }

            System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
            {
                return this.GetEnumerator();
            }
        }
    }
}
