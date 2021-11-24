// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace VS.Mefx.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using DiffPlex;
    using DiffPlex.DiffBuilder;
    using DiffPlex.DiffBuilder.Model;
    using Microsoft;
    using Xunit;
    using Xunit.Abstractions;
    using Xunit.Sdk;

    public class TestRunner
    {
        private const bool TestOverride = false;
        private const bool IgnoreHelperFacts = true;
        private const string SkipLabel = IgnoreHelperFacts ? "Debugging" : null;

        private readonly ITestOutputHelper output;

        public TestRunner(ITestOutputHelper output)
        {
            this.output = output;
        }

        private static async Task<string[]> RunCommand(string[] args)
        {
            // Won't support quoted strings
            StringBuilder normalBuilder = new StringBuilder();
            StringBuilder errorBuilder = new StringBuilder();

            using (StringWriter errorWriter = new StringWriter(errorBuilder))
            using (StringWriter normalWriter = new StringWriter(normalBuilder))
            {
                await Program.Runner(normalWriter, errorWriter, args);
            }

            string normalOutput = normalBuilder.ToString().Trim();
            string errorOutput = errorBuilder.ToString().Trim();
            return new string[] { normalOutput, errorOutput };
        }

        private void PrintCommandResult(string[] result)
        {
            this.output.WriteLine("Default Output:");
            this.output.WriteLine(result[0]);
            this.output.WriteLine("Standard Error: ");
            this.output.WriteLine(result[1]);
        }

        private async Task<string[]> CreateTest(string outputFilePath, string command)
        {
            TestInfo testData = new TestInfo();
            testData.UpdateTestCommand(command);
            string[] args = testData.GetCommandArgs();
            string[] commandOutput = await RunCommand(args);
            this.PrintCommandResult(commandOutput);

            testData.UpdateTestOutput(commandOutput[0]);
            testData.UpdateTestError(commandOutput[1]);
            testData.WriteToFile(outputFilePath);

            return commandOutput;
        }

        private async Task OverrideTest(string filePath)
        {
            TestInfo fileData = new TestInfo(filePath);
            string[] args = fileData.GetCommandArgs();
            string[] commandOutput = await RunCommand(args);
            this.PrintCommandResult(commandOutput);

            fileData.UpdateTestOutput(commandOutput[0]);
            fileData.UpdateTestError(commandOutput[1]);

            string originalFilePath = fileData.FilePath;
            fileData.WriteToFile(originalFilePath);
        }

        private bool RunComparsion(string type, List<string> expectedOutput, string output)
        {
            this.output.WriteLine("Running analysis for " + type);
            List<string> commandResult = TestInfo.GetLines(output);
            string commandText = string.Join("\n", commandResult);
            string expectedText = string.Join("\n", expectedOutput);
            bool isSame = !PrintDiff("Expected", "Result", expectedText, commandText, this.output);
            return isSame;
        }

        private async Task JustRunTest(string filePath)
        {
            TestInfo fileData = new TestInfo(filePath);
            string[] args = fileData.GetCommandArgs();
            string[] commandOutput = await RunCommand(args);
            bool defaultMatch = this.RunComparsion("standard out", fileData.TestOutputNormal, commandOutput[0]);
            bool errorMatch = this.RunComparsion("standard err", fileData.TestOutputError, commandOutput[1]);
            Assert.True(defaultMatch && errorMatch);
        }

        [Theory]
        [TestGetter]
        private async Task Runner(string fileName)
        {
            try
            {
                string filePath = TestGetter.GetFilePath(fileName);
                if (TestOverride)
                {
#pragma warning disable CS0162 // Unreachable code detected
                    await this.OverrideTest(filePath);
#pragma warning restore CS0162 // Unreachable code detected
                }
                else
                {
                    await this.JustRunTest(filePath);
                }
            }
            catch (Exception)
            {
                throw;
            }
        }

        [Fact(Skip = SkipLabel)]
        public async Task CreateSampleTest()
        {
            string testName = "DetailNoSuchPart.txt";
            string testCommand = "--detail NonExistantPart --directory TestFiles/Basic";

            string testFilePath = Path.Combine(TestGetter.UpdateFolder, testName);
            string currentDir = Directory.GetCurrentDirectory();
            string filePath = Path.GetFullPath(Path.Combine(currentDir, testFilePath));
            string[] result = await this.CreateTest(filePath, testCommand);
        }

        [Fact(Skip = SkipLabel)]
        public async Task RunSampleTest()
        {
            string command = "--match CarOne.InvalidType User.Importer --match-exports CarOne.InvalidType --directory TestFiles/Matching";
            string[] result = await RunCommand(command.Split(" "));
            this.PrintCommandResult(result);
        }

        [Fact(Skip = SkipLabel)]
        public async Task Playground()
        {
            await this.Runner("MatchTypeTest.txt");
        }

        private class TestGetter : DataAttribute
        {
            private static string validFileExtension = "txt";

            public static string UpdateFolder =
                string.Format("..{0}..{0}..{0}..{0}test{0}VS.Mefx.Tests{0}TestData",
                    Path.DirectorySeparatorChar);

            public static string RunFolder = "TestData";

            public static string GetFilePath(string fileName)
            {
                string folderName = RunFolder;
                if (TestRunner.TestOverride)
                {
#pragma warning disable CS0162 // Unreachable code detected
                    folderName = UpdateFolder;
#pragma warning restore CS0162 // Unreachable code detected
                }

                string currentDir = Directory.GetCurrentDirectory();
                string folderPath = Path.Combine(currentDir, folderName);
                string filePath = Path.Combine(folderPath, fileName);
                return Path.GetFullPath(filePath);
            }

            public override IEnumerable<object[]> GetData(MethodInfo testMethod)
            {
                List<object[]> output = new List<object[]>();
                foreach (var filePath in this.TestFilePaths)
                {
                    output.Add(new object[] { filePath });
                }

                return output.AsEnumerable();
            }

            private List<string> TestFilePaths { get; set; }

            public TestGetter()
            {
                string folderName = RunFolder;
                if (TestRunner.TestOverride)
                {
#pragma warning disable CS0162 // Unreachable code detected
                    folderName = UpdateFolder;
#pragma warning restore CS0162 // Unreachable code detected
                }

                string currentDir = Directory.GetCurrentDirectory();
                string folderPath = Path.GetFullPath(Path.Combine(currentDir, folderName));
                this.TestFilePaths = new List<string>();
                if (Directory.Exists(folderPath))
                {
                    DirectoryInfo dirInfo = new DirectoryInfo(folderPath);
                    foreach (var fileInfo in dirInfo.EnumerateFiles())
                    {
                        string fileName = fileInfo.Name;
                        if (fileName.Contains(validFileExtension))
                        {
                            this.TestFilePaths.Add(fileName);
                        }
                    }
                }
            }
        }

        private static bool PrintDiff(
            string beforeDescription,
            string afterDescription,
            string before,
            string after,
            ITestOutputHelper output)
        {
            Requires.NotNull(output, nameof(output));

            var d = new Differ();
            var inlineBuilder = new InlineDiffBuilder(d);
            var result = inlineBuilder.BuildDiffModel(before, after);
            if (result.Lines.Any(l => l.Type != ChangeType.Unchanged))
            {
                output.WriteLine("Catalog {0} vs. {1}", beforeDescription, afterDescription);
                foreach (var line in result.Lines)
                {
                    string prefix;
                    if (line.Type == ChangeType.Inserted)
                    {
                        prefix = "+ ";
                    }
                    else if (line.Type == ChangeType.Deleted)
                    {
                        prefix = "- ";
                    }
                    else
                    {
                        prefix = "  ";
                    }

                    output.WriteLine(prefix + line.Text);
                }

                return true;
                ////Assert.False(anyStringRepresentationDifferences, "Catalogs not equivalent");
            }

            return false;
        }
    }
}
