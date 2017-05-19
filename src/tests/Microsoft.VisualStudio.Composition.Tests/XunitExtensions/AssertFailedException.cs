namespace Microsoft.VisualStudio.Composition.Tests
{
    using System;
    using System.Runtime.Serialization;

    [Serializable]
    internal class AssertFailedException : Exception
    {
        public AssertFailedException()
        {
        }

        public AssertFailedException(string message)
            : base(message)
        {
        }

        public AssertFailedException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

        protected AssertFailedException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }
    }
}