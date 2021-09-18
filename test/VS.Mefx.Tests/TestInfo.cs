namespace VS.Mefx.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text.RegularExpressions;

    internal class TestInfo
    {
        public string TestCommand { get; set; }

        public List<string> TestResult { get; set; }

        public string? FilePath { get; set; }

        public TestInfo()
        {
            this.TestCommand = string.Empty;
            this.TestResult = new List<string>();
        }

        public TestInfo(string filePath)
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
            lines.RemoveAt(0);
            this.TestResult = lines;
            this.FilePath = filePath;
        }

        public void UpdateTestCommand(string command)
        {
            this.TestCommand = command.Trim();
        }

        public void UpdateTestResult(string text)
        {
            this.TestResult = GetLines(text);
        }

        public static List<string> GetLines(string text)
        {
            List<string> lines = new List<string>();
            using (StringReader sr = new StringReader(text))
            {
                string line;
                while ((line = sr.ReadLine()) != null)
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
                this.TestResult.ForEach(line => sw.WriteLine(line));
            }
        }

        public string[] GetCommandArgs()
        {
            return Regex.Split(this.TestCommand.Trim(), @"\s+");
        }

    }
}
