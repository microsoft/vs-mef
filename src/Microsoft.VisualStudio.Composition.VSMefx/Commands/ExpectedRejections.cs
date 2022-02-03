// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.Composition.VSMefx.Commands
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Text.RegularExpressions;

    internal class ExpectedRejections
    {
        // Constants associated with the Regex's for the expressions specified in the expected rejections list
        private static readonly TimeSpan MaxRegexTime = TimeSpan.FromSeconds(5);
        private static readonly RegexOptions RegexOptions = RegexOptions.IgnoreCase;

        /// <summary>
        /// Gets or sets a list of regex expression when doing expected rejection checks using regex.
        /// </summary>
        private HashSet<Regex>? Expressions { get; set; }

        /// <summary>
        /// Gets or sets the names of parts names expect rejection from.
        /// </summary>
        private HashSet<string>? Parts { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether we are using regex while testing for expected rejections.
        /// </summary>
        private bool UsingRegex { get; set; }

        /// <summary>
        /// Gets or sets a <see cref="CLIOptions"/> of the input arguments passed by the user.
        /// </summary>
        private CLIOptions Options { get; set; }

        internal ExpectedRejections(CLIOptions options)
        {
            // Read and process the expected rejections file, if one is present
            this.Options = options;
            this.UsingRegex = options.UseRegex;
            if (this.UsingRegex)
            {
                this.Expressions = new HashSet<Regex>();
            }
            else
            {
                this.Parts = new HashSet<string>();
            }

            if (options.ExpectedRejectionsFile != null && options.ExpectedRejectionsFile.Length > 0)
            {
                string currentFolder = Directory.GetCurrentDirectory();
                this.ReadExpectedRejectionsFile(currentFolder, options.ExpectedRejectionsFile);
            }
        }

        /// <summary>
        /// Checks whether a given part is present in the list of parts expected to be rejected.
        /// </summary>
        /// <param name="partName">The name of the part we want to check.</param>
        /// <returns>A boolean value.</returns>
        internal bool IsRejectionExpected(string partName)
        {
            if (!this.UsingRegex)
            {
                return this.Parts == null ? false : this.Parts.Contains(partName);
            }

            if (this.Expressions == null)
            {
                return false;
            }

            foreach (Regex test in this.Expressions)
            {
                try
                {
                    if (test.IsMatch(partName))
                    {
                        return true;
                    }
                }
                catch
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
        private void ReadExpectedRejectionsFile(string currentFolder, string fileName)
        {
            string filePath = Path.Combine(currentFolder, fileName.Trim());
            if (!File.Exists(filePath))
            {
                string missingFile = string.Format(
                    CultureInfo.CurrentCulture,
                    Strings.MissingFileMessage,
                    fileName);
                this.Options.ErrorWriter.WriteLine(missingFile);
                return;
            }

            try
            {
                string[] lines = File.ReadAllLines(filePath);
                foreach (string description in lines)
                {
                    string name = description.Trim();
                    if (this.UsingRegex && this.Expressions != null)
                    {
                        string pattern = @"^" + name + @"$";
                        this.Expressions.Add(new Regex(pattern, RegexOptions, MaxRegexTime));
                    }
                    else if (this.Parts != null)
                    {
                        this.Parts.Add(name);
                    }
                }
            }
            catch (Exception error)
            {
                var errorMessage = string.Format(
                    CultureInfo.CurrentCulture,
                    Strings.ErrorMessage,
                    error.Message);
                this.Options.ErrorWriter.WriteLine(errorMessage);
            }
        }
    }
}
