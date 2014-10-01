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

    public class ThreadSafetyTests
    {
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

        [MefFact(CompositionEngines.V1Compat | CompositionEngines.V2Compat, typeof(P1), typeof(P2))]
        public void SharedPartNotExposedBeforeImportsAreTransitivelySatisfied(IContainer container)
        {
            P1.UnblockPart2Setter.Reset();
            P1.Part2SetterInvoked.Reset();
            var t = Task.Run(delegate
            {
                Task t2;
                try
                {
                    P1.Part2SetterInvoked.WaitOne();
                    t2 = Task.Run(delegate
                    {
                        var p2 = container.GetExportedValue<P2>();
                        Console.WriteLine("GetExportedValue<P2> returned");
                        var p2ViaCycle = p2.Part1.Part2;
                        Assert.Same(p2, p2ViaCycle); // if this fails, then MEF exposed a part that imports parts that are not yet initialized.
                    });

                    // We expect this Wait to timeout because if MEF is doing the right thing,
                    // it would block t2 from finishing until we allow P1 to finish initializing.
                    // But that can't happen unless we give up waiting and we don't want to
                    // deadlock when the right thing happens.
                    Assert.False(t2.Wait(1000));
                    Console.WriteLine("t2.Wait(int) timed out.");
                }
                catch (AggregateException)
                {
                    Console.WriteLine("t2.Wait(int) threw an exception instead of timing out.");
                    throw;
                }
                finally
                {
                    Console.WriteLine("Unblocking completion of P1.set_Part2.");
                    P1.UnblockPart2Setter.Set();
                }

                Console.WriteLine("Getting t2 result.");
                t2.GetAwaiter().GetResult(); // this not only propagates exceptions, but waits for completion in case of a timeout earlier.
            });

            var p1 = container.GetExportedValue<P1>();
            Assert.Same(p1, p1.Part2.Part1);

            t.GetAwaiter().GetResult();
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

        [Export, Shared]
        [MefV1.Export]
        public class P1
        {
            internal static readonly ManualResetEventSlim UnblockPart2Setter = new ManualResetEventSlim();
            internal static readonly AutoResetEvent Part2SetterInvoked = new AutoResetEvent(false);
            private P2 part2;

            [Import, MefV1.Import]
            public P2 Part2
            {
                get { return this.part2; }
                set
                {
                    Part2SetterInvoked.Set();
                    UnblockPart2Setter.Wait();
                    this.part2 = value;
                }
            }
        }

        [Export, Shared]
        [MefV1.Export]
        public class P2
        {
            [Import, MefV1.Import]
            public P1 Part1 { get; set; }
        }
    }
}
