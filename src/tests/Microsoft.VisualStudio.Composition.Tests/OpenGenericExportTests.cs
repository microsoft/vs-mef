// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

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

        [MefFact(CompositionEngines.V1Compat | CompositionEngines.V2Compat, typeof(Useful<>), typeof(Useful<,>))]
        public void AcquireOpenGenericExportArity2(IContainer container)
        {
            var useful1 = container.GetExportedValue<Useful<int>>();
            Assert.NotNull(useful1);

            var useful2 = container.GetExportedValue<Useful<int, byte>>();
            Assert.NotNull(useful2);
        }

        [MefFact(CompositionEngines.V1Compat | CompositionEngines.V2Compat, typeof(Useful<>), typeof(User))]
        public void AcquireExportWithImportOfOpenGenericExport(IContainer container)
        {
            User user = container.GetExportedValue<User>();
            Assert.NotNull(user);
            Assert.NotNull(user.Useful);
        }

        [MefFact(CompositionEngines.V1Compat | CompositionEngines.V2Compat, typeof(Useful<>), typeof(User), typeof(Useful<,>), typeof(UserOfTwoArity))]
        public void AcquireExportWithImportOfOpenGenericExportArity2(IContainer container)
        {
            var user = container.GetExportedValue<User>();
            Assert.NotNull(user);
            Assert.NotNull(user.Useful);

            var userOfTwo = container.GetExportedValue<UserOfTwoArity>();
            Assert.NotNull(userOfTwo);
            Assert.NotNull(userOfTwo.Useful);
        }

        [MefFact(CompositionEngines.V1Compat | CompositionEngines.V2Compat, typeof(Useful<>), typeof(ImportManyUser))]
        public void AcquireExportWithImportManyOfOpenGenericExport(IContainer container)
        {
            var user = container.GetExportedValue<ImportManyUser>();
            Assert.NotNull(user);
            Assert.NotNull(user.Useful);
            Assert.Equal(1, user.Useful.Length);
            Assert.IsType<Useful<int>>(user.Useful[0]);
        }

        [MefFact(CompositionEngines.V1Compat | CompositionEngines.V2Compat, typeof(Useful<>), typeof(ImportManyLazyUser))]
        public void AcquireExportWithImportManyLazyOfOpenGenericExport(IContainer container)
        {
            var user = container.GetExportedValue<ImportManyLazyUser>();
            Assert.NotNull(user);
            Assert.NotNull(user.Useful);
            Assert.Equal(1, user.Useful.Length);
            Assert.IsType<Useful<int>>(user.Useful[0].Value);
        }

        [MefFact(CompositionEngines.V1Compat | CompositionEngines.V2Compat, typeof(Useful<>))]
        [Trait("Container.GetExport", "Plural")]
        public void GetExportedValuesOfOpenGenericExport(IContainer container)
        {
            var usefuls = container.GetExportedValues<Useful<int>>();
            Assert.Equal(1, usefuls.Count());
            Assert.IsType<Useful<int>>(usefuls.First());
        }

        [MefFact(CompositionEngines.V1Compat | CompositionEngines.V2Compat, typeof(Useful<>))]
        [Trait("Container.GetExport", "Plural")]
        public void GetExportsOfLazyOpenGenericExport(IContainer container)
        {
            var usefuls = container.GetExports<Useful<int>>();
            Assert.Equal(1, usefuls.Count());
            Assert.IsType<Useful<int>>(usefuls.First().Value);
        }

        [MefFact(CompositionEngines.V1Compat | CompositionEngines.V2Compat, typeof(OpenGenericPartWithExportingProperty<>), InvalidConfiguration = true)]
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

        [Export, Shared]
        [MefV1.Export]
        public class Useful<T1, T2> { }

        [Export]
        [MefV1.Export, MefV1.PartCreationPolicy(MefV1.CreationPolicy.NonShared)]
        public class User
        {
            [Import]
            [MefV1.Import]
            public Useful<int> Useful { get; set; } = null!;
        }

        [Export]
        [MefV1.Export, MefV1.PartCreationPolicy(MefV1.CreationPolicy.NonShared)]
        public class UserOfTwoArity
        {
            [Import]
            [MefV1.Import]
            public Useful<int, byte> Useful { get; set; } = null!;
        }

        [Export]
        [MefV1.Export, MefV1.PartCreationPolicy(MefV1.CreationPolicy.NonShared)]
        public class ImportManyUser
        {
            [ImportMany]
            [MefV1.ImportMany]
            public Useful<int>[] Useful { get; set; } = null!;
        }

        [Export]
        [MefV1.Export, MefV1.PartCreationPolicy(MefV1.CreationPolicy.NonShared)]
        public class ImportManyLazyUser
        {
            [ImportMany]
            [MefV1.ImportMany]
            public Lazy<Useful<int>>[] Useful { get; set; } = null!;
        }

        public class OpenGenericPartWithExportingProperty<T>
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

        #region Sharing instance distinction tests

        [MefFact(CompositionEngines.V1Compat | CompositionEngines.V2Compat, typeof(SharedUserOfSharedUseful), typeof(SharedUseful<>))]
        public void OpenGenericExportSharedByTypeArg(IContainer container)
        {
            var part = container.GetExportedValue<SharedUserOfSharedUseful>();
            Assert.NotNull(part.Container);
            Assert.NotNull(part.Disposable);

            var d = container.GetExportedValue<SharedUseful<IDisposable>>();
            Assert.Same(d, part.Disposable);
            var c = container.GetExportedValue<SharedUseful<IContainer>>();
            Assert.Same(c, part.Container);
        }

        [MefFact(CompositionEngines.V1Compat | CompositionEngines.V3EmulatingV2WithNonPublic, typeof(SharedUserOfInternalSharedUseful), typeof(InternalSharedUseful<>))]
        public void OpenGenericExportSharedByTypeArgNonPublic(IContainer container)
        {
            var part = container.GetExportedValue<SharedUserOfInternalSharedUseful>();
            Assert.NotNull(part.Container);
            Assert.NotNull(part.Disposable);

            var d = container.GetExportedValue<InternalSharedUseful<IDisposable>>();
            Assert.Same(d, part.Disposable);
            var c = container.GetExportedValue<InternalSharedUseful<IContainer>>();
            Assert.Same(c, part.Container);
        }

        [Export(typeof(SharedUseful<>)), Shared]
        [MefV1.Export(typeof(SharedUseful<>))]
        public class SharedUseful<T> { }

        [Export, Shared]
        [MefV1.Export]
        public class SharedUserOfSharedUseful
        {
            [Import, MefV1.Import]
            public SharedUseful<IDisposable> Disposable { get; set; } = null!;

            [Import, MefV1.Import]
            public SharedUseful<IContainer> Container { get; set; } = null!;
        }

        [Export(typeof(InternalSharedUseful<>)), Shared]
        [MefV1.Export(typeof(InternalSharedUseful<>))]
        internal class InternalSharedUseful<T> { }

        [Export, Shared]
        [MefV1.Export]
        public class SharedUserOfInternalSharedUseful
        {
            [Import, MefV1.Import]
            internal InternalSharedUseful<IDisposable> Disposable { get; set; } = null!;

            [Import, MefV1.Import]
            internal InternalSharedUseful<IContainer> Container { get; set; } = null!;
        }

        #endregion

        #region Non-public tests

        [Trait("Access", "NonPublic")]
        [MefFact(CompositionEngines.V1Compat | CompositionEngines.V3EmulatingV2WithNonPublic, typeof(Useful<>), typeof(UserOfNonPublicNestedType), typeof(UserOfNonPublicNestedType.NonPublicNestedType))]
        public void NonPublicTypeArgOfOpenGenericExport(IContainer container)
        {
            var user = container.GetExportedValue<UserOfNonPublicNestedType>();
            Assert.NotNull(user.Importer);
        }

        [Trait("Access", "NonPublic")]
        [MefFact(CompositionEngines.V1Compat | CompositionEngines.V3EmulatingV2WithNonPublic, typeof(InternalUseful<>), typeof(UserOfNonPublicNestedType), typeof(UserOfNonPublicNestedType.NonPublicNestedType))]
        public void NonPublicTypeArgOfOpenGenericExportWithNonPublicPart(IContainer container)
        {
            var user = container.GetExportedValue<UserOfNonPublicNestedType>();
            Assert.NotNull(user.Importer);
        }

        [Export]
        [MefV1.Export, MefV1.PartCreationPolicy(MefV1.CreationPolicy.NonShared)]
        public class UserOfNonPublicNestedType
        {
            [Import, MefV1.Import]
            internal Useful<NonPublicNestedType> Importer { get; set; } = null!;

            internal class NonPublicNestedType { }
        }

        [Export(typeof(Useful<>))]
        [MefV1.Export(typeof(Useful<>)), MefV1.PartCreationPolicy(MefV1.CreationPolicy.NonShared)]
        internal class InternalUseful<T> : Useful<T> { }

        #endregion
    }
}
