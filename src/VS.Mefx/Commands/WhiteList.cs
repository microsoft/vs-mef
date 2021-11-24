// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace VS.Mefx.Commands
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Text.RegularExpressions;

    internal class WhiteList
    {
        // Constants associated with the Regex's for the expressions specified in the whitelist
        private static readonly TimeSpan MaxRegexTime = new TimeSpan(0, 0, 5);
        private static readonly RegexOptions RegexOptions = RegexOptions.IgnoreCase;

        /// <summary>
        /// Gets or sets a list of regex expression when doing whitelisting using regex.
        /// </summary>
        private HashSet<Regex>? WhiteListExpressions { get; set; }

        /// <summary>
        /// Gets or sets part names to whitelisting when not whitelisting.
        /// </summary>
        private HashSet<string>? WhiteListParts { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether we are using regex while whitelisting.
        /// </summary>
        private bool UsingRegex { get; set; }

        /// <summary>
        /// Gets or sets a <see cref="TextWriter"/> to output text to the user.
        /// </summary>
        private TextWriter Output { get; set; }

        public WhiteList(CLIOptions options)
        {
            // Read and process the whitelist file, if one is present
            this.UsingRegex = options.UseRegex;
            this.Output = options.Writer;
            if (this.UsingRegex)
            {
                this.WhiteListExpressions = new HashSet<Regex>();
            }
            else
            {
                this.WhiteListParts = new HashSet<string>();
            }

            if (options.WhiteListFile != null && options.WhiteListFile.Length > 0)
            {
                string currentFolder = Directory.GetCurrentDirectory();
                this.ReadWhiteListFile(currentFolder, options.WhiteListFile);
            }
        }

        /// <summary>
        /// Method to check if a given part is present in the whitelist or not.
        /// </summary>
        /// <param name="partName">The name of the part we want to check.</param>
        /// <returns> A boolean indicating if the specified part was included in the whitelist or not.</returns>
        public bool IsWhiteListed(string partName)
        {
            if (!this.UsingRegex)
            {
                return this.WhiteListParts.Contains(partName);
            }

            foreach (Regex test in this.WhiteListExpressions)
            {
                try
                {
                    if (test.IsMatch(partName))
                    {
                        return true;
                    }
                }
                catch (Exception error)
                {
                }
            }

            return false;
        }

        /// <summary>
        /// Method to process the input files based on whether we are using regex or not and
        /// print any issues encountered while processing the input file back to the user.
        /// </summary>
        /// <param name = "currentFolder">The complete path to the folder that the file is present in.</param>
        /// <param name = "fileName">The relative path to the file from the current folder.</param>
        private void ReadWhiteListFile(string currentFolder, string fileName)
        {
            string filePath = Path.Combine(currentFolder, fileName.Trim());
            if (!File.Exists(filePath))
            {
                string missingFile = string.Format(
                    CultureInfo.CurrentCulture,
                    Strings.MissingFileMessage,
                    fileName);
                this.Output.WriteLine(missingFile);
                return;
            }

            try
            {
                string[] lines = File.ReadAllLines(filePath);
                foreach (string description in lines)
                {
                    string name = description.Trim();
                    if (this.UsingRegex)
                    {
                        string pattern = @"^" + name + @"$";
                        this.WhiteListExpressions.Add(new Regex(pattern, RegexOptions, MaxRegexTime));
                    }
                    else
                    {
                        this.WhiteListParts.Add(name);
                    }
                }
            }
            catch (Exception error)
            {
                var errorMessage = string.Format(
                    CultureInfo.CurrentCulture,
                    Strings.ErrorMessage,
                    error.Message);
                this.Output.WriteLine(errorMessage);
            }
        }
    }
}
