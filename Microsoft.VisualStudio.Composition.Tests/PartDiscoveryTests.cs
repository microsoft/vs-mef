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
    using Validation;
    using Xunit;

    public class PartDiscoveryTests
    {
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
            var parts = await discovery.CreatePartsAsync(this.GetType().Assembly);
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

        [Fact]
        public async Task Combined_CreatePartsAsync_Assembly_ResilientAgainstReflectionErrors()
        {
            var discovery = PartDiscovery.Combine(new SketchyPartDiscovery(), new NoOpDiscovery());
            var parts = await discovery.CreatePartsAsync(this.GetType().Assembly);
            Assert.Equal(1, parts.DiscoveryErrors.Count);
            Assert.Equal(0, parts.Parts.Count);
        }

        [Fact]
        public async Task Combined_CreatePartsAsync_AssemblyEnumerable_ResilientAgainstReflectionErrors()
        {
            var discovery = PartDiscovery.Combine(new SketchyPartDiscovery(), new NoOpDiscovery());
            var parts = await discovery.CreatePartsAsync(new[] { this.GetType().Assembly });
            Assert.Equal(1, parts.DiscoveryErrors.Count);
            Assert.Equal(0, parts.Parts.Count);
        }

        [Fact]
        public async Task Combined_IncrementalProgressUpdates()
        {
            var discovery = PartDiscovery.Combine(new AttributedPartDiscovery(), new AttributedPartDiscoveryV1());
            var assemblies = new[] { 
                typeof(AssemblyDiscoveryTests.DiscoverablePart1).Assembly,
                this.GetType().Assembly,
            };
            PartDiscovery.DiscoveryProgress lastReceivedUpdate = default(PartDiscovery.DiscoveryProgress);
            int progressUpdateCount = 0;
            var progress = new SynchronousProgress<PartDiscovery.DiscoveryProgress>(update =>
            {
                progressUpdateCount++;
                Assert.NotNull(update.Status);
                ////Assert.True(update.Completion >= lastReceivedUpdate.Completion); // work can be discovered that regresses this legitimately
                Assert.True(update.Completion <= 1);
                Assert.True(update.Status != lastReceivedUpdate.Status || update.Completion != lastReceivedUpdate.Completion);
                Console.WriteLine(
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

        private class SynchronousProgress<T> : IProgress<T>
        {
            private readonly Action<T> callback;
            private readonly List<Exception> exceptions = new List<Exception>();

            internal SynchronousProgress(Action<T> callback)
            {
                Requires.NotNull(callback, "callback");
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
            protected override ComposablePartDefinition CreatePart(Type partType, bool typeExplicitlyRequested)
            {
                if (partType == typeof(string))
                {
                    throw new ArgumentException();
                }

                return new ComposablePartDefinition(
                    TypeRef.Get(typeof(int)),
                    ImmutableList.Create<ExportDefinition>(),
                    ImmutableDictionary.Create<MemberRef, IReadOnlyCollection<ExportDefinition>>(),
                    ImmutableList.Create<ImportDefinitionBinding>(),
                    null,
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
