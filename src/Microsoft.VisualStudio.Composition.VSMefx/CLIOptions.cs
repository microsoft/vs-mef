// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.Composition.VSMefx
{
    using System;
    using System.Collections.Generic;
    using System.IO;

    /// <summary>
    /// A class to store the command line arguments passed in by the user.
    /// </summary>
    internal class CLIOptions
    {
        /// <summary>
        /// Gets or sets a value indicating whether we want the text output in detail.
        /// </summary>
        internal bool Verbose { get; set; }

        /// <summary>
        /// Gets or sets the name of the files to consider.
        /// </summary>
        internal List<string>? Files { get; set; }

        /// <summary>
        /// Gets or sets the name of the folders to consider.
        /// </summary>
        internal List<string>? Folders { get; set; }

        /// <summary>
        /// Gets or sets the path to an .exe.config or .dll.config file that can help resolve assembly references.
        /// </summary>
        internal string? ConfigFile { get; set; }

        /// <summary>
        /// Gets or sets the path to the directory to consider the base directory for relative paths in <see cref="ConfigFile"/>. If unspecified, the directory containing <see cref="ConfigFile"/> will be used.
        /// </summary>
        internal string? BaseDir { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the program should list all the parts.
        /// </summary>
        internal bool ListParts { get; set; }

        /// <summary>
        /// Gets or sets the name of the parts whose complete details need to be printed out.
        /// </summary>
        internal List<string>? PartDetails { get; set; }

        /// <summary>
        /// Gets or sets the contract names whose importing parts need to be listed.
        /// </summary>
        internal List<string>? ImportDetails { get; set; }

        /// <summary>
        /// Gets or sets the contract names whose exporting parts need to be listed.
        /// </summary>
        internal List<string>? ExportDetails { get; set; }

        /// <summary>
        /// Gets or sets the name of the parts whose rejection detail we want.
        /// </summary>
        internal List<string>? RejectedDetails { get; set; }

        /// <summary>
        /// Gets or sets the relative path to store the DGML files in.
        /// </summary>
        internal string? GraphPath { get; set; }

        /// <summary>
        /// Gets or sets the name of the file to read expected rejections from.
        /// </summary>
        internal string? ExpectedRejectionsFile { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether to treat the expected rejections file text as regex.
        /// </summary>
        internal bool UseRegex { get; set; }

        /// <summary>
        /// Gets or sets the name of the cache file to store the imported parts.
        /// </summary>
        internal string? CacheFile { get; set; }

        /// <summary>
        /// Gets or sets the name of the parts to perform matching on.
        /// </summary>
        internal List<string>? MatchParts { get; set; }

        /// <summary>
        /// Gets or sets the name of the fields to consider in the exporting part when matching.
        /// </summary>
        internal List<string>? MatchExports { get; set; }

        /// <summary>
        /// Gets or sets the name of the fields to consider in the importing part when matching.
        /// </summary>
        internal List<string>? MatchImports { get; set; }

        /// <summary>
        /// Gets or sets the writer to use when writing output to the user.
        /// </summary>
        internal TextWriter Writer { get; set; } = Console.Out;

        /// <summary>
        /// Gets or sets the writer to use when writing error messages to the user.
        /// </summary>
        internal TextWriter ErrorWriter { get; set; } = Console.Error;
    }
}
