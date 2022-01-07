// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace VS.Mefx.Commands
{
    using System.Collections.Generic;
    using Microsoft.VisualStudio.Composition;

    /// <summary>
    /// Node object to represent parts when performing rejection tracing.
    /// </summary>
    internal class PartNode
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="PartNode"/> class.
        /// </summary>
        /// <param name="definition">The definition of the part associated with the node.</param>
        /// <param name="message">Initialize rejection message in error stack.</param>
        /// <param name="currLevel">The depth of the part in the stack.</param>
        internal PartNode(ComposablePartDefinition definition, string message, int currLevel)
        {
            this.Part = definition;
            this.IsWhiteListed = false;
            this.VerboseMessages.Add(message);
            this.Level = currLevel;
            this.IsWhiteListed = false;

            this.ExportingContracts = new List<string>();
            foreach (var export in this.Part.ExportDefinitions)
            {
                this.ExportingContracts.Add(export.Value.ContractName);
            }
        }

        /// <summary>
        /// Gets the definition associated with the current part.
        /// </summary>
        internal ComposablePartDefinition Part { get; private set; }

        /// <summary>
        /// Gets the verbose rejection messages associated with the current part.
        /// </summary>
        internal HashSet<string> VerboseMessages { get; } = new HashSet<string>();

        /// <summary>
        /// Gets the "children" of the current node, which represents parts that the current
        /// node imports that have import issues themselves.
        /// </summary>
        internal HashSet<PartEdge> ImportRejects { get; } = new HashSet<PartEdge>();

        /// <summary>
        /// Gets the "parent" of the current node, which represents parts that the current
        /// node caused import issues in due to its failure.
        /// </summary>
        internal HashSet<PartEdge> RejectsCaused { get; } = new HashSet<PartEdge>();

        /// <summary>
        /// Gets the level of the current node, which serves as an indicator
        /// of its depth in the rejection stack.
        /// </summary>
        internal int Level { get; private set; }

        /// <summary>
        /// Gets a value indicating whether the current node has been whitelisted by the user.
        /// </summary>
        internal bool IsWhiteListed { get; private set; }

        /// <summary>
        /// Gets the name of contracts exported by the part other than itself.
        /// </summary>
        internal List<string> ExportingContracts { get; private set; }

        /// <summary>
        /// Gets the name of the associated part.
        /// </summary>
        internal string Name => this.Part.Type.FullName!;

        /// <summary>
        /// Gets a value indicating whether gets a value indication whether the given node is a leaf node.
        /// </summary>
        internal bool IsLeafNode => this.ImportRejects.Count == 0;

        /// <summary>
        /// Method to add a node that caused a rejection error in the current node.
        /// </summary>
        /// <param name="node">The node that is the cause of the error.</param>
        /// <param name="description">Label to use when visualizing the edge.</param>
        internal void AddChild(PartNode node, string description = "")
            => this.ImportRejects.Add(new PartEdge(node, description));

        /// <summary>
        /// Method to add a node that the current node caused a rejection error in.
        /// </summary>
        /// <param name="node">The node that the current node caused the error in.</param>
        /// <param name="description">Label to use when visualizing the edge.</param>
        internal void AddParent(PartNode node, string description = "")
            => this.RejectsCaused.Add(new PartEdge(node, description));

        /// <summary>
        /// Method to update the whitelisted propert of the current node.
        /// </summary>
        /// <param name="value">New value of the whitelist property.</param>
        internal void SetWhiteListed(bool value) => this.IsWhiteListed = value;

        /// <summary>
        /// Method to add error message to display as output.
        /// </summary>
        /// <param name="message">The text associated with the error.</param>
        internal void AddErrorMessage(string message) => this.VerboseMessages.Add(message);

        /// <summary>
        /// Gets a value indicating whether the part exports any fields.
        /// </summary>
        internal bool HasExports => this.ExportingContracts.Count > 0;
    }
}
