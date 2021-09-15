namespace VS.Mefx.Tests
{
    using System.Threading.Tasks;
    using Xunit;
    using Xunit.Abstractions;
    using VS.Mefx;
    using System;
    using System.IO;
    using System.Collections.Generic;

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

            if (targetDirExists)
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
            string relativePath = "..\\..\\..\\..\\test\\VS.Mefx.Tests";
            string srcDir = Path.GetFullPath(Path.Combine(currentDir, relativePath));
            moveAllSubFolders(srcDir, currentDir, false);
        }

        private static string ReadFile(string FileName)
        {
            string result = File.ReadAllText(FileName);
            return result.Trim();
        }

        [Fact]
        public async Task Test1()
        {
            await Program.Runner(new string[] { "--parts", "--directory", "Basic" });
            Program.Output.Flush();
            Program.Output.Dispose();
            string savedOutput = ReadFile(Program.OutputFileName);
            this.output.WriteLine(savedOutput);
            Assert.True(true);
        }
    }
}
