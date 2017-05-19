namespace Microsoft.VisualStudio.Composition.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Runtime.CompilerServices;

    internal static class AssertEx
    {
        /// <summary>
        /// Since the exceptions thrown by xUnit asserts are not serializable we lose information
        /// in case tests fail in another appdomain. These four helpers throw a serializable exception
        /// that results in a more information exception in the error log.
        /// </summary>
        internal static void AssertFalse(bool condition, [CallerFilePath] string filePath = null, [CallerMemberName] string memberName = null, [CallerLineNumber] int lineNumber = 0)
        {
            if (condition)
            {
                var message = $"Assert failed: expected false (actual: true) at {filePath}, {memberName} line {lineNumber}";
                throw new AssertFailedException(message);
            }
        }

        internal static void AssertTrue(bool condition, [CallerFilePath] string filePath = null, [CallerMemberName] string memberName = null, [CallerLineNumber] int lineNumber = 0)
        {
            if (!condition)
            {
                var message = $"Assert failed: expected true (actual: false) at {filePath}, {memberName} line {lineNumber}";
                throw new AssertFailedException(message);
            }
        }

        internal static void AssertNotNull(object reference, [CallerFilePath] string filePath = null, [CallerMemberName] string memberName = null, [CallerLineNumber] int lineNumber = 0)
        {
            if (reference == null)
            {
                var message = $"Assert failed: unexpected null at {filePath}, {memberName} line {lineNumber}";
                throw new AssertFailedException(message);
            }
        }

        internal static void AssertEqual<T>(T expected, T actual, [CallerFilePath] string filePath = null, [CallerMemberName] string memberName = null, [CallerLineNumber] int lineNumber = 0)
            where T : IEquatable<T>
        {
            if (!EqualityComparer<T>.Default.Equals(expected, actual))
            {
                var message = $"Assert failed: expected: {expected} actual: {actual} at {filePath}, {memberName} line {lineNumber}";
                throw new AssertFailedException(message);
            }
        }
    }
}
