// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.Composition.VSMefx.Commands
{
    /// <summary>
    /// A class to represent a simple edge between nodes.
    /// </summary>
    internal class PartEdge
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="PartEdge"/> class.
        /// </summary>
        /// <param name="other">The Node at the tail of the edge.</param>
        /// <param name="description">The label associated with the node.</param>
        internal PartEdge(PartNode other, string description)
        {
            this.Target = other;
            this.Label = description;
        }

        /// <summary>
        /// Gets the node at the tail of the edge.
        /// </summary>
        internal PartNode Target { get; private set; }

        /// <summary>
        /// Gets the label associated with the given node.
        /// </summary>
        internal string Label { get; private set; }
    }
}
