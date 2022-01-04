// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

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
            Assert.Single(parts.DiscoveryErrors);
            Assert.Single(parts.Parts);
        }

        [Fact]
        public async Task CreatePartsAsync_Assembly_ResilientAgainstReflectionErrors()
        {
            var discovery = new SketchyPartDiscovery();
            var parts = await discovery.CreatePartsAsync(this.GetType().GetTypeInfo().Assembly);
            Assert.Single(parts.DiscoveryErrors);
            Assert.Empty(parts.Parts);
        }

        [Fact]
        public async Task Combined_CreatePartsAsync_TypeArray_ResilientAgainstReflectionErrors()
        {
            var discovery = PartDiscovery.Combine(new SketchyPartDiscovery(), new NoOpDiscovery());
            var parts = await discovery.CreatePartsAsync(typeof(string), typeof(int));
            Assert.Single(parts.DiscoveryErrors);
            Assert.Single(parts.Parts);
        }

        [Fact]
        public async Task AssembliesLoadedViaIAssemblyLoader()
        {
            LoggingAssemblyLoader assemblyLoader = new();
            NoOpDiscovery discovery = new(new Resolver(assemblyLoader));
            string mockAssemblyPath = typeof(PartDiscoveryTests).Assembly.Location;
            await discovery.CreatePartsAsync(new string[] { mockAssemblyPath });
            Assert.Equal(new[] { mockAssemblyPath }, assemblyLoader.AttemptedAssemblyPaths);
        }

        [Fact]
        public async Task DiscoveriesCombinedWithResolver()
        {
            LoggingAssemblyLoader assemblyLoader = new();
            Resolver assemblyResolver = new(assemblyLoader);
            PartDiscovery combined = PartDiscovery.Combine(assemblyResolver, TestUtilities.V2Discovery, TestUtilities.V1Discovery);
            Assert.Same(assemblyResolver, combined.Resolver);
            string testAssemblyPath = typeof(PartDiscoveryTests).Assembly.Location;
            await combined.CreatePartsAsync(new string[] { testAssemblyPath });
            Assert.Equal(new[] { testAssemblyPath }, assemblyLoader.AttemptedAssemblyPaths);
        }

        [Fact]
        public void Combine_JustOneButWithDifferentResolver()
        {
            LoggingAssemblyLoader assemblyLoader = new();
            Resolver assemblyResolver = new(assemblyLoader);
            PartDiscovery combined = PartDiscovery.Combine(assemblyResolver, TestUtilities.V1Discovery);
            Assert.NotSame(TestUtilities.V1Discovery, combined);
            Assert.Same(assemblyResolver, combined.Resolver);
        }

        [Fact]
        public void Combine_JustOneButWithSameResolver()
        {
            PartDiscovery combined = PartDiscovery.Combine(TestUtilities.V1Discovery.Resolver, TestUtilities.V1Discovery);
            Assert.Same(TestUtilities.V1Discovery, combined);
        }

        [Fact]
        public void Combine_NullArgs()
        {
            Assert.Throws<ArgumentNullException>("resolver", () => PartDiscovery.Combine(resolver: null!, TestUtilities.V1Discovery));
            Assert.Throws<ArgumentNullException>("discoveryMechanisms", () => PartDiscovery.Combine(resolver: Resolver.DefaultInstance, null!));
            Assert.Throws<ArgumentException>("discoveryMechanisms", () => PartDiscovery.Combine(resolver: Resolver.DefaultInstance, new PartDiscovery[] { null! }));
            Assert.Throws<ArgumentException>("discoveryMechanisms", () => PartDiscovery.Combine(resolver: Resolver.DefaultInstance, TestUtilities.V1Discovery, null!));
        }

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
            Assert.NotEmpty(parts.Parts);
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

        [Fact]
        public async Task CatalogAssemblyLoadFailure()
        {
            var discovery = TestUtilities.V2Discovery;
            var result = await discovery.CreatePartsAsync(new[] { "Foo\\DoesNotExist.dll" });
            Assert.Single(result.DiscoveryErrors);
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

        private class LoggingAssemblyLoader : IAssemblyLoader
        {
            internal List<string?> AttemptedAssemblyPaths { get; } = new();

            public Assembly LoadAssembly(string assemblyFullName, string? codeBasePath)
            {
                lock (this.AttemptedAssemblyPaths)
                {
                    this.AttemptedAssemblyPaths.Add(codeBasePath);
                }

                throw new NotImplementedException();
            }

            public Assembly LoadAssembly(AssemblyName assemblyName)
            {
                lock (this.AttemptedAssemblyPaths)
                {
                    this.AttemptedAssemblyPaths.Add(assemblyName.CodeBase);
                }

                throw new NotImplementedException();
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
                    ImmutableDictionary<string, object?>.Empty,
                    ImmutableList.Create<ExportDefinition>(),
                    ImmutableDictionary.Create<MemberRef, IReadOnlyCollection<ExportDefinition>>(),
                    ImmutableList.Create<ImportDefinitionBinding>(),
                    null,
                    default(MethodRef),
                    default(MethodRef),
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
                : this(TestUtilities.Resolver)
            {
            }

            internal NoOpDiscovery(Resolver resolver)
                : base(resolver)
            {
            }

            protected override ComposablePartDefinition? CreatePart(Type partType, bool typeExplicitlyRequested)
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
