// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.Composition.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Composition;
    using System.Linq;
    using System.Reflection;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.VisualStudio.Composition.Reflection;
    using Xunit;
    using Xunit.Abstractions;

    public class StaticFactoryMethodTests
    {
        private readonly ITestOutputHelper logger;

        public StaticFactoryMethodTests(ITestOutputHelper logger)
        {
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        [Fact]
        public async Task StaticFactoryMethodCanCreateMEFPart()
        {
            var discoverer = new AttributedPartDiscovery(Resolver.DefaultInstance, isNonPublicSupported: true);
            var someOtherExportPart = discoverer.CreatePart(typeof(SomeOtherExport))!;
            var staticFactoryPart = WithStaticFactoryMethod(discoverer.CreatePart(typeof(MEFPartWithStaticFactoryMethod))!, nameof(MEFPartWithStaticFactoryMethod.Create));

            var container = await this.CreateContainerAsync(someOtherExportPart, staticFactoryPart);

            SomeOtherExport anotherExport = container.GetExportedValue<SomeOtherExport>();
            MEFPartWithStaticFactoryMethod mefPart = container.GetExportedValue<MEFPartWithStaticFactoryMethod>();

            Assert.NotNull(mefPart.SomeOtherExport);
            Assert.Same(anotherExport, mefPart.SomeOtherExport);
            Assert.True(mefPart.AnotherRandomValue);
        }

        /// <summary>
        /// Verifies that a static factory method of an open generic part whose parameter refers to the part's
        /// type parameter (<c>Create(IFoo&lt;T&gt;)</c>) is mapped to the closed generic part when activated.
        /// </summary>
        [Fact]
        public async Task StaticFactoryMethodCanCreateGenericMEFPartWithParameterizedGenericImport()
        {
            var discoverer = new AttributedPartDiscovery(Resolver.DefaultInstance, isNonPublicSupported: true);
            var factoryPart = discoverer.CreatePart(typeof(GenericOptionsFactory<>))!;
            var appPart = discoverer.CreatePart(typeof(GenericMEFPartWithStaticFactoryMethodApp))!;
            var staticFactoryPart = WithStaticFactoryMethod(discoverer.CreatePart(typeof(GenericMEFPartWithStaticFactoryMethod<>))!, nameof(GenericMEFPartWithStaticFactoryMethod<object>.Create));

            var container = await this.CreateContainerAsync(factoryPart, appPart, staticFactoryPart);

            GenericMEFPartWithStaticFactoryMethodApp app = container.GetExportedValue<GenericMEFPartWithStaticFactoryMethodApp>();

            Assert.IsType<GenericOptionsFactory<SomeOtherExport>>(app.Part.Factory);
            Assert.True(app.Part.AnotherRandomValue);
        }

        /// <summary>
        /// Replaces the importing constructor of a part with one of its static factory methods, which takes the
        /// leading parameters of the importing constructor.
        /// </summary>
        private static ComposablePartDefinition WithStaticFactoryMethod(ComposablePartDefinition part, string factoryMethodName)
        {
            MethodInfo factoryMethod = part.Type.GetTypeInfo().DeclaredMethods.Single(m => m.Name == factoryMethodName);
            return new ComposablePartDefinition(
                part.TypeRef,
                part.Metadata,
                part.ExportedTypes,
                part.ExportingMembers,
                part.ImportingMembers,
                part.SharingBoundary,
                part.OnImportsSatisfiedMethodRefs,
                MethodRef.Get(factoryMethod, Resolver.DefaultInstance),
                part.ImportingConstructorImports?.Take(factoryMethod.GetParameters().Length).ToList(),
                part.CreationPolicy,
                part.IsSharingBoundaryInferred);
        }

        private async Task<ExportProvider> CreateContainerAsync(params ComposablePartDefinition[] parts)
        {
            var catalog = ComposableCatalog.Create(Resolver.DefaultInstance).AddParts(parts);
            var configuration = CompositionConfiguration.Create(catalog);
            if (!configuration.CompositionErrors.IsEmpty)
            {
                foreach (var error in configuration.CompositionErrors.Peek())
                {
                    this.logger.WriteLine(error.Message);
                }

                configuration.ThrowOnErrors();
            }

            return await configuration.CreateContainerAsync(this.logger);
        }

        [Export]
        private class MEFPartWithStaticFactoryMethod
        {
            [ImportingConstructor] // This is so we can 'transfer' it to the static factory method in the test.
            private MEFPartWithStaticFactoryMethod(SomeOtherExport someOtherExport, bool anotherRandomValue)
            {
                this.SomeOtherExport = someOtherExport;
                this.AnotherRandomValue = anotherRandomValue;
            }

            public SomeOtherExport SomeOtherExport { get; }

            public bool AnotherRandomValue { get; }

            public static MEFPartWithStaticFactoryMethod Create(SomeOtherExport someOtherExport)
            {
                return new MEFPartWithStaticFactoryMethod(someOtherExport, true);
            }
        }

        [Export, Shared]
        private class SomeOtherExport
        {
        }

        private interface IGenericOptionsFactory<T>
        {
        }

        [Export(typeof(IGenericOptionsFactory<>)), Shared]
        private class GenericOptionsFactory<T> : IGenericOptionsFactory<T>
        {
        }

        [Export]
        private class GenericMEFPartWithStaticFactoryMethod<T>
        {
            [ImportingConstructor] // This is so we can 'transfer' it to the static factory method in the test.
            private GenericMEFPartWithStaticFactoryMethod(IGenericOptionsFactory<T> factory, bool anotherRandomValue)
            {
                this.Factory = factory;
                this.AnotherRandomValue = anotherRandomValue;
            }

            public IGenericOptionsFactory<T> Factory { get; }

            public bool AnotherRandomValue { get; }

            public static GenericMEFPartWithStaticFactoryMethod<T> Create(IGenericOptionsFactory<T> factory)
            {
                return new GenericMEFPartWithStaticFactoryMethod<T>(factory, true);
            }
        }

        [Export]
        private class GenericMEFPartWithStaticFactoryMethodApp
        {
            [Import]
            public GenericMEFPartWithStaticFactoryMethod<SomeOtherExport> Part { get; set; } = null!;
        }
    }
}
