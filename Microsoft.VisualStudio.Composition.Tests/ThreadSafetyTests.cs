namespace Microsoft.VisualStudio.Composition.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Composition;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Xunit;
    using MefV1 = System.ComponentModel.Composition;

    [Trait("Multithreaded", "")]
    public class ThreadSafetyTests
    {
        #region PartRequestedAcrossMultipleThreads

        /// <summary>
        /// Exercises code that relies on provisionalSharedObjects
        /// to break circular dependencies in a way that tries to force
        /// thread safety issues to show themselves.
        /// </summary>
        [MefFact(CompositionEngines.V1Compat | CompositionEngines.V2Compat, typeof(PartThatImportsSharedPartWithBlockableConstructor), typeof(SharedPartWithBlockableConstructor))]
        public void PartRequestedAcrossMultipleThreads(IContainer container)
        {
            var testFailedCancellationSource = new CancellationTokenSource();
            var timeoutCancellationSource = CancellationTokenSource.CreateLinkedTokenSource(testFailedCancellationSource.Token);
            timeoutCancellationSource.CancelAfter(TestUtilities.ExpectedTimeout);

            const int threads = 2;
            SharedPartWithBlockableConstructor.ImportingConstructorBlockEvent.Reset();
            SharedPartWithBlockableConstructor.ConstructorEnteredCountdown.Reset(threads);
            SharedPartWithBlockableConstructor.CancellationToken = testFailedCancellationSource.Token;

            Task<SharedPartWithBlockableConstructor>[] contrivedPartTasks = new Task<SharedPartWithBlockableConstructor>[threads];
            for (int i = 0; i < threads; i++)
            {
                contrivedPartTasks[i] = Task.Run(delegate
                {
                    PartThatImportsSharedPartWithBlockableConstructor part = container.GetExportedValue<PartThatImportsSharedPartWithBlockableConstructor>();
                    SharedPartWithBlockableConstructor getExtension = part.ImportingProperty.Value;
                    return getExtension;
                });
                contrivedPartTasks[i].ContinueWith(t => testFailedCancellationSource.Cancel(), CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted, TaskScheduler.Default);
            }

            // Wait for all threads to have reached SomeOtherPart's constructor.
            // Then unblock them all to complete.
            try
            {
                SharedPartWithBlockableConstructor.ConstructorEnteredCountdown.Wait(timeoutCancellationSource.Token);
                SharedPartWithBlockableConstructor.ImportingConstructorBlockEvent.Set();
            }
            catch (OperationCanceledException)
            {
                // Rethrow any exceptions that caused this to be canceled.
                var exceptions = new AggregateException(contrivedPartTasks.Where(t => t.IsFaulted).Select(t => t.Exception));
                if (exceptions.InnerExceptions.Count > 0)
                {
                    testFailedCancellationSource.Cancel();
                    throw exceptions;
                }

                // A timeout is acceptable. It suggests the container
                // is threadsafe in a manner that does not allow a shared part's constructor
                // to be invoked multiple times.
                // Make sure it was in fact only invoked once.
                Assert.Equal(threads - 1, SharedPartWithBlockableConstructor.ConstructorEnteredCountdown.CurrentCount);

                // Signal to unblock the one constructor invocation that we have.
                SharedPartWithBlockableConstructor.ImportingConstructorBlockEvent.Set();
            }

            // Verify that although the constructor was started multiple times,
            // we still ended up with just one shared part satisfying all the imports.
            for (int i = 1; i < threads; i++)
            {
                Assert.Same(contrivedPartTasks[0].Result, contrivedPartTasks[1].Result);
            }
        }

        [Export]
        [MefV1.Export, MefV1.PartCreationPolicy(MefV1.CreationPolicy.NonShared)]
        public class PartThatImportsSharedPartWithBlockableConstructor
        {
            [Import, MefV1.Import]
            public Lazy<SharedPartWithBlockableConstructor> ImportingProperty { get; set; }
        }

        [Export, Shared]
        [MefV1.Export]
        public class SharedPartWithBlockableConstructor
        {
            internal static readonly ManualResetEventSlim ImportingConstructorBlockEvent = new ManualResetEventSlim();
            internal static readonly CountdownEvent ConstructorEnteredCountdown = new CountdownEvent(0);
            internal static CancellationToken CancellationToken;

            public SharedPartWithBlockableConstructor()
            {
                ConstructorEnteredCountdown.Signal();
                ImportingConstructorBlockEvent.Wait(CancellationToken);
            }
        }

        #endregion

        #region SharedPartNotExposedBeforeImportsAreTransitivelySatisfied Test

        [MefFact(CompositionEngines.V3EmulatingV1 | CompositionEngines.V2Compat, typeof(PartWithBlockingImportPropertySetter), typeof(PartThatImportsPartWithBlockingImportPropertySetter))]
        public void SharedPartNotExposedBeforeImportsAreTransitivelySatisfied(IContainer container)
        {
            PartWithBlockingImportPropertySetter.UnblockSetter.Reset();
            PartWithBlockingImportPropertySetter.SetterInvoked.Reset();
            var t1 = Task.Run(delegate
            {
                Task t2;
                try
                {
                    PartWithBlockingImportPropertySetter.SetterInvoked.WaitOne();
                    t2 = Task.Run(delegate
                    {
                        var leafPart = container.GetExportedValue<PartThatImportsPartWithBlockingImportPropertySetter>();
                        Console.WriteLine("GetExportedValue<PartThatImportsPartWithBlockingImportPropertySetter> has returned.");
                        var leafPartViaCycle = leafPart.PartWithBlockingImport.OtherPartThatImportsThis;
                        Assert.Same(leafPart, leafPartViaCycle); // if this fails, then MEF exposed a part that imports parts that are not yet initialized.
                    });

                    // We expect this Wait to timeout because if MEF is doing the right thing,
                    // it would block t2 from finishing until we allow PartWithBlockingImportPropertySetter to finish initializing.
                    // But that can't happen unless we give up waiting and we don't want to
                    // deadlock when the right thing happens.
                    Assert.False(t2.Wait(TestUtilities.ExpectedTimeout));
                    Console.WriteLine("t2.Wait(int) timed out.");
                }
                catch (AggregateException)
                {
                    Console.WriteLine("t2.Wait(int) threw an exception instead of timing out.");
                    throw;
                }
                finally
                {
                    Console.WriteLine("Unblocking completion of PartWithBlockingImportPropertySetter.set_OtherPartThatImportsThis.");
                    PartWithBlockingImportPropertySetter.UnblockSetter.Set();
                }

                Console.WriteLine("Getting t2 result.");
                t2.GetAwaiter().GetResult(); // this not only propagates exceptions, but waits for completion in case of a timeout earlier.
            });

            var rootPart = container.GetExportedValue<PartWithBlockingImportPropertySetter>();
            Assert.Same(rootPart, rootPart.OtherPartThatImportsThis.PartWithBlockingImport);

            t1.GetAwaiter().GetResult();
        }

        [Export, Shared]
        [MefV1.Export]
        public class PartWithBlockingImportPropertySetter
        {
            internal static readonly ManualResetEventSlim UnblockSetter = new ManualResetEventSlim();
            internal static readonly AutoResetEvent SetterInvoked = new AutoResetEvent(false);
            private PartThatImportsPartWithBlockingImportPropertySetter otherPartThatImportsThis;

            [Import, MefV1.Import]
            public PartThatImportsPartWithBlockingImportPropertySetter OtherPartThatImportsThis
            {
                get
                {
                    return this.otherPartThatImportsThis;
                }

                set
                {
                    SetterInvoked.Set();
                    UnblockSetter.Wait();
                    this.otherPartThatImportsThis = value;
                }
            }
        }

        [Export, Shared]
        [MefV1.Export]
        public class PartThatImportsPartWithBlockingImportPropertySetter
        {
            [Import, MefV1.Import]
            public PartWithBlockingImportPropertySetter PartWithBlockingImport { get; set; }
        }

        #endregion

        // TODO: write test to verify that visibility is not granted to types till their OnImportsSatisfied methods are finished.
        // Artificially control when OnImportsSatisfied is finished and try to prove that other parts are unveiled before they're done.

        #region SharedPartCircularDependencyCreationAcrossMultipleThreads test

        [MefFact(CompositionEngines.V3EmulatingV1 | CompositionEngines.V2Compat, typeof(CircularDependencySharedPart1), typeof(CircularDependencySharedPart2))]
        public void SharedPartCircularDependencyCreationAcrossMultipleThreads(IContainer container)
        {
            CircularDependencySharedPart1.UnblockSurroundingImportingProperties.Reset();
            CircularDependencySharedPart1.SurroundingImportingPropertiesHit.Reset();
            CircularDependencySharedPart2.UnblockSurroundingImportingProperties.Reset();
            CircularDependencySharedPart2.SurroundingImportingPropertiesHit.Reset();

            var part1Task = Task.Run(delegate
            {
                Console.WriteLine("Part1 being requested on thread {0}", Thread.CurrentThread.ManagedThreadId);
                var part1 = container.GetExportedValue<CircularDependencySharedPart1>();
                Assert.NotNull(part1.OnImportsSatisfiedInvokedThread);
                return part1;
            });

            var part2Task = Task.Run(delegate
            {
                Console.WriteLine("Part2 being requested on thread {0}", Thread.CurrentThread.ManagedThreadId);
                var part2 = container.GetExportedValue<CircularDependencySharedPart2>();
                Assert.NotNull(part2.OnImportsSatisfiedInvokedThread);
                return part2;
            });

            // Wait for each thread to construct the first part to the point of initializing imports.
            if (!CircularDependencySharedPart1.SurroundingImportingPropertiesHit.Wait(500))
            {
                Console.WriteLine("Timed out waiting for Part1 to hit the importing property setter.");
            }

            if (!CircularDependencySharedPart2.SurroundingImportingPropertiesHit.Wait(500))
            {
                Console.WriteLine("Timed out waiting for Part2 to hit the importing property setter.");
            }

            // Unleash the satisfying of imports now, which allows the MEF container to discover the need for
            // another part and have to interact with the other thread as necessary.
            CircularDependencySharedPart1.UnblockSurroundingImportingProperties.Set();
            CircularDependencySharedPart2.UnblockSurroundingImportingProperties.Set();

            // Get the two parts.
            var p1 = part1Task.Result;
            var p2 = part2Task.Result;

            Assert.Same(p1, p2.Part1);
            Assert.Same(p2, p1.Part2);

            // Verify (for the validity of the test itself) that the parts were initialized from different threads.
            Console.WriteLine(
                "Each part initialized from unique threads: {0}",
                CircularDependencySharedPart1.ThreadSatisfiedFrom != CircularDependencySharedPart2.ThreadSatisfiedFrom);
        }

        [Export, Shared]
        [MefV1.Export]
        public class CircularDependencySharedPart1 : MefV1.IPartImportsSatisfiedNotification
        {
            internal static readonly ManualResetEventSlim SurroundingImportingPropertiesHit = new ManualResetEventSlim();
            internal static readonly ManualResetEventSlim UnblockSurroundingImportingProperties = new ManualResetEventSlim();
            internal static Thread ThreadSatisfiedFrom;

            [ImportMany, MefV1.ImportMany]
            public object[] DummyImporter
            {
                get
                {
                    return null;
                }

                set
                {
                    ThreadSatisfiedFrom = Thread.CurrentThread;
                    SurroundingImportingPropertiesHit.Set();
                    UnblockSurroundingImportingProperties.Wait();
                }
            }

            [Import, MefV1.Import]
            public CircularDependencySharedPart2 Part2 { get; set; }

            public Thread OnImportsSatisfiedInvokedThread { get; private set; }

            [OnImportsSatisfied]
            public void OnImportsSatisfied()
            {
                this.OnImportsSatisfiedInvokedThread = Thread.CurrentThread;
                Assert.Same(this, this.Part2.Part1);

                // This isn't a requirement, but log whether the OnImportsSatisfied method
                // was invoked on this thread because it's interesting.
                Console.WriteLine("Part1 OnImportsSatisfied invoked on thread {0}", this.OnImportsSatisfiedInvokedThread.ManagedThreadId);
            }
        }

        [Export, Shared]
        [MefV1.Export]
        public class CircularDependencySharedPart2 : MefV1.IPartImportsSatisfiedNotification
        {
            internal static readonly ManualResetEventSlim SurroundingImportingPropertiesHit = new ManualResetEventSlim();
            internal static readonly ManualResetEventSlim UnblockSurroundingImportingProperties = new ManualResetEventSlim();
            internal static Thread ThreadSatisfiedFrom;

            [ImportMany, MefV1.ImportMany]
            public object[] DummyImporter
            {
                get
                {
                    return null;
                }

                set
                {
                    ThreadSatisfiedFrom = Thread.CurrentThread;
                    SurroundingImportingPropertiesHit.Set();
                    UnblockSurroundingImportingProperties.Wait();
                }
            }

            [Import, MefV1.Import]
            public CircularDependencySharedPart1 Part1 { get; set; }

            public Thread OnImportsSatisfiedInvokedThread { get; private set; }

            [OnImportsSatisfied]
            public void OnImportsSatisfied()
            {
                this.OnImportsSatisfiedInvokedThread = Thread.CurrentThread;
                Assert.Same(this, this.Part1.Part2);

                // This isn't a requirement, but log whether the OnImportsSatisfied method
                // was invoked on this thread because it's interesting.
                Console.WriteLine("Part2 OnImportsSatisfied invoked on thread {0}", this.OnImportsSatisfiedInvokedThread.ManagedThreadId);
            }
        }

        #endregion

        #region ImportingConstructorImportsAreFullyInitialized test

        [MefFact(CompositionEngines.V1Compat | CompositionEngines.V2Compat, typeof(PartThatImportsPartWithOwnImports), typeof(PartThatImportsRandomExport), typeof(RandomExport))]
        public void ImportingConstructorImportsAreFullyInitialized(IContainer container)
        {
            PartThatImportsRandomExport.UnblockImportingPropertySetters.Reset();
            PartThatImportsRandomExport.ImportingPropertySettersInvoked.Reset();

            var partDependencyTask = Task.Run(delegate
            {
                var part = container.GetExportedValue<PartThatImportsRandomExport>();
                Assert.NotNull(part.RandomExport);
                return part;
            });

            PartThatImportsRandomExport.ImportingPropertySettersInvoked.Wait();

            var partWithImportingConstructorTask = Task.Run(delegate
            {
                var part = container.GetExportedValue<PartThatImportsPartWithOwnImports>();
                return part;
            });

            Assert.False(partWithImportingConstructorTask.Wait(TestUtilities.ExpectedTimeout)); // we expect this task cannot proceed till we unblock its importing constructor arguments.
            PartThatImportsRandomExport.UnblockImportingPropertySetters.Set();
            Assert.True(partWithImportingConstructorTask.Wait(TestUtilities.UnexpectedTimeout));

            partDependencyTask.Wait(); // rethrow any failures here

            Assert.Same(partDependencyTask.Result, partWithImportingConstructorTask.Result.ImportingConstructorArgument);
        }

        [Export, Shared]
        [MefV1.Export]
        public class PartThatImportsPartWithOwnImports
        {
            [ImportingConstructor, MefV1.ImportingConstructor]
            public PartThatImportsPartWithOwnImports(PartThatImportsRandomExport export)
            {
                Assert.NotNull(export.RandomExport);
                this.ImportingConstructorArgument = export;
            }

            public PartThatImportsRandomExport ImportingConstructorArgument { get; set; }
        }

        [Export, Shared]
        [MefV1.Export]
        public class PartThatImportsRandomExport
        {
            internal static readonly ManualResetEventSlim UnblockImportingPropertySetters = new ManualResetEventSlim();
            internal static readonly ManualResetEventSlim ImportingPropertySettersInvoked = new ManualResetEventSlim();

            private RandomExport randomExport;

            [Import, MefV1.Import]
            public RandomExport RandomExport
            {
                get
                {
                    return this.randomExport;
                }

                set
                {
                    ImportingPropertySettersInvoked.Set();
                    UnblockImportingPropertySetters.Wait();
                    this.randomExport = value;
                }
            }
        }

        #endregion

        [Export, Shared]
        [MefV1.Export]
        public class RandomExport { }
    }
}
