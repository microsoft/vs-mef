// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.VisualStudio.Composition
{
    using System;

    public class PartDiscoveryException : Exception
    {
        public PartDiscoveryException()
        {
        }

        public PartDiscoveryException(string message)
            : base(message)
        {
        }

        public PartDiscoveryException(string message, Exception inner)
            : base(message, inner)
        {
        }

        public string? AssemblyPath { get; set; }

        public Type? ScannedType { get; set; }
    }
}
