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
        [MefFact(CompositionEngines.V1Compat | CompositionEngines.V2Compat)]
        public void PartRequestedAcrossMultipleThreads(IContainer container)
        {
            var testFailedCancellationSource = new CancellationTokenSource();
            var timeoutCancellationSource = CancellationTokenSource.CreateLinkedTokenSource(testFailedCancellationSource.Token);
            timeoutCancellationSource.CancelAfter(TestUtilities.ExpectedTimeout);

            const int threads = 2;
            SomeOtherPart.ImportingConstructorBlockEvent.Reset();
            SomeOtherPart.ConstructorEnteredCountdown.Reset(threads);
            SomeOtherPart.CancellationToken = testFailedCancellationSource.Token;

            Task<SomeOtherPart>[] contrivedPartTasks = new Task<SomeOtherPart>[threads];
            for (int i = 0; i < threads; i++)
            {
                contrivedPartTasks[i] = Task.Run(delegate
                {
                    RootPart part = container.GetExportedValue<RootPart>();
                    SomeOtherPart getExtension = part.ImportingProperty.Value;
                    return getExtension;
                });
                contrivedPartTasks[i].ContinueWith(t => testFailedCancellationSource.Cancel(), CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted, TaskScheduler.Default);
            }

            // Wait for all threads to have reached SomeOtherPart's constructor.
            // Then unblock them all to complete.
            try
            {
                SomeOtherPart.ConstructorEnteredCountdown.Wait(timeoutCancellationSource.Token);
                SomeOtherPart.ImportingConstructorBlockEvent.Set();
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
                Assert.Equal(threads - 1, SomeOtherPart.ConstructorEnteredCountdown.CurrentCount);

                // Signal to unblock the one constructor invocation that we have.
                SomeOtherPart.ImportingConstructorBlockEvent.Set();
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
        public class RootPart
        {
            [Import, MefV1.Import]
            public Lazy<SomeOtherPart> ImportingProperty { get; set; }
        }

        [Export, Shared]
        [MefV1.Export]
        public class SomeOtherPart
        {
            internal static readonly ManualResetEventSlim ImportingConstructorBlockEvent = new ManualResetEventSlim();
            internal static readonly CountdownEvent ConstructorEnteredCountdown = new CountdownEvent(0);
            internal static CancellationToken CancellationToken;

            public SomeOtherPart()
            {
                ConstructorEnteredCountdown.Signal();
                ImportingConstructorBlockEvent.Wait(CancellationToken);
            }
        }
    }
}
