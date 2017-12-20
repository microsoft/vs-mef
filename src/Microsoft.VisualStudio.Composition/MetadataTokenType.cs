// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.VisualStudio.Composition
{
    /// <summary>
    /// The bits set on metadata tokens based on type of token.
    /// </summary>
    /// <remarks>
    /// This can be used in the future to remove these MSBs when serializing the metadata tokens
    /// in order to make them more compressible by virtue of their being significantly smaller
    /// numbers after removing the leading byte.
    /// These come from: http://msdn.microsoft.com/en-us/library/ms231937(v=vs.110).aspx
    /// </remarks>
    internal enum MetadataTokenType : uint
    {
        Type = 0x02000000,
        Field = 0x04000000,
        Method = 0x06000000,
        Parameter = 0x08000000,
        Property = 0x17000000,
        GenericParam = 0x2a000000,
        Mask = 0xff000000,
    }
}
