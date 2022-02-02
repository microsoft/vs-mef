// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.Composition.VSMefx.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text.RegularExpressions;

    internal class TestInfo
    {
        private static readonly string TestSeperator = "***";

        public string TestCommand { get; set; }

        public List<string> TestOutputNormal { get; set; }

        public List<string> TestOutputError { get; set; }

        public string FilePath { get; set; }

        public TestInfo()
        {
            this.TestCommand = string.Empty;
            this.TestOutputNormal = new List<string>();
            this.TestOutputError = new List<string>();
            this.FilePath = string.Empty;
        }

        public TestInfo(string filePath)
            : this()
        {
            if (!File.Exists(filePath))
            {
                throw new ArgumentException("Couldn't find file " + filePath);
            }

            string text = File.ReadAllText(filePath);
            List<string> lines = GetLines(text);
            if (lines.Count < 2)
            {
                throw new ArgumentException("Invalid file format when parsing file " + filePath);
            }

            this.TestCommand = lines[0];
            this.FilePath = filePath;
            lines.RemoveAt(0);
            int iterateIndex = 0;

            // Get the text that is printed out to standard output
            while (iterateIndex < lines.Count)
            {
                string line = lines[iterateIndex];
                if (line.Equals(TestSeperator))
                {
                    iterateIndex += 1;
                    break;
                }

                this.TestOutputNormal.Add(line);
                iterateIndex += 1;
            }

            // Get the text that is printed out to standard error
            while (iterateIndex < lines.Count)
            {
                this.TestOutputError.Add(lines[iterateIndex]);
                iterateIndex += 1;
            }
        }

        public void UpdateTestCommand(string command)
        {
            this.TestCommand = command.Trim();
        }

        public void UpdateTestOutput(string text)
        {
            this.TestOutputNormal = GetLines(text);
        }

        public void UpdateTestError(string text)
        {
            this.TestOutputError = GetLines(text);
        }

        public static List<string> GetLines(string text)
        {
            List<string> lines = new List<string>();
            using (StringReader sr = new StringReader(text))
            {
                string line;
                while ((line = sr.ReadLine()!) != null)
                {
                    line = line.Trim();
                    if (line.Length > 0)
                    {
                        lines.Add(line);
                    }
                }
            }

            return lines;
        }

        public void WriteToFile(string outputFilePath)
        {
            using (StreamWriter sw = new StreamWriter(outputFilePath))
            {
                sw.WriteLine(this.TestCommand);
                this.TestOutputNormal.ForEach(line => sw.WriteLine(line));
                sw.WriteLine(TestSeperator);
                this.TestOutputError.ForEach(line => sw.WriteLine(line));
            }
        }

        public string[] GetCommandArgs()
        {
            return Regex.Split(this.TestCommand.Trim(), @"\s+");
        }
    }
}
