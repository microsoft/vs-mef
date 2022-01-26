// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.Composition.VSMefx
{
    using System;
    using System.Collections.Generic;
    using Microsoft.VisualStudio.Composition.VSMefx.Commands;
    using OpenSoftware.DgmlTools;
    using OpenSoftware.DgmlTools.Builders;
    using OpenSoftware.DgmlTools.Model;

    /// <summary>
    /// Class to create and save a graph assocaited with the rejection errors.
    /// </summary>
    internal class GraphCreator
    {
        private const string WhiteListLabel = "[Whitelisted]";
        private const string NormalNodeLabel = "Error";
        private const string EdgeLabel = "Edge";
        private const string NodeColor = "#00FFFF";
        private const string EdgeThickness = "3";
        private const string ContainerString = "Expanded";
        private const string ContainerLabel = "Contains";
        private const string ContainerStart = "Part: ";

        /// <summary>
        /// Initializes a new instance of the <see cref="GraphCreator"/> class.
        /// </summary>
        /// <param name="graph">A dictionary of the parts to include in the rejection graph.</param>
        internal GraphCreator(Dictionary<string, PartNode> graph)
        {
            this.RejectionGraph = graph;

            // Tell the DGML creator how to create nodes, categorize them and create edges between.
            var nodeCreator = new[]
            {
                new NodeBuilder<PartNode>(this.NodeConverter),
            };
            var edgeCreator = new[]
            {
                new LinksBuilder<PartNode>(this.EdgeGenerator),
            };
            var categoryCreator = new[]
            {
                new CategoryBuilder<PartNode>(x => new Category { Id = x.Level.ToString() }),
            };
            StyleBuilder[] styleCreator =
            {
                new StyleBuilder<Node>(WhiteListedNode),
                new StyleBuilder<Link>(EdgeStyle),
            };
            var builder = new DgmlBuilder
            {
                NodeBuilders = nodeCreator,
                LinkBuilders = edgeCreator,
                CategoryBuilders = categoryCreator,
                StyleBuilders = styleCreator,
            };
            IEnumerable<PartNode> nodes = this.RejectionGraph.Values;
            this.Dgml = builder.Build(nodes);
        }

        private Dictionary<string, PartNode> RejectionGraph { get; set; }

        private DirectedGraph Dgml { get; set; }

        /// <summary>
        /// Method to get the rejection DGML.
        /// </summary>
        /// <returns>A directed graph visualizing the part rejections.</returns>
        internal DirectedGraph GetGraph()
        {
            return this.Dgml;
        }

        /// <summary>
        /// Method to save the generated graph to an output file.
        /// </summary>
        /// <param name="outputFilePath"> The complete path of the file to which we want to save the DGML graph.</param>
        internal void SaveGraph(string outputFilePath)
        {
            this.Dgml.WriteToFile(outputFilePath);
        }

        internal static string GetNodeName(PartNode current)
        {
            if (current.HasExports)
            {
                return ContainerStart + current.Name;
            }
            else
            {
                return current.Name;
            }
        }

        /// <summary>
        /// Returns a Style object that sets the background of whitelisted nodes to white.
        /// </summary>
        /// <param name="node">The node to create the style for.</param>
        /// <returns>The Style object for a whitelisted node.</returns>
        private static Style WhiteListedNode(Node node)
        {
            return new Style
            {
                GroupLabel = WhiteListLabel,
                Setter = new List<Setter>
                {
                    new Setter { Property = "Background", Value = NodeColor },
                },
            };
        }

        /// <summary>
        /// Method to generate the Style properties for the edges.
        /// </summary>
        /// <param name="edge">The edge to generate style info for.</param>
        /// <returns>A style object to use when styling edges.</returns>
        private static Style EdgeStyle(Link edge)
        {
            return new Style
            {
                GroupLabel = EdgeLabel,
                Setter = new List<Setter>
                {
                    new Setter { Property = "StrokeThickness", Value = EdgeThickness },
                },
            };
        }

        /// <summary>
        /// Method to convert from custom Node representation to the DGML node representation.
        /// </summary>
        /// <param name="current">The PartNode object which we want to convert.</param>
        /// <returns> A DGML Node representation of the input PartNode.</returns>
        private Node NodeConverter(PartNode current)
        {
            string nodeName = GetNodeName(current);
            Node convertered = new Node
            {
                Id = nodeName,
                Category = current.IsWhiteListed ? WhiteListLabel : NormalNodeLabel,
                Group = current.HasExports ? ContainerString : null,
            };
            convertered.Properties.Add("Level", current.Level.ToString());
            return convertered;
        }

        /// <summary>
        /// Method to get all the outgoing edges from the current node.
        /// </summary>
        /// <param name="current">The PartNode whose outgoing edges we want to find.</param>
        /// <returns> A list of Links that represent the outgoing edges for the input node.</returns>
        private IEnumerable<Link> EdgeGenerator(PartNode current)
        {
            // Add edges for import/exports between parts
            if (current.RejectsCaused != null)
            {
                foreach (var outgoingEdge in current.RejectsCaused)
                {
                    if (this.ValidEdge(current, outgoingEdge))
                    {
                        string sourceName = GetNodeName(current);
                        string targetName = GetNodeName(outgoingEdge.Target);
                        Link edge = new Link
                        {
                            Source = sourceName,
                            Target = targetName,
                            Label = outgoingEdge.Label,
                            Category = EdgeLabel,
                        };
                        yield return edge;
                    }
                }
            }

            // Create containers for the parts that have exports for the current part
            if (current.HasExports)
            {
                string sourceName = GetNodeName(current);
                foreach (var exportName in current.ExportingContracts)
                {
                    yield return new Link
                    {
                        Source = sourceName,
                        Target = exportName,
                        Category = ContainerLabel,
                    };
                }
            }
        }

        /// <summary>
        /// Method to check if a given potential edge is valid or not.
        /// </summary>
        /// <param name="source">The PartNode that would be the source of the potential edge.</param>
        /// <param name="edge">The PartEdge indicating an outgoing edge from the Source Node.</param>
        /// <returns> A boolean indicating if the specified edge should be included in the graph or not.</returns>
        private bool ValidEdge(PartNode source, PartEdge edge)
        {
            string sourceName = source.Name;
            string targetName = edge.Target.Name;
            return this.RejectionGraph.ContainsKey(sourceName)
                && this.RejectionGraph.ContainsKey(targetName);
        }
    }
}
