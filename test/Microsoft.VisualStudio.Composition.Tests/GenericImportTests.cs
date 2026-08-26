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
    using MefV1 = System.ComponentModel.Composition;

    public class GenericImportTests
    {
        /// <summary>
        /// This is a very difficult scenario to support in MEFv3, since it means much of the graph
        /// could change based on the type argument. We may never be able to support it.
        /// </summary>
        [MefFact(CompositionEngines.V1 | CompositionEngines.V2, NoCompatGoal = true)]
        public void GenericPartImportsTypeParameter(IContainer container)
        {
            var genericPart = container.GetExportedValue<PartThatImportsT<SomeOtherPart>>();
            Assert.NotNull(genericPart);
            Assert.IsType<SomeOtherPart>(genericPart.Value);

            var genericPartViaCtor = container.GetExportedValue<PartThatImportsTViaImportingConstructor<SomeOtherPart>>();
            Assert.NotNull(genericPartViaCtor);
            Assert.IsType<SomeOtherPart>(genericPartViaCtor.Value);

            var genericPartLazy = container.GetExportedValue<PartThatImportsLazyT<SomeOtherPart>>();
            Assert.NotNull(genericPartLazy);
            Assert.IsType<SomeOtherPart>(genericPartLazy.Value.Value);

            var genericPartArray = container.GetExportedValue<PartThatImportsArrayOfT<SomeOtherPart>>();
            Assert.NotNull(genericPartArray);
            Assert.IsType<SomeOtherPart>(genericPartArray.Value[0]);

            var genericPartList = container.GetExportedValue<PartThatImportsEnumerableOfT<SomeOtherPart>>();
            Assert.NotNull(genericPartList);
            Assert.IsType<SomeOtherPart>(genericPartList.Value.First());
        }

        [MefFact(CompositionEngines.V3EmulatingV1 | CompositionEngines.V3EmulatingV2 | CompositionEngines.V3AllowConfigurationWithErrors, InvalidConfiguration = true)]
        public void GenericPartImportsTypeParameterFailsGracefullyInV3(IContainer container)
        {
            Assert.NotNull(container.GetExportedValue<SomeOtherPart>());
            Assert.Empty(container.GetExportedValues<PartThatImportsT<SomeOtherPart>>());
            Assert.Empty(container.GetExportedValues<PartThatImportsTViaImportingConstructor<SomeOtherPart>>());
            Assert.Empty(container.GetExportedValues<PartThatImportsLazyT<SomeOtherPart>>());
            Assert.Empty(container.GetExportedValues<PartThatImportsArrayOfT<SomeOtherPart>>());
            Assert.Empty(container.GetExportedValues<PartThatImportsEnumerableOfT<SomeOtherPart>>());
        }

        [Export, Shared, MefV1.Export]
        public class PartThatImportsT<T>
        {
            [Import, MefV1.Import]
            public T Value { get; set; } = default!;
        }

        [Export, Shared, MefV1.Export]
        public class PartThatImportsTViaImportingConstructor<T>
        {
            [ImportingConstructor, MefV1.ImportingConstructor]
            public PartThatImportsTViaImportingConstructor(T value)
            {
                this.Value = value;
            }

            public T Value { get; }
        }

        [Export, Shared, MefV1.Export]
        public class PartThatImportsLazyT<T>
        {
            [Import, MefV1.Import]
            public Lazy<T> Value { get; set; } = null!;
        }

        [Export, Shared, MefV1.Export]
        public class PartThatImportsArrayOfT<T>
        {
            [ImportMany, MefV1.ImportMany]
            public T[] Value { get; set; } = null!;
        }

        [Export, Shared, MefV1.Export]
        public class PartThatImportsEnumerableOfT<T>
        {
            [ImportMany, MefV1.ImportMany]
            public IEnumerable<T> Value { get; set; } = null!;
        }

        [Export, Shared, MefV1.Export]
        public class SomeOtherPart { }

        /// <summary>
        /// Tests that a generic part whose import is a generic type parameterized by the part's
        /// own type parameter (e.g. IOptionsFactory&lt;TOptions&gt;) can be satisfied by an open
        /// generic export of that interface (e.g. [Export(typeof(IOptionsFactory&lt;&gt;))]).
        /// This is the GenericHost/Options DI pattern from issue #457.
        /// </summary>
        [MefFact(CompositionEngines.V3EmulatingV1 | CompositionEngines.V3EmulatingV2, typeof(ParameterizedGenericImport_OptionsFactory<>), typeof(ParameterizedGenericImport_OptionsManager<>), typeof(ParameterizedGenericImport_App))]
        public void GenericPartImportsParameterizedGenericMatchingOpenGenericExport(IContainer container)
        {
            var app = container.GetExportedValue<ParameterizedGenericImport_App>();
            Assert.NotNull(app);
            Assert.NotNull(app.Manager);
            Assert.NotNull(app.Manager.Factory);
            Assert.IsType<ParameterizedGenericImport_OptionsFactory<ParameterizedGenericImport_MyOptions>>(app.Manager.Factory);
        }

        public interface IParameterizedGenericImport_OptionsFactory<T>
        {
            T Create();
        }

        [Export(typeof(IParameterizedGenericImport_OptionsFactory<>)), Shared]
        [MefV1.Export(typeof(IParameterizedGenericImport_OptionsFactory<>))]
        public class ParameterizedGenericImport_OptionsFactory<T> : IParameterizedGenericImport_OptionsFactory<T>
        {
            public T Create() => Activator.CreateInstance<T>();
        }

        [Export, Shared]
        [MefV1.Export]
        public class ParameterizedGenericImport_OptionsManager<TOptions>
        {
            [ImportingConstructor]
            [MefV1.ImportingConstructor]
            public ParameterizedGenericImport_OptionsManager(IParameterizedGenericImport_OptionsFactory<TOptions> factory)
            {
                this.Factory = factory;
            }

            public IParameterizedGenericImport_OptionsFactory<TOptions> Factory { get; }
        }

        [Export, Shared]
        [MefV1.Export]
        public class ParameterizedGenericImport_App
        {
            [Import]
            [MefV1.Import]
            public ParameterizedGenericImport_OptionsManager<ParameterizedGenericImport_MyOptions> Manager { get; set; } = null!;
        }

        public class ParameterizedGenericImport_MyOptions { }

        [MefFact(CompositionEngines.V3EmulatingV1 | CompositionEngines.V3EmulatingV2, typeof(ParameterizedGenericImport_OptionsFactory<>), typeof(ParameterizedGenericImport_OptionsManager_Lazy<>), typeof(ParameterizedGenericImport_App_Lazy))]
        public void GenericPartImportsParameterizedGenericMatchingOpenGenericExport_Lazy(IContainer container)
        {
            var app = container.GetExportedValue<ParameterizedGenericImport_App_Lazy>();
            Assert.NotNull(app);
            Assert.NotNull(app.Manager);
            Assert.NotNull(app.Manager.Factory);
            Assert.IsType<ParameterizedGenericImport_OptionsFactory<ParameterizedGenericImport_MyOptions>>(app.Manager.Factory.Value);
        }

        [Export, Shared]
        [MefV1.Export]
        public class ParameterizedGenericImport_OptionsManager_Lazy<TOptions>
        {
            [ImportingConstructor]
            [MefV1.ImportingConstructor]
            public ParameterizedGenericImport_OptionsManager_Lazy(Lazy<IParameterizedGenericImport_OptionsFactory<TOptions>> factory)
            {
                this.Factory = factory;
            }

            public Lazy<IParameterizedGenericImport_OptionsFactory<TOptions>> Factory { get; }
        }

        [Export, Shared]
        [MefV1.Export]
        public class ParameterizedGenericImport_App_Lazy
        {
            [Import]
            [MefV1.Import]
            public ParameterizedGenericImport_OptionsManager_Lazy<ParameterizedGenericImport_MyOptions> Manager { get; set; } = null!;
        }

        [MefFact(CompositionEngines.V3EmulatingV2, typeof(ParameterizedGenericImport_OptionsFactory2<>), typeof(ParameterizedGenericImport_OptionsManager_ExportFactory<>), typeof(ParameterizedGenericImport_App_ExportFactory))]
        public void GenericPartImportsParameterizedGenericMatchingOpenGenericExport_ExportFactory(IContainer container)
        {
            var app = container.GetExportedValue<ParameterizedGenericImport_App_ExportFactory>();
            Assert.NotNull(app);
            Assert.NotNull(app.Manager);
            Assert.NotNull(app.Manager.Factory);
            using var export = app.Manager.Factory.CreateExport();
            Assert.IsType<ParameterizedGenericImport_OptionsFactory2<ParameterizedGenericImport_MyOptions>>(export.Value);
        }

        public interface IParameterizedGenericImport_OptionsFactory2<T> { }

        [Export(typeof(IParameterizedGenericImport_OptionsFactory2<>))]
        public class ParameterizedGenericImport_OptionsFactory2<T> : IParameterizedGenericImport_OptionsFactory2<T> { }

        [Export, Shared]
        public class ParameterizedGenericImport_OptionsManager_ExportFactory<TOptions>
        {
            [ImportingConstructor]
            public ParameterizedGenericImport_OptionsManager_ExportFactory(ExportFactory<IParameterizedGenericImport_OptionsFactory2<TOptions>> factory)
            {
                this.Factory = factory;
            }

            public ExportFactory<IParameterizedGenericImport_OptionsFactory2<TOptions>> Factory { get; }
        }

        [Export, Shared]
        public class ParameterizedGenericImport_App_ExportFactory
        {
            [Import]
            public ParameterizedGenericImport_OptionsManager_ExportFactory<ParameterizedGenericImport_MyOptions> Manager { get; set; } = null!;
        }

        [MefFact(CompositionEngines.V3EmulatingV1 | CompositionEngines.V3EmulatingV2, typeof(ParameterizedGenericImport_OptionsFactory3<>), typeof(ParameterizedGenericImport_OptionsManager_ImportManyEnumerable<>), typeof(ParameterizedGenericImport_App_ImportManyEnumerable))]
        public void GenericPartImportsParameterizedGenericMatchingOpenGenericExport_ImportManyEnumerable(IContainer container)
        {
            var app = container.GetExportedValue<ParameterizedGenericImport_App_ImportManyEnumerable>();
            Assert.NotNull(app);
            Assert.NotNull(app.Manager);
            Assert.NotNull(app.Manager.Factories);
            var factory = Assert.Single(app.Manager.Factories);
            Assert.IsType<ParameterizedGenericImport_OptionsFactory3<ParameterizedGenericImport_MyOptions>>(factory);
        }

        public interface IParameterizedGenericImport_OptionsFactory3<T> { }

        [Export(typeof(IParameterizedGenericImport_OptionsFactory3<>)), Shared]
        [MefV1.Export(typeof(IParameterizedGenericImport_OptionsFactory3<>))]
        public class ParameterizedGenericImport_OptionsFactory3<T> : IParameterizedGenericImport_OptionsFactory3<T> { }

        [Export, Shared]
        [MefV1.Export]
        public class ParameterizedGenericImport_OptionsManager_ImportManyEnumerable<TOptions>
        {
            [ImportMany]
            [MefV1.ImportMany]
            public IEnumerable<IParameterizedGenericImport_OptionsFactory3<TOptions>> Factories { get; set; } = null!;
        }

        [Export, Shared]
        [MefV1.Export]
        public class ParameterizedGenericImport_App_ImportManyEnumerable
        {
            [Import]
            [MefV1.Import]
            public ParameterizedGenericImport_OptionsManager_ImportManyEnumerable<ParameterizedGenericImport_MyOptions> Manager { get; set; } = null!;
        }

        [MefFact(CompositionEngines.V3EmulatingV1 | CompositionEngines.V3EmulatingV2, typeof(ParameterizedGenericImport_OptionsFactory4<>), typeof(ParameterizedGenericImport_OptionsManager_ImportManyArray<>), typeof(ParameterizedGenericImport_App_ImportManyArray))]
        public void GenericPartImportsParameterizedGenericMatchingOpenGenericExport_ImportManyArray(IContainer container)
        {
            var app = container.GetExportedValue<ParameterizedGenericImport_App_ImportManyArray>();
            Assert.NotNull(app);
            Assert.NotNull(app.Manager);
            Assert.NotNull(app.Manager.Factories);
            var factory = Assert.Single(app.Manager.Factories);
            Assert.IsType<ParameterizedGenericImport_OptionsFactory4<ParameterizedGenericImport_MyOptions>>(factory);
        }

        public interface IParameterizedGenericImport_OptionsFactory4<T> { }

        [Export(typeof(IParameterizedGenericImport_OptionsFactory4<>)), Shared]
        [MefV1.Export(typeof(IParameterizedGenericImport_OptionsFactory4<>))]
        public class ParameterizedGenericImport_OptionsFactory4<T> : IParameterizedGenericImport_OptionsFactory4<T> { }

        [Export, Shared]
        [MefV1.Export]
        public class ParameterizedGenericImport_OptionsManager_ImportManyArray<TOptions>
        {
            [ImportMany]
            [MefV1.ImportMany]
            public IParameterizedGenericImport_OptionsFactory4<TOptions>[] Factories { get; set; } = null!;
        }

        [Export, Shared]
        [MefV1.Export]
        public class ParameterizedGenericImport_App_ImportManyArray
        {
            [Import]
            [MefV1.Import]
            public ParameterizedGenericImport_OptionsManager_ImportManyArray<ParameterizedGenericImport_MyOptions> Manager { get; set; } = null!;
        }

        /// <summary>
        /// Verifies that the importing constructor of a generic part is located on the closed generic type
        /// even when the part declares more than one constructor, since the order of the constructors
        /// returned by reflection is not guaranteed to match between the open generic type and its closed form.
        /// </summary>
        [MefFact(CompositionEngines.V3EmulatingV1 | CompositionEngines.V3EmulatingV2, typeof(ParameterizedGenericImport_OptionsFactory5<>), typeof(ParameterizedGenericImport_OptionsManager_MultipleConstructors<>), typeof(ParameterizedGenericImport_App_MultipleConstructors))]
        public void GenericPartImportsParameterizedGenericMatchingOpenGenericExport_MultipleConstructors(IContainer container)
        {
            var app = container.GetExportedValue<ParameterizedGenericImport_App_MultipleConstructors>();
            Assert.NotNull(app);
            Assert.NotNull(app.Manager);
            Assert.IsType<ParameterizedGenericImport_OptionsFactory5<ParameterizedGenericImport_MyOptions>>(app.Manager.Factory);
        }

        public interface IParameterizedGenericImport_OptionsFactory5<T> { }

        [Export(typeof(IParameterizedGenericImport_OptionsFactory5<>)), Shared]
        [MefV1.Export(typeof(IParameterizedGenericImport_OptionsFactory5<>))]
        public class ParameterizedGenericImport_OptionsFactory5<T> : IParameterizedGenericImport_OptionsFactory5<T> { }

        [Export, Shared]
        [MefV1.Export]
        public class ParameterizedGenericImport_OptionsManager_MultipleConstructors<TOptions>
        {
            public ParameterizedGenericImport_OptionsManager_MultipleConstructors()
            {
            }

            public ParameterizedGenericImport_OptionsManager_MultipleConstructors(string unused)
            {
            }

            [ImportingConstructor]
            [MefV1.ImportingConstructor]
            public ParameterizedGenericImport_OptionsManager_MultipleConstructors(IParameterizedGenericImport_OptionsFactory5<TOptions> factory)
            {
                this.Factory = factory;
            }

            public IParameterizedGenericImport_OptionsFactory5<TOptions>? Factory { get; }
        }

        [Export, Shared]
        [MefV1.Export]
        public class ParameterizedGenericImport_App_MultipleConstructors
        {
            [Import]
            [MefV1.Import]
            public ParameterizedGenericImport_OptionsManager_MultipleConstructors<ParameterizedGenericImport_MyOptions> Manager { get; set; } = null!;
        }

        /// <summary>
        /// Verifies that a parameterized generic import wrapped in <see cref="Lazy{T, TMetadata}"/> (i.e. a
        /// <em>Lazy</em> with a metadata type argument) resolves, since the effective closed wrapper type must
        /// preserve the metadata type argument.
        /// </summary>
        [MefFact(CompositionEngines.V3EmulatingV1 | CompositionEngines.V3EmulatingV2, typeof(ParameterizedGenericImport_OptionsFactory6<>), typeof(ParameterizedGenericImport_OptionsManager_LazyWithMetadata<>), typeof(ParameterizedGenericImport_App_LazyWithMetadata))]
        public void GenericPartImportsParameterizedGenericMatchingOpenGenericExport_LazyWithMetadata(IContainer container)
        {
            var app = container.GetExportedValue<ParameterizedGenericImport_App_LazyWithMetadata>();
            Assert.NotNull(app.Manager);
            Assert.Equal("Factory6", app.Manager.Factory.Metadata["Name"]);
            Assert.IsType<ParameterizedGenericImport_OptionsFactory6<ParameterizedGenericImport_MyOptions>>(app.Manager.Factory.Value);
        }

        public interface IParameterizedGenericImport_OptionsFactory6<T> { }

        [Export(typeof(IParameterizedGenericImport_OptionsFactory6<>)), ExportMetadata("Name", "Factory6"), Shared]
        [MefV1.Export(typeof(IParameterizedGenericImport_OptionsFactory6<>)), MefV1.ExportMetadata("Name", "Factory6")]
        public class ParameterizedGenericImport_OptionsFactory6<T> : IParameterizedGenericImport_OptionsFactory6<T> { }

        [Export, Shared]
        [MefV1.Export]
        public class ParameterizedGenericImport_OptionsManager_LazyWithMetadata<TOptions>
        {
            [Import]
            [MefV1.Import]
            public Lazy<IParameterizedGenericImport_OptionsFactory6<TOptions>, IDictionary<string, object>> Factory { get; set; } = null!;
        }

        [Export, Shared]
        [MefV1.Export]
        public class ParameterizedGenericImport_App_LazyWithMetadata
        {
            [Import]
            [MefV1.Import]
            public ParameterizedGenericImport_OptionsManager_LazyWithMetadata<ParameterizedGenericImport_MyOptions> Manager { get; set; } = null!;
        }

        /// <summary>
        /// Verifies that a parameterized generic import wrapped in <see cref="ExportFactory{T, TMetadata}"/>
        /// resolves, since the effective closed wrapper type must preserve the metadata type argument.
        /// </summary>
        [MefFact(CompositionEngines.V3EmulatingV2, typeof(ParameterizedGenericImport_OptionsFactory7<>), typeof(ParameterizedGenericImport_OptionsManager_ExportFactoryWithMetadata<>), typeof(ParameterizedGenericImport_App_ExportFactoryWithMetadata))]
        public void GenericPartImportsParameterizedGenericMatchingOpenGenericExport_ExportFactoryWithMetadata(IContainer container)
        {
            var app = container.GetExportedValue<ParameterizedGenericImport_App_ExportFactoryWithMetadata>();
            Assert.NotNull(app.Manager);
            Assert.Equal("Factory7", app.Manager.Factory.Metadata["Name"]);
            using var export = app.Manager.Factory.CreateExport();
            Assert.IsType<ParameterizedGenericImport_OptionsFactory7<ParameterizedGenericImport_MyOptions>>(export.Value);
        }

        public interface IParameterizedGenericImport_OptionsFactory7<T> { }

        [Export(typeof(IParameterizedGenericImport_OptionsFactory7<>)), ExportMetadata("Name", "Factory7")]
        public class ParameterizedGenericImport_OptionsFactory7<T> : IParameterizedGenericImport_OptionsFactory7<T> { }

        [Export, Shared]
        public class ParameterizedGenericImport_OptionsManager_ExportFactoryWithMetadata<TOptions>
        {
            [Import]
            public ExportFactory<IParameterizedGenericImport_OptionsFactory7<TOptions>, IDictionary<string, object>> Factory { get; set; } = null!;
        }

        [Export, Shared]
        public class ParameterizedGenericImport_App_ExportFactoryWithMetadata
        {
            [Import]
            public ParameterizedGenericImport_OptionsManager_ExportFactoryWithMetadata<ParameterizedGenericImport_MyOptions> Manager { get; set; } = null!;
        }

        /// <summary>
        /// Verifies that a parameterized generic import collected by <see cref="MefV1.ImportManyAttribute"/> into a
        /// custom collection of <see cref="Lazy{T}"/> resolves, since the effective collection type must be built
        /// around the wrapped element type rather than the unwrapped contract type.
        /// </summary>
        [MefFact(CompositionEngines.V3EmulatingV1 | CompositionEngines.V3EmulatingV2, typeof(ParameterizedGenericImport_OptionsFactory8<>), typeof(ParameterizedGenericImport_OptionsManager_ImportManyLazyList<>), typeof(ParameterizedGenericImport_App_ImportManyLazyList))]
        public void GenericPartImportsParameterizedGenericMatchingOpenGenericExport_ImportManyLazyList(IContainer container)
        {
            var app = container.GetExportedValue<ParameterizedGenericImport_App_ImportManyLazyList>();
            Assert.NotNull(app.Manager);
            var factory = Assert.Single(app.Manager.Factories);
            Assert.IsType<ParameterizedGenericImport_OptionsFactory8<ParameterizedGenericImport_MyOptions>>(factory.Value);
        }

        public interface IParameterizedGenericImport_OptionsFactory8<T> { }

        [Export(typeof(IParameterizedGenericImport_OptionsFactory8<>)), Shared]
        [MefV1.Export(typeof(IParameterizedGenericImport_OptionsFactory8<>))]
        public class ParameterizedGenericImport_OptionsFactory8<T> : IParameterizedGenericImport_OptionsFactory8<T> { }

        [Export, Shared]
        [MefV1.Export]
        public class ParameterizedGenericImport_OptionsManager_ImportManyLazyList<TOptions>
        {
            [ImportMany]
            [MefV1.ImportMany]
            public List<Lazy<IParameterizedGenericImport_OptionsFactory8<TOptions>>> Factories { get; set; } = null!;
        }

        [Export, Shared]
        [MefV1.Export]
        public class ParameterizedGenericImport_App_ImportManyLazyList
        {
            [Import]
            [MefV1.Import]
            public ParameterizedGenericImport_OptionsManager_ImportManyLazyList<ParameterizedGenericImport_MyOptions> Manager { get; set; } = null!;
        }

        /// <summary>
        /// Verifies that a *partially* open generic import (only some type arguments are the part's own type
        /// parameters, e.g. <c>IFactory&lt;TOptions, int&gt;</c>) is rejected while scanning the part, rather than
        /// producing a part that fails with a confusing error later while the composition is computed.
        /// </summary>
        /// <param name="attributesDiscovery">The discovery engine to test.</param>
        [Theory]
        [InlineData(CompositionEngines.V1)]
        [InlineData(CompositionEngines.V2)]
        public async Task PartiallyOpenGenericImportFailsGracefully(CompositionEngines attributesDiscovery)
        {
            PartDiscovery discovery = attributesDiscovery == CompositionEngines.V1 ? TestUtilities.V1Discovery : TestUtilities.V2Discovery;
            DiscoveredParts parts = await discovery.CreatePartsAsync(
                typeof(PartiallyOpenGenericImport_Factory<,>),
                typeof(PartiallyOpenGenericImport_Manager<>),
                typeof(PartiallyOpenGenericImport_App));

            PartDiscoveryException error = Assert.Single(parts.DiscoveryErrors);
            Assert.Same(typeof(PartiallyOpenGenericImport_Manager<>), error.ScannedType);

            // The part that imports the rejected one cannot be satisfied, but that is reported as a
            // composition error rather than thrown while the configuration is computed.
            CompositionConfiguration configuration = CompositionConfiguration.Create(TestUtilities.EmptyCatalog.AddParts(parts));
            Assert.NotEmpty(configuration.CompositionErrors);
        }

        public interface IPartiallyOpenGenericImport_Factory<T1, T2> { }

        [Export(typeof(IPartiallyOpenGenericImport_Factory<,>)), Shared]
        [MefV1.Export(typeof(IPartiallyOpenGenericImport_Factory<,>))]
        public class PartiallyOpenGenericImport_Factory<T1, T2> : IPartiallyOpenGenericImport_Factory<T1, T2> { }

        [Export, Shared]
        [MefV1.Export]
        public class PartiallyOpenGenericImport_Manager<TOptions>
        {
            [Import]
            [MefV1.Import]
            public IPartiallyOpenGenericImport_Factory<TOptions, int> Factory { get; set; } = null!;
        }

        [Export, Shared]
        [MefV1.Export]
        public class PartiallyOpenGenericImport_App
        {
            [Import]
            [MefV1.Import]
            public PartiallyOpenGenericImport_Manager<ParameterizedGenericImport_MyOptions> Manager { get; set; } = null!;
        }
    }
}
