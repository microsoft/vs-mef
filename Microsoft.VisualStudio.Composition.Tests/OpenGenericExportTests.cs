namespace Microsoft.VisualStudio.Composition.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Composition;
    using System.Composition.Hosting;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Xunit;
    using MefV1 = System.ComponentModel.Composition;

    [Trait("GenericExports", "Open")]
    public class OpenGenericExportTests
    {
        [MefFact(CompositionEngines.V1Compat | CompositionEngines.V2Compat, typeof(Useful<>), typeof(User))]
        public void AcquireOpenGenericExport(IContainer container)
        {
            Useful<int> useful = container.GetExportedValue<Useful<int>>();
            Assert.NotNull(useful);
        }

        [MefFact(CompositionEngines.V1Compat | CompositionEngines.V2Compat, typeof(Useful<>), typeof(User))]
        public void AcquireExportWithImportOfOpenGenericExport(IContainer container)
        {
            User user = container.GetExportedValue<User>();
            Assert.NotNull(user);
            Assert.NotNull(user.Useful);
        }

        [MefFact(CompositionEngines.V1Compat | CompositionEngines.V2Compat, typeof(OpenGenericPartWithPrivateExportingProperty<>), InvalidConfiguration = true)]
        public void ExportingPropertyOnGenericPart(IContainer container)
        {
            string result = container.GetExportedValue<string>();
        }

        [MefFact(CompositionEngines.V1Compat, typeof(OpenGenericPartWithPrivateExportingField<>), InvalidConfiguration = true)]
        public void ExportingFieldOnGenericPart(IContainer container)
        {
            string result = container.GetExportedValue<string>();
        }

        [Export]
        [MefV1.Export, MefV1.PartCreationPolicy(MefV1.CreationPolicy.NonShared)]
        public class Useful<T> { }

        [Export]
        [MefV1.Export, MefV1.PartCreationPolicy(MefV1.CreationPolicy.NonShared)]
        public class User
        {
            [Import]
            [MefV1.Import]
            public Useful<int> Useful { get; set; }
        }

        public class OpenGenericPartWithPrivateExportingProperty<T>
        {
            [MefV1.Export]
            [Export]
            public string ExportingProperty
            {
                get { return "Success"; }
            }
        }

        public class OpenGenericPartWithPrivateExportingField<T>
        {
            [MefV1.Export]
            public string ExportingField = "Success";
        }
    }
}
