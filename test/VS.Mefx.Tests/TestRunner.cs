namespace VS.Mefx.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Threading.Tasks;
    using VS.Mefx;
    using Xunit;
    using Xunit.Abstractions;
    using Xunit.Sdk;

    public class TestRunner
    {
        private readonly ITestOutputHelper output;

        public TestRunner(ITestOutputHelper output)
        {
            this.output = output;
            var currentFileType = typeof(TestRunner);
        }

        private static readonly string OutputFileName = "output.txt";

        private static async Task<string> RunCommand(string[] args)
        {
            // Won't support quoted strings
            using (StreamWriter sw = new StreamWriter(OutputFileName))
            {
                await Program.Runner(sw, args);
            }

            string savedOutput = File.ReadAllText(OutputFileName).Trim();
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

        [Theory]
        [TestGetter(true)]
        private async Task OverrideTest(string filePath)
        {
            TestInfo fileData = new TestInfo(filePath);
            string[] args = fileData.GetCommandArgs();
            string savedOutput = await RunCommand(args);
            fileData.UpdateTestResult(savedOutput);
            string originalFilePath = fileData.FilePath;
            fileData.WriteToFile(originalFilePath);
        }

        [Theory]
        [TestGetter(false)]
        private async Task<bool> RunTest(string filePath)
        {
            TestInfo fileData = new TestInfo(filePath);
            string[] args = fileData.GetCommandArgs();
            string savedOutput = await RunCommand(args);
            List<string> commandResult = TestInfo.GetLines(savedOutput);
            List<string> expectedOutput = fileData.TestResult;
            return commandResult.SequenceEqual(expectedOutput);
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
            string command = "--match CarOne.MoreMetadata Garage.Importer --directory TestFiles/Matching";
            string result = await RunCommand(command.Split(" "));
            this.output.WriteLine(result);
        }

        [Fact]
        public async Task Playground()
        {
            var currType = typeof(TestRunner);
            string path = currType.Assembly.Location;
            this.output.WriteLine(path);
        }

        private class TestGetter : DataAttribute
        {
            private static string validFileExtension = "txt";
            public static string RunFolder = "TestData";
            public static string UpdateFolder = "..\\..\\..\\../test/VS.Mefx.Tests/TestData";

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

            public TestGetter(bool updateTests)
            {
                string folderName = RunFolder;
                if (updateTests)
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
                            this.TestFilePaths.Add(fileInfo.FullName);
                        }
                    }
                }
            }
        }
    }
}
