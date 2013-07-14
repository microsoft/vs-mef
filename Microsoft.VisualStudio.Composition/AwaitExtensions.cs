namespace Microsoft.VisualStudio.Composition
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.Build.Execution;

    internal static class AwaitExtensions
    {
        /// <summary>
        /// Begins an asynchronous build via MSBuild.
        /// </summary>
        /// <param name="submission">The submission to begin execution.</param>
        /// <returns>The task that will return build results.</returns>
        internal static Task<BuildResult> ExecuteAsync(this BuildSubmission submission)
        {
            var tcs = new TaskCompletionSource<BuildResult>();
            submission.ExecuteAsync(SetBuildComplete, tcs);
            return tcs.Task;
        }

        /// <summary>
        /// Marks the task created for a BuildSubmission as completed.
        /// </summary>
        /// <param name="submission">The build submission that has completed.</param>
        private static void SetBuildComplete(BuildSubmission submission)
        {
            var tcs = (TaskCompletionSource<BuildResult>)submission.AsyncContext;
            if (submission.BuildResult.Exception is Microsoft.Build.Exceptions.BuildAbortedException)
            {
                tcs.SetCanceled();
            }
            else
            {
                tcs.SetResult(submission.BuildResult);
            }
        }
    }
}
