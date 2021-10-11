namespace VS.Mefx.Tests
{
    using System;
    using System.Collections.Generic;
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
        private readonly ITestOutputHelper output;

        private static bool testOverride = false;

        public TestRunner(ITestOutputHelper output)
        {
            this.output = output;
        }

        private static async Task<string> RunCommand(string[] args)
        {
            // Won't support quoted strings
            StringBuilder builder = new StringBuilder();
            using (StringWriter sw = new StringWriter(builder))
            {
                await Program.Runner(sw, args);
            }

            string savedOutput = builder.ToString().Trim();
            return savedOutput;
        }

        private static async Task<string> CreateTest(string outputFilePath, string command)
        {
            TestInfo testData = new TestInfo();
            testData.UpdateTestCommand(command);
            string[] args = testData.GetCommandArgs();
            string savedOutput = await RunCommand(args);
            testData.UpdateTestResult(savedOutput);
            testData.WriteToFile(outputFilePath);
            return savedOutput;
        }

        private async Task OverrideTest(string filePath)
        {
            TestInfo fileData = new TestInfo(filePath);
            string[] args = fileData.GetCommandArgs();
            string savedOutput = await RunCommand(args);
            fileData.UpdateTestResult(savedOutput);
            string originalFilePath = fileData.FilePath;
            fileData.WriteToFile(originalFilePath);
        }

        private async Task JustRunTest(string filePath)
        {
            TestInfo fileData = new TestInfo(filePath);
            string[] args = fileData.GetCommandArgs();
            string savedOutput = await RunCommand(args);
            List<string> commandResult = TestInfo.GetLines(savedOutput);
            List<string> expectedOutput = fileData.TestResult;
            string commandText = string.Join("\n", commandResult);
            string expectedText = string.Join("\n", expectedOutput);
            Assert.False(PrintDiff("Expected", "Result", expectedText, commandText, this.output));
        }

        [Theory]
        [TestGetter]
        private async Task Runner(string fileName)
        {
            string filePath = TestGetter.GetFilePath(fileName);
            if (testOverride)
            {
                await this.OverrideTest(filePath);
            } else
            {
                await this.JustRunTest(filePath);
            }
        }

        [Fact]
        public async Task CreateSampleTest()
        {
            string testName = "MatchingMissingField.txt";
            string testCommand = "--match CarOne.MoreMetadata Garage.Importer --match-imports NoCar --directory TestFiles/Matching";

            string testFilePath = Path.Combine(TestGetter.UpdateFolder, testName);
            string currentDir = Directory.GetCurrentDirectory();
            string filePath = Path.GetFullPath(Path.Combine(currentDir, testFilePath));
            string result = await CreateTest(filePath, testCommand);
            this.output.WriteLine(result);
        }

        [Fact]
        public async Task RunSampleTest()
        {
            string command = "--verbose --rejected ExtendedOperations.Modulo --directory TestFiles/Basic";
            string result = await RunCommand(command.Split(" "));
            this.output.WriteLine(result);
        }

        [Fact]
        public async Task Playground()
        {
            await this.Runner("BasicDetail.txt");
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
                if (TestRunner.testOverride)
                {
                    folderName = UpdateFolder;
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
                if (TestRunner.testOverride)
                {
                    folderName = UpdateFolder;
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
