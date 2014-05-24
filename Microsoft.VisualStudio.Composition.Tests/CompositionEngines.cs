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
        V3EmulatingV1 = 0x8 | V3NonPublicSupport,

        /// <summary>
        /// Microsoft.VisualStudio.Composition, with the catalog created by reading both
        /// System.ComponentModel.Composition and System.Composition MEF attributes.
        /// </summary>
        V3EmulatingV1AndV2AtOnce = 0x10,

        /// <summary>
        /// Indicates that non-publics will be reflected over.
        /// </summary>
        V3NonPublicSupport = 0x100,

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

        /// <summary>
        /// The <see cref="V3EmulatingV2"/> and <see cref="V3NonPublicSupport"/> flags.
        /// </summary>
        V3EmulatingV2WithNonPublic = V3EmulatingV2 | V3NonPublicSupport,

        /// <summary>
        /// The bit mask for options sent to the V3 engine.
        /// </summary>
        V3OptionsMask = 0xF00,
    }
}
