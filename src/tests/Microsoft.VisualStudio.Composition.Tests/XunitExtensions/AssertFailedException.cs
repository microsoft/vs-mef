namespace Microsoft.VisualStudio.Composition.Tests
{
    using System;
    using System.Runtime.Serialization;

#if DESKTOP
    [Serializable]
#endif
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

#if DESKTOP
        protected AssertFailedException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }
#endif
    }
}