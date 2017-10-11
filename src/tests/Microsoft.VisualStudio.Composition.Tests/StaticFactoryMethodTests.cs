// Copyright (c) Microsoft. All rights reserved.

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
            var someOtherExportPart = discoverer.CreatePart(typeof(SomeOtherExport));
            var staticFactoryPart = discoverer.CreatePart(typeof(MEFPartWithStaticFactoryMethod));
            var staticFactoryMethodRef = MethodRef.Get(typeof(MEFPartWithStaticFactoryMethod).GetTypeInfo().DeclaredMethods.Single(m => m.Name == nameof(MEFPartWithStaticFactoryMethod.Create)), Resolver.DefaultInstance);
            staticFactoryPart = new ComposablePartDefinition(
                staticFactoryPart.TypeRef,
                staticFactoryPart.Metadata,
                staticFactoryPart.ExportedTypes,
                staticFactoryPart.ExportingMembers,
                staticFactoryPart.ImportingMembers,
                staticFactoryPart.SharingBoundary,
                staticFactoryPart.OnImportsSatisfiedRef,
                staticFactoryMethodRef,
                staticFactoryPart.ImportingConstructorImports.Take(1).ToList(),
                staticFactoryPart.CreationPolicy,
                staticFactoryPart.IsSharingBoundaryInferred);

            var catalog = ComposableCatalog.Create(Resolver.DefaultInstance)
                .AddParts(new[] { someOtherExportPart, staticFactoryPart });
            var configuration = CompositionConfiguration.Create(catalog);
            if (!configuration.CompositionErrors.IsEmpty)
            {
                foreach (var error in configuration.CompositionErrors.Peek())
                {
                    this.logger.WriteLine(error.Message);
                }

                configuration.ThrowOnErrors();
            }

            var container = await configuration.CreateContainerAsync(this.logger);

            SomeOtherExport anotherExport = container.GetExportedValue<SomeOtherExport>();
            MEFPartWithStaticFactoryMethod mefPart = container.GetExportedValue<MEFPartWithStaticFactoryMethod>();

            Assert.NotNull(mefPart.SomeOtherExport);
            Assert.Same(anotherExport, mefPart.SomeOtherExport);
            Assert.True(mefPart.AnotherRandomValue);
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
    }
}
