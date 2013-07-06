namespace Microsoft.VisualStudio.Composition.Tests
{
    using System;

    [Flags]
    public enum CompositionEngines
    {
        Unspecified = 0,
        V1 = 0x1,
        V2 = 0x2,
        Both = V1 | V2,
    }
}
