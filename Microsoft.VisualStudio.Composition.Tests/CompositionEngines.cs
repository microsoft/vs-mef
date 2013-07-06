namespace Microsoft.VisualStudio.Composition.Tests
{
    using System;

    [Flags]
    public enum CompositionEngines
    {
        Unspecified = 0,

        /// <summary>
        /// System.ComponentModel.Composition (the original MEF that's built into the .NET 4.0 and above).
        /// </summary>
        V1 = 0x1,

        /// <summary>
        /// System.Composition (The NuGet MEF).
        /// </summary>
        V2 = 0x2,

        /// <summary>
        /// Microsoft.VisualStudio.Composition, with the catalog created by reading System.Composition MEF attributes.
        /// </summary>
        V3EmulatingV2 = 0x4,

        /// <summary>
        /// Microsoft.VisualStudio.Composition, with the catalog created by reading System.ComponentModel.Composition MEF attributes.
        /// </summary>
        V3EmulatingV1 = 0x8,
        
        /// <summary>
        /// The test is run both against System.ComponentModel.Composition and Microsoft.VisualStudio.Composition,
        /// assuming MEF parts are decorated with attributes from System.ComponentModel.Composition.
        /// </summary>
        V1Compat = V1 | V3EmulatingV1,

        /// <summary>
        /// The test is run both against System.Composition and Microsoft.VisualStudio.Composition,
        /// assuming MEF parts are decorated with attributes from System.Composition.
        /// </summary>
        V2Compat = V2 | V3EmulatingV2,
    }
}
