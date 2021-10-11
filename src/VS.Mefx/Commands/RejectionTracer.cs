namespace VS.Mefx.Commands
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Text;
    using Microsoft.VisualStudio.Composition;

    /// <summary>
    /// Class to perform rejection tracing on the input parts.
    /// </summary>
    internal class RejectionTracer : Command
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="RejectionTracer"/> class.
        /// </summary>
        /// <param name="derivedInfo">The catalog and config associated with the input files.</param>
        /// <param name="arguments">The arguments specified by the user.</param>
        public RejectionTracer(ConfigCreator derivedInfo, CLIOptions arguments)
            : base(derivedInfo, arguments)
        {
            this.RejectionGraph = new Dictionary<string, PartNode>();
            this.GenerateNodeGraph();
        }

        /// <summary>
        /// Gets or sets all the nodes in the rejectionGraph, which is a graph representation of
        /// the error stack provided by the config.
        /// </summary>
        private Dictionary<string, PartNode> RejectionGraph { get; set; }

        /// <summary>
        /// Gets or sets the number of levels present in the overall graph where the level value
        /// corresponds to the depth of the node/part in the error stack.
        /// </summary>
        private int MaxLevels { get; set; }

        /// <summary>
        /// Method to read the input arguments and perform rejection tracing for the requested parts.
        /// </summary>
        public void PerformRejectionTracing()
        {
            if (this.Options.RejectedDetails.Contains(Strings.AllText, StringComparer.OrdinalIgnoreCase))
            {
                this.ListAllRejections();
            }
            else
            {
                foreach (string rejectPart in this.Options.RejectedDetails)
                {
                    this.ListReject(rejectPart);
                }
            }
        }

        /// <summary>
        /// Method to initialize the part nodes and thier "pointers" based on the error
        /// stack from the config.
        /// </summary>
        private void GenerateNodeGraph()
        {
            // Get the error stack from the composition configuration
            var whiteListChecker = new WhiteList(this.Options);
            CompositionConfiguration config = this.Creator.Config;
            var errors = config.CompositionErrors;
            int levelNumber = 1;
            while (errors.Count() > 0)
            {
                // Process all the parts present in the current level of the stack
                var currentLevel = errors.Peek();
                foreach (var element in currentLevel)
                {
                    var part = element.Parts.First();

                    // Create a PartNode object from the definition of the current Part
                    ComposablePartDefinition definition = part.Definition;
                    string currentName = definition.Type.FullName;
                    if (currentName == null)
                    {
                        continue;
                    }

                    if (this.RejectionGraph.ContainsKey(currentName))
                    {
                        this.RejectionGraph[currentName].AddErrorMessage(element.Message);
                        continue;
                    }

                    PartNode currentNode = new PartNode(definition, element.Message, levelNumber);
                    currentNode.SetWhiteListed(whiteListChecker.IsWhiteListed(currentName));
                    this.RejectionGraph.Add(currentName, currentNode);
                }

                // Get the next level of the stack
                errors = errors.Pop();
                levelNumber += 1;
            }

            this.MaxLevels = levelNumber - 1;
            foreach (var nodePair in this.RejectionGraph)
            {
                var node = nodePair.Value;
                var currentNodeName = node.GetName();
                var nodeDefinition = node.Part;

                // Get the imports for the current part to update the pointers associated with the current node
                foreach (var import in nodeDefinition.Imports)
                {
                    string importName = import.ImportingSiteType.FullName;
                    if (importName == null || !this.RejectionGraph.ContainsKey(importName))
                    {
                        continue;
                    }

                    string importLabel = importName;
                    if (import.ImportingMember != null)
                    {
                        importLabel = import.ImportingMember.Name;
                    }

                    PartNode childNode = this.RejectionGraph[importName];
                    childNode.AddParent(node, importLabel);
                    node.AddChild(childNode, importLabel);
                }
            }
        }

        /// <summary>
        /// Method to indicate all the rejection issues present in a given level.
        /// </summary>
        /// <param name="currentLevel">An integer representing the level we are intrested in.</param>
        private void ListErrorsinLevel(int currentLevel)
        {
            string errorMessage = string.Format(
                        CultureInfo.CurrentCulture,
                        Strings.ErrorsLevelMessage,
                        currentLevel);
            this.Options.Writer.WriteLine(errorMessage);
            List<string> currentErrors = new List<string>();
            foreach (var pair in this.RejectionGraph)
            {
                PartNode currentNode = pair.Value;
                if (currentNode.Level.Equals(currentLevel))
                {
                    currentErrors.Add(this.GetNodeDetail(currentNode));
                }
            }

            this.WriteLines(currentErrors);
            if (!this.Options.Verbose)
            {
                this.Options.Writer.WriteLine();
            }
        }

        /// <summary>
        /// Method to save the DGML graph to the given fileName.
        /// </summary>
        /// <param name="fileName">Name of the dgml file whose path we want to determine.</param>
        /// <param name="graph">The <see cref="GraphCreator"/> to save in the specified file path.</param>
        private void SaveGraph(string fileName, GraphCreator graph)
        {
            string relativePath = this.Options.GraphPath;
            string currentDirectory = Directory.GetCurrentDirectory();
            string outputDirectory = Path.GetFullPath(Path.Combine(currentDirectory, relativePath));
            if (!Directory.Exists(outputDirectory))
            {
                string missingMessage = string.Format(
                    CultureInfo.CurrentCulture,
                    Strings.MissingRelativePath,
                    relativePath);
                this.Options.Writer.WriteLine(missingMessage);
                outputDirectory = currentDirectory;
            }

            string outputPath = Path.GetFullPath(Path.Combine(outputDirectory, fileName));
            graph.SaveGraph(outputPath);
            string savedMessage = string.Format(
                        CultureInfo.CurrentCulture,
                        Strings.SavedGraphMessage,
                        fileName);
            this.Options.Writer.WriteLine(savedMessage);
        }

        /// <summary>
        /// Method to display all the rejections present in the input files.
        /// If the graph argument was passed in the input arguments, a DGML graph representing
        /// all the rejection issues is saved in a file called All.dgml.
        /// </summary>
        /// <remarks>
        /// Based on how the levels are assigned, the root causes for the errors in the
        /// application can easily be accessed by looking at the rejection issues present at
        /// the highest level.
        /// </remarks>
        private void ListAllRejections()
        {
            this.Options.Writer.WriteLine(Strings.AllRejectionsMessage);
            for (int level = this.MaxLevels; level > 0; level--)
            {
                this.ListErrorsinLevel(level);
            }

            bool saveGraph = this.Options.GraphPath.Length > 0;
            if (saveGraph)
            {
                GraphCreator creator = new GraphCreator(this.RejectionGraph);
                string fileName = "AllErrors.dgml";
                this.SaveGraph(fileName, creator);
            }
        }

        /// <summary>
        /// Method to the get the information about the rejection information that caused a
        /// particular import failure, rather than for the entire system.
        /// If graph was specified in the input arguments then a DGML graph tracing the rejection
        /// chain assocaited with the current path alone is saved to a file called [partName].dgml.
        /// </summary>
        /// <param name = "partName"> The name of the part which we want to analyze.</param>
        /// <remarks>
        /// Once again, the root causes can easily be accessed by looking at the rejection
        /// issues at the highest levels of the output.
        /// </remarks>
        private void ListReject(string partName)
        {
            string individualRejection = string.Format(
                    CultureInfo.CurrentCulture,
                    Strings.IndividualRejectionMessage,
                    partName);
            this.Options.Writer.WriteLine(individualRejection);

            // Deal with the case that there are no rejection issues with the given part
            if (!this.RejectionGraph.ContainsKey(partName))
            {
                string noRejection = string.Format(
                    CultureInfo.CurrentCulture,
                    Strings.NoRejectionMessage,
                    partName);
                this.Options.Writer.WriteLine(noRejection);
                return;
            }

            // Store just the nodes that are involved in the current rejection chain to use when generating the graph
            Dictionary<string, PartNode> relevantNodes = null;
            bool saveGraph = this.Options.GraphPath.Length > 0;
            if (saveGraph)
            {
                relevantNodes = new Dictionary<string, PartNode>();
            }

            // Perform Breadth First Search (BFS) with the node associated with partName as the root.
            // When performing BFS, only the child nodes are considered since we want to the root to be
            // the end point of the rejection chain(s).
            // BFS was chosen over DFS because of the fact that we process level by level when performing
            // the travesal and thus easier to communicate the causes and pathway to the end user
            Queue<PartNode> currentLevelNodes = new Queue<PartNode>();
            currentLevelNodes.Enqueue(this.RejectionGraph[partName]);
            while (currentLevelNodes.Count() > 0)
            {
                int currentLevel = currentLevelNodes.Peek().Level;
                string errorLevel = string.Format(
                    CultureInfo.CurrentCulture,
                    Strings.ErrorsLevelMessage,
                    currentLevel);
                this.Options.Writer.WriteLine(errorLevel);

                // Iterate through all the nodes in the current level
                int numNodes = currentLevelNodes.Count();
                List<string> errorMessages = new List<string>(numNodes);
                for (int index = 0; index < numNodes; index++)
                {
                    // Process the current node by displaying its import issue and adding it to the graph
                    PartNode current = currentLevelNodes.Dequeue();
                    if (saveGraph)
                    {
                        relevantNodes.Add(current.GetName(), current);
                    }

                    errorMessages.Add(this.GetNodeDetail(current));

                    // Add the "children" of the current node to the queue for future processing
                    if (current.ImportRejects.Count() > 0)
                    {
                        foreach (var childEdge in current.ImportRejects)
                        {
                            currentLevelNodes.Enqueue(childEdge.Target);
                        }
                    }
                }

                this.WriteLines(errorMessages);
                if (!this.Options.Verbose)
                {
                    this.Options.Writer.WriteLine();
                }
            }

            // Save the output graph if the user request it
            if (saveGraph)
            {
                GraphCreator nodeGraph = new GraphCreator(relevantNodes);

                // Replacing '.' with '_' in the fileName to ensure that the '.' is associated with the file extension
                string fileName = partName.Replace(".", "_") + ".dgml";
                this.SaveGraph(fileName, nodeGraph);
            }
        }

        /// <summary>
        /// Method to display information about a particular node to the user.
        /// </summary>
        /// <param name="current">The Node whose information we want to display.</param>
        /// <returns>A string with information regarding the input node</returns>
        private string GetNodeDetail(PartNode current)
        {
            string startMessage;
            if (current.IsWhiteListed)
            {
                startMessage = Strings.WhitelistLabel;
            }
            else
            {
                startMessage = string.Empty;
            }

            if (this.Options.Verbose)
            {
                StringWriter writer = new StringWriter(new StringBuilder());
                foreach (string errorMessage in current.VerboseMessages)
                {
                    string message = startMessage + errorMessage;
                    writer.WriteLine(message);
                    writer.WriteLine();
                }

                return writer.ToString();
            }
            else
            {
                string message = startMessage + this.GetName(current.Part, Strings.VerbosePartLabel);
                return message;
            }
        }
    }
}
