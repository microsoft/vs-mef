// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.VisualStudio.Composition.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Linq;
    using System.Reflection;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.VisualStudio.Composition.Reflection;
    using Xunit;
    using Xunit.Abstractions;

    public class PartDiscoveryTests
    {
        private readonly ITestOutputHelper output;

        public PartDiscoveryTests(ITestOutputHelper output)
        {
            this.output = output;
        }

        [Fact]
        public async Task CreatePartsAsync_TypeArray_ResilientAgainstReflectionErrors()
        {
            var discovery = new SketchyPartDiscovery();
            var parts = await discovery.CreatePartsAsync(typeof(string), typeof(int));
            Assert.Equal(1, parts.DiscoveryErrors.Count);
            Assert.Equal(1, parts.Parts.Count);
        }

        [Fact]
        public async Task CreatePartsAsync_Assembly_ResilientAgainstReflectionErrors()
        {
            var discovery = new SketchyPartDiscovery();
            var parts = await discovery.CreatePartsAsync(this.GetType().GetTypeInfo().Assembly);
            Assert.Equal(1, parts.DiscoveryErrors.Count);
            Assert.Equal(0, parts.Parts.Count);
        }

        [Fact]
        public async Task Combined_CreatePartsAsync_TypeArray_ResilientAgainstReflectionErrors()
        {
            var discovery = PartDiscovery.Combine(new SketchyPartDiscovery(), new NoOpDiscovery());
            var parts = await discovery.CreatePartsAsync(typeof(string), typeof(int));
            Assert.Equal(1, parts.DiscoveryErrors.Count);
            Assert.Equal(1, parts.Parts.Count);
        }

#if DESKTOP
        [Fact]
        public async Task Combined_CreatePartsAsync_AssemblyPathEnumerable()
        {
            var discovery = PartDiscovery.Combine(TestUtilities.V2Discovery, TestUtilities.V1Discovery);
            var assemblies = new[]
            {
                typeof(AssemblyDiscoveryTests.DiscoverablePart1).GetTypeInfo().Assembly,
                this.GetType().GetTypeInfo().Assembly,
            };
            var parts = await discovery.CreatePartsAsync(assemblies.Select(a => a.Location));
            Assert.NotEqual(0, parts.Parts.Count);
        }

        [Fact]
        public async Task Combined_IncrementalProgressUpdates()
        {
            var discovery = PartDiscovery.Combine(TestUtilities.V2Discovery, TestUtilities.V1Discovery);
            var assemblies = new[]
            {
                typeof(AssemblyDiscoveryTests.DiscoverablePart1).GetTypeInfo().Assembly,
                this.GetType().GetTypeInfo().Assembly,
            };
            DiscoveryProgress lastReceivedUpdate = default(DiscoveryProgress);
            int progressUpdateCount = 0;
            var progress = new SynchronousProgress<DiscoveryProgress>(update =>
            {
                progressUpdateCount++;
                Assert.NotNull(update.Status);
                ////Assert.True(update.Completion >= lastReceivedUpdate.Completion); // work can be discovered that regresses this legitimately
                Assert.True(update.Completion <= 1);
                Assert.True(update.Status != lastReceivedUpdate.Status || update.Completion != lastReceivedUpdate.Completion);
                this.output.WriteLine(
                    "Completion reported: {0} ({1}/{2}): {3}",
                    update.Completion,
                    update.CompletedSteps,
                    update.TotalSteps,
                    update.Status);
                lastReceivedUpdate = update;
            });
            await discovery.CreatePartsAsync(assemblies, progress);
            progress.RethrowAnyExceptions();
            Assert.True(lastReceivedUpdate.Completion > 0);
            Assert.True(progressUpdateCount > 2);
        }
#endif

        [Fact]
        public async Task CatalogAssemblyLoadFailure()
        {
            var discovery = TestUtilities.V2Discovery;
            var result = await discovery.CreatePartsAsync(new[] { "Foo\\DoesNotExist.dll" });
            Assert.Equal(1, result.DiscoveryErrors.Count);
            Assert.Equal("Foo\\DoesNotExist.dll", result.DiscoveryErrors[0].AssemblyPath);
        }

        private class SynchronousProgress<T> : IProgress<T>
        {
            private readonly Action<T> callback;
            private readonly List<Exception> exceptions = new List<Exception>();

            internal SynchronousProgress(Action<T> callback)
            {
                Requires.NotNull(callback, nameof(callback));
                this.callback = callback;
            }

            public void Report(T value)
            {
                try
                {
                    this.callback(value);
                }
                catch (Exception ex)
                {
                    this.exceptions.Add(ex);
                }
            }

            public void RethrowAnyExceptions()
            {
                if (this.exceptions.Count > 0)
                {
                    throw new AggregateException(this.exceptions);
                }
            }
        }

        private class SketchyPartDiscovery : PartDiscovery
        {
            internal SketchyPartDiscovery()
                : base(TestUtilities.Resolver)
            {
            }

            protected override ComposablePartDefinition CreatePart(Type partType, bool typeExplicitlyRequested)
            {
                if (partType == typeof(string))
                {
                    throw new ArgumentException();
                }

                return new ComposablePartDefinition(
                    TypeRef.Get(typeof(int), TestUtilities.Resolver),
                    ImmutableDictionary<string, object>.Empty,
                    ImmutableList.Create<ExportDefinition>(),
                    ImmutableDictionary.Create<MemberRef, IReadOnlyCollection<ExportDefinition>>(),
                    ImmutableList.Create<ImportDefinitionBinding>(),
                    null,
                    default(MethodRef),
                    default(ConstructorRef),
                    ImmutableList.Create<ImportDefinitionBinding>(),
                    CreationPolicy.Any,
                    true);
            }

            public override bool IsExportFactoryType(Type type)
            {
                return false;
            }

            protected override IEnumerable<Type> GetTypes(System.Reflection.Assembly assembly)
            {
                throw new ArgumentException();
            }
        }

        private class NoOpDiscovery : PartDiscovery
        {
            internal NoOpDiscovery()
                : base(TestUtilities.Resolver)
            {
            }

            protected override ComposablePartDefinition CreatePart(Type partType, bool typeExplicitlyRequested)
            {
                return null;
            }

            public override bool IsExportFactoryType(Type type)
            {
                return false;
            }

            protected override IEnumerable<Type> GetTypes(Assembly assembly)
            {
                return Enumerable.Empty<Type>();
            }
        }
    }
}
