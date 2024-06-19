// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.Composition
{
    using System.IO;

    internal interface IDescriptiveToString
    {
        void ToString(TextWriter writer);
    }
}
