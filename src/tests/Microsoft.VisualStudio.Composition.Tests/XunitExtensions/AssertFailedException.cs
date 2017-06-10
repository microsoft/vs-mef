namespace Microsoft.VisualStudio.Composition.Tests
{
    using System;
    using System.Runtime.Serialization;

    [Serializable]
    internal class AssertFailedException : Exception
    {
        internal AssertFailedException()
        {
        }

        internal AssertFailedException(string message)
            : base(message)
        {
        }

        internal AssertFailedException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

        protected AssertFailedException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }
    }
}