namespace VS.Mefx
{
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
        public bool Verbose { get; set; }

        /// <summary>
        /// Gets or sets the name of the files to consider.
        /// </summary>
        public List<string>? Files { get; set; }

        /// <summary>
        /// Gets or sets the name of the folders to consider.
        /// </summary>
        public List<string>? Folders { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the program should list all the parts.
        /// </summary>
        public bool ListParts { get; set; }

        /// <summary>
        /// Gets or sets the name of the parts whose complete details need to be printed out.
        /// </summary>
        public List<string>? PartDetails { get; set; }

        /// <summary>
        /// Gets or sets the contract names whose importing parts need to be listed.
        /// </summary>
        public List<string>? ImportDetails { get; set; }

        /// <summary>
        /// Gets or sets the contract names whose exporting parts need to be listed.
        /// </summary>
        public List<string>? ExportDetails { get; set; }

        /// <summary>
        /// Gets or sets the name of the parts whose rejection detail we want.
        /// </summary>
        public List<string>? RejectedDetails { get; set; }

        /// <summary>
        /// Gets or sets the relative path to store the DGML files in.
        /// </summary>
        public string GraphPath { get; set; }

        /// <summary>
        /// Gets or sets the name of the whitelist file to use.
        /// </summary>
        public string WhiteListFile { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether to treat the whitelist file text as regex.
        /// </summary>
        public bool UseRegex { get; set; }

        /// <summary>
        /// Gets or sets the name of the cache file to store the imported parts.
        /// </summary>
        public string CacheFile { get; set; }

        /// <summary>
        /// Gets or sets the name of the parts to perform matching on.
        /// </summary>
        public List<string>? MatchParts { get; set; }

        /// <summary>
        /// Gets or sets the name of the fields to consider in the exporting part when matching.
        /// </summary>
        public List<string>? MatchExports { get; set; }

        /// <summary>
        /// Gets or sets the name of the fields to consider in the importing part when matching.
        /// </summary>
        public List<string>? MatchImports { get; set; }

        /// <summary>
        /// Gets or sets the writer to use when writing output to the user.
        /// </summary>
        public TextWriter Writer { get; set; }
    }
}
