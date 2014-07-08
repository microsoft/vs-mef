namespace Microsoft.VisualStudio.Composition.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Xunit;
    using MefV1 = System.ComponentModel.Composition;

    [Trait("Export", "Inherited")]
    public class InheritedExportTests
    {
        #region Abstract base class tests

        [MefFact(CompositionEngines.V1Compat, typeof(AbstractBaseClass), typeof(DerivedOfAbstractClass))]
        public void InheritedExportDoesNotApplyToAbstractBaseClasses(IContainer container)
        {
            var derived = container.GetExportedValue<AbstractBaseClass>();
            Assert.IsType<DerivedOfAbstractClass>(derived);
        }

        [MefFact(CompositionEngines.V1Compat, typeof(AbstractBaseClass), typeof(DerivedOfAbstractClass))]
        public void ExportMetadataOnlyVisibleAtExportSite(IContainer container)
        {
            var derived = container.GetExport<AbstractBaseClass, IDictionary<string, object>>();
            Assert.Equal(1, derived.Metadata["a"]);
            Assert.False(derived.Metadata.ContainsKey("b"));

            derived = container.GetExport<AbstractBaseClass, IDictionary<string, object>>("InheritedExportOnBoth");
            Assert.False(derived.Metadata.ContainsKey("a"));
            Assert.Equal(2, derived.Metadata["b"]);
        }

        [MefFact(CompositionEngines.V1Compat, typeof(AbstractBaseClass))]
        public void MetadataOnAbstractClassExportIsInaccessible(IContainer container)
        {
            Assert.Throws<CompositionFailedException>(() => container.GetExport<AbstractBaseClass, IDictionary<string, object>>());
        }

        [MefV1.InheritedExport]
        [MefV1.InheritedExport("InheritedExportOnBoth")]
        [MefV1.ExportMetadata("a", 1)]
        public abstract class AbstractBaseClass { }

        [MefV1.InheritedExport("InheritedExportOnBoth", typeof(AbstractBaseClass))]
        [MefV1.ExportMetadata("b", 2)]
        public class DerivedOfAbstractClass : AbstractBaseClass { }

        #endregion

        #region Concrete base class tests

        [MefFact(CompositionEngines.V1Compat, typeof(BaseClass), typeof(DerivedClass))]
        public void InheritedExportAppliesToConcreteBaseClasses(IContainer container)
        {
            var exports = container.GetExportedValues<BaseClass>();
            Assert.Equal(2, exports.Count());
            Assert.Equal(1, exports.OfType<DerivedClass>().Count());

            exports = container.GetExportedValues<DerivedClass>();
            Assert.Equal(0, exports.Count());
        }

        [MefV1.InheritedExport]
        public class BaseClass { }

        public class DerivedClass : BaseClass { }

        #endregion

        #region ExportAttribute does not inherit

        [MefFact(CompositionEngines.V1Compat, typeof(BaseClassWithExport), typeof(DerivedTypeOfExportedClass))]
        public void ExportDoesNotInherit(IContainer container)
        {
            Assert.IsType<BaseClassWithExport>(container.GetExportedValue<BaseClassWithExport>());
        }

        [MefV1.Export]
        public class BaseClassWithExport { }

        public class DerivedTypeOfExportedClass : BaseClassWithExport { }

        #endregion

        #region ExportAttribute does not double up in inheritance tree.

        [MefFact(CompositionEngines.V1Compat, typeof(BaseClassWithCustomExport), typeof(DerivedTypeOfExportedClassWithItsOwnExport), typeof(PartWithImportManyOfBaseClassWithCustomExport))]
        public void MultipleInheritedExportsAlongInheritanceTree(IContainer container)
        {
            var part = container.GetExportedValue<PartWithImportManyOfBaseClassWithCustomExport>();
            Assert.Equal(2, part.InheritedExportBothPlaces.Count);
            Assert.Equal(1, part.InheritedExportBothPlaces.Select(v => v.Value).OfType<DerivedTypeOfExportedClassWithItsOwnExport>().Count());

            var derivedPart = container.GetExportedValue<DerivedTypeOfExportedClassWithItsOwnExport>();
            Assert.NotNull(derivedPart);
        }

        [MefFact(CompositionEngines.V1Compat, typeof(BaseClassWithCustomExport), typeof(DerivedTypeOfExportedClassWithItsOwnExport), typeof(PartWithImportManyOfBaseClassWithCustomExport))]
        public void InheritedExportsInBaseAndExportInDerived(IContainer container)
        {
            var part = container.GetExportedValue<PartWithImportManyOfBaseClassWithCustomExport>();
            Assert.Equal(3, part.InheritedExportOnBaseType.Count);
            Assert.Equal(2, part.InheritedExportOnBaseType.Select(v => v.Value).OfType<DerivedTypeOfExportedClassWithItsOwnExport>().Count());
        }

        [MefV1.InheritedExport("InheritedExportBothPlaces", typeof(BaseClassWithCustomExport))]
        [MefV1.InheritedExport("InheritedExportOnBaseOnly", typeof(BaseClassWithCustomExport))]
        [MefV1.ExportMetadata("DefinedOn", "Base")]
        [MefV1.ExportMetadata("UniqueToBase", "true")]
        public class BaseClassWithCustomExport { }

        [MefV1.InheritedExport("InheritedExportBothPlaces", typeof(BaseClassWithCustomExport))]
        [MefV1.Export("InheritedExportOnBaseOnly", typeof(BaseClassWithCustomExport))]
        [MefV1.Export] // also export its own contract type
        [MefV1.ExportMetadata("DefinedOn", "Derived")]
        [MefV1.ExportMetadata("UniqueToDerived", "true")]
        public class DerivedTypeOfExportedClassWithItsOwnExport : BaseClassWithCustomExport { }

        [MefV1.Export]
        public class PartWithImportManyOfBaseClassWithCustomExport
        {
            [MefV1.ImportMany("InheritedExportBothPlaces")]
            public List<Lazy<BaseClassWithCustomExport, IDictionary<string, object>>> InheritedExportBothPlaces { get; set; }

            [MefV1.ImportMany("InheritedExportOnBaseOnly")]
            public List<Lazy<BaseClassWithCustomExport, IDictionary<string, object>>> InheritedExportOnBaseType { get; set; }
        }

        #endregion

        #region Open generic inheriting exports

        [Trait("GenericExports", "Open")]
        [MefFact(CompositionEngines.V1Compat, typeof(AbstractBaseClass), typeof(GenericDerived<>))]
        public void InheritedExportOnAbstractNonGenericBaseWithGenericDerived(IContainer container)
        {
            var exports = container.GetExportedValues<AbstractBaseClass>();
            Assert.Equal(0, exports.Count());
        }

        [Trait("GenericExports", "Open")]
        [MefFact(CompositionEngines.V1Compat, typeof(GenericBase<>), typeof(ClosedDerivedOfGeneric))]
        public void InheritedExportOnAbstractGenericBaseWithNonGenericDerived(IContainer container)
        {
            var exports = container.GetExportedValues<GenericBase<int>>();
            Assert.Equal(1, exports.Count());

            var exports2 = container.GetExportedValues<GenericBase<double>>();
            Assert.Equal(0, exports2.Count());
        }

        public class GenericDerived<T> : AbstractBaseClass { }

        [MefV1.InheritedExport]
        public abstract class GenericBase<T> { }

        public class ClosedDerivedOfGeneric : GenericBase<int> { }

        #endregion
    }
}
