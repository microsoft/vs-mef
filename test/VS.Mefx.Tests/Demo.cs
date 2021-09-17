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

        public Demo(ITestOutputHelper output)
        {
            this.output = output;
            string currentDir = Directory.GetCurrentDirectory();
            string dirName = Path.GetFileName(currentDir);
            if (!dirName.Equals("VS.Mefx.Tests"))
            {
                string relativePath = "..\\..\\..\\..\\test\\VS.Mefx.Tests";
                string srcDir = Path.GetFullPath(Path.Combine(currentDir, relativePath));
                Directory.SetCurrentDirectory(srcDir);
            }
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
    }
}
