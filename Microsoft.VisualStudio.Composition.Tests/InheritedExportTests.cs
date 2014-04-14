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

    public class InheritedExportTests
    {
        #region Abstract base class tests 

        [MefFact(CompositionEngines.V1, typeof(AbstractBaseClass), typeof(DerivedOfAbstractClass))]
        public void InheritedExportDoesNotApplyToAbstractBaseClasses(IContainer container)
        {
            var derived = container.GetExportedValue<AbstractBaseClass>();
            Assert.IsType<DerivedOfAbstractClass>(derived);
        }

        [MefV1.InheritedExport]
        public abstract class AbstractBaseClass { }

        public class DerivedOfAbstractClass : AbstractBaseClass { }

        #endregion

        #region Concrete base class tests 

        [MefFact(CompositionEngines.V1, typeof(BaseClass), typeof(DerivedClass))]
        public void InheritedExportAppliesToConcreteBaseClasses(IContainer container)
        {
            var exports = container.GetExportedValues<BaseClass>();
            Assert.Equal(2, exports.Count());
            Assert.Equal(1, exports.OfType<DerivedClass>().Count());
        }

        [MefV1.InheritedExport]
        public class BaseClass { }

        public class DerivedClass : BaseClass { }

        #endregion
    }
}
