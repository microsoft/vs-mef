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

        private void MoveAllSubFolders(string src, string target, bool saveFiles = true)
        {
            bool targetDirExists = Directory.Exists(target);
            if (saveFiles && targetDirExists)
            {
                return;
            }

            if (!targetDirExists)
            {
                Directory.CreateDirectory(target);
            }

            DirectoryInfo currentDir = new DirectoryInfo(src);
            if (saveFiles)
            {
                foreach (FileInfo file in currentDir.EnumerateFiles())
                {
                    string destFilePath = Path.Combine(target, file.Name);
                    file.CopyTo(destFilePath, true);
                }
            }

            foreach (DirectoryInfo subDir in currentDir.EnumerateDirectories())
            {
                string destFolderPath = Path.Combine(target, subDir.Name);
                this.MoveAllSubFolders(subDir.FullName, destFolderPath);
            }
        }

        private void CheckFolder()
        {
            string currentDir = Directory.GetCurrentDirectory();
            string dirName = Path.GetFileName(currentDir);
            if (!dirName.Equals("VS.Mefx.Tests"))
            {
                string relativePath = "..\\..\\..\\..\\test\\VS.Mefx.Tests";
                string srcDir = Path.GetFullPath(Path.Combine(currentDir, relativePath));
                Directory.SetCurrentDirectory(srcDir);
            }
        }

        public TestRunner(ITestOutputHelper output)
        {
            this.output = output;
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

        private static async Task<string> CreateTest(string testFileName, string command)
        {
            TestInfo testData = new TestInfo();
            testData.UpdateTestCommand(command);
            string[] args = testData.GetCommandArgs();
            string savedOutput = await RunCommand(args);
            testData.UpdateTestResult(savedOutput);

            string currentDir = Directory.GetCurrentDirectory();
            string folderPath = Path.Combine(currentDir, "TestData");
            string filePath = Path.Combine(folderPath, testFileName);
            testData.WriteToFile(filePath);
            return savedOutput;
        }

        [Theory]
        [TestGetter("TestData")]
        private async Task UpdateAllTests(string filePath)
        {
            TestInfo fileData = new TestInfo(filePath);
            string[] args = fileData.GetCommandArgs();
            string savedOutput = await RunCommand(args);
            fileData.UpdateTestResult(savedOutput);
            string originalFilePath = fileData.FilePath;
            fileData.WriteToFile(originalFilePath);
        }

        [Theory]
        [TestGetter("TestData")]
        private async Task<bool> RunAllTests(string filePath)
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
            string testName = "CacheReader.txt";
            string testCommand = "--parts --file TestFiles/Basic/All.cache";
            string result = await CreateTest(testName, testCommand);
            this.output.WriteLine(result);
        }

        [Fact]
        public async Task RunSampleTest()
        {
            string command = "--match CarOne.MoreMetadata Garage.Importer --directory TestFiles/Matching";
            string result = await RunCommand(command.Split(" "));
            this.output.WriteLine(result);
        }

        private class TestGetter : DataAttribute
        {
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

            public TestGetter(string folderName)
            {
                string currentDir = Directory.GetCurrentDirectory();
                string folderPath = Path.GetFullPath(Path.Combine(currentDir, folderName));
                this.TestFilePaths = new List<string>();
                if (Directory.Exists(folderPath))
                {
                    DirectoryInfo dirInfo = new DirectoryInfo(folderPath);
                    foreach (var fileInfo in dirInfo.EnumerateFiles())
                    {
                        this.TestFilePaths.Add(fileInfo.FullName);
                    }
                }
            }
        }
    }
}
