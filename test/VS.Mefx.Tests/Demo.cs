namespace VS.Mefx.Tests
{
    using System.Threading.Tasks;
    using Xunit;
    using Xunit.Abstractions;
    using VS.Mefx;
    using System;
    using System.IO;
    using System.Collections.Generic;
    using System.Text.RegularExpressions;
    using System.Linq;
    using System.Collections;
    using Xunit.Sdk;
    using System.Reflection;

    public class Demo
    {
        private readonly ITestOutputHelper output;

        private void moveAllSubFolders(string src, string target, bool saveFiles = true)
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
                moveAllSubFolders(subDir.FullName, destFolderPath);
            }
        }

        private void checkFolder()
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

        public Demo(ITestOutputHelper output)
        {
            this.output = output;
        }

        private static readonly string OutputFileName = "output.txt";

        private static async Task<string> RunCommand(string command)
        {
            // Won't support quoted strings
            string[] parts = Regex.Split(command.Trim(), @"\s+");
            using (StreamWriter sw = new StreamWriter(OutputFileName))
            {
                await Program.Runner(sw, parts);
            }

            string savedOutput = File.ReadAllText(OutputFileName).Trim();
            return savedOutput;
        }

        [Theory]
        [TestGetter("TestData")]
        private async Task<bool> RunTest(string filePath)
        {
            TestInfo fileData = new TestInfo(filePath);
            string[] args = fileData.GetCommandArgs();
            using (StreamWriter sw = new StreamWriter(OutputFileName))
            {
                await Program.Runner(sw, args);
            }

            string savedOutput = File.ReadAllText(OutputFileName);
            List<string> commandResult = TestInfo.GetLines(savedOutput);
            List<string> expectedOutput = fileData.TestResult;
            return commandResult.SequenceEqual(expectedOutput);
        }

        [Fact]
        public async Task GetBasicParts()
        {
            var result = await RunCommand("--parts --directory Basic");
            this.output.WriteLine(result);
            Assert.True(true);
        }

        [Fact]
        public async Task GetMatchingParts()
        {
            var result = await RunCommand("--parts --directory Matching");
            this.output.WriteLine(result);
            Assert.True(true);
        }

        [Fact]
        public async Task ReadFile()
        {
            string currentDir = Directory.GetCurrentDirectory();
            string relativePath = Path.Combine("TestData", "matchParts.txt");
            string filePath = Path.GetFullPath(Path.Combine(currentDir, relativePath));
            Assert.True(await RunTest(filePath));
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
