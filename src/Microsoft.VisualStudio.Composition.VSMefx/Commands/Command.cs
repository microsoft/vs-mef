﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.Composition.VSMefx.Commands
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Microsoft.VisualStudio.Composition;

    /// <summary>
    /// A general command class which serves a parent class for all the commands that can be run by application .
    /// </summary>
    internal class Command
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Command"/> class.
        /// </summary>
        /// <param name="derivedInfo">The ConfigCreator for the input files.</param>
        /// <param name="arguments">The command line arguments from the user.</param>
        internal Command(ConfigCreator derivedInfo, CLIOptions arguments)
        {
            this.Creator = derivedInfo;
            this.Options = arguments;
        }

        /// <summary>
        /// Write the given lines in sorted order to the user.
        /// </summary>
        /// <param name="lines">The lines to output to the user.</param>
        protected void WriteLines(List<string> lines)
        {
            IEnumerable<string> sortedLines = lines.OrderBy(line => line);
            foreach (string line in sortedLines)
            {
                this.Options.Writer.WriteLine(line);
            }
        }

        /// <summary>
        /// Gets the composable catalog and configuration.
        /// </summary>
        protected ConfigCreator Creator { get; }

        /// <summary>
        /// Gets the command line arguments passed in by the user.
        /// </summary>
        protected CLIOptions Options { get; }

        /// <summary>
        /// Method to get the name of the given its definition.
        /// </summary>
        /// <param name="part"> The defintion of the part whose name we want.</param>
        /// <param name="verboseLabel"> Label to add before the verbose description of the part.</param>
        /// <returns>
        /// A string representing either the simple or verbose name of the part based on if
        /// verbose was specified as an input argument.
        /// </returns>
        protected string GetName(ComposablePartDefinition part, string verboseLabel = "")
        {
            if (part == null)
            {
                return Strings.DoesNotExists;
            }

            Type partType = part.Type;
            if (this.Options.Verbose)
            {
                string divider = " ";
                if (verboseLabel.Length == 0)
                {
                    divider = string.Empty;
                }

                return verboseLabel + divider + partType.AssemblyQualifiedName;
            }
            else
            {
                return partType.FullName!;
            }
        }
    }
}
