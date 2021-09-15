namespace VS.Mefx
{
    using System;
    using System.Collections.Generic;
    using System.CommandLine.DragonFruit;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Threading.Tasks;
    using VS.Mefx.Commands;

    /// <summary>
    /// Main class that process and runs the user's command.
    /// </summary>
    public class Program
    {
        public static readonly string OutputFileName = "out.txt";
        public static TextWriter Output = null;

        /// <summary>
        /// A command line application to diagonse composition failures in MEF applications.
        /// </summary>
        /// <param name="verbose">An boolean option to toggle the detail level of the text output.</param>
        /// <param name="file">Specify files from which we want to load parts from.</param>
        /// <param name="directory">Specify folders from which we want to load parts from.</param>
        /// <param name="parts">An boolean to toggle if we want to print out all the parts.</param>
        /// <param name="detail">Specify the parts we want to get more information about.</param>
        /// <param name="importer">List the parts who import the specified contract name(s).</param>
        /// <param name="exporter">List the parts who export the specified contract name(s).</param>
        /// <param name="rejected">List the rejection causes for a given part (use all to list every rejection error).</param>
        /// <param name="graph">Specify path to directory to save the rejection DGML file.</param>
        /// <param name="whitelist">A file which lists the parts we expect to be rejected.</param>
        /// <param name="regex">Treat the text in the whitelist file as regular expressions.</param>
        /// <param name="cache">Specify the name of the output file to store the loaded parts.</param>
        /// <param name="match">Check relationship between given part which are provided in order: ExportPart ImportPart.</param>
        /// <param name="matchExports">List of fields in the export part that we want to consider.</param>
        /// <param name="matchImports">List of fields in the import part that we want to consider.</param>
        public static async Task Main(
            bool verbose = false,
            List<string>? file = null,
            List<string>? directory = null,
            bool parts = false,
            List<string>? detail = null,
            List<string>? importer = null,
            List<string>? exporter = null,
            List<string>? rejected = null,
            string graph = "",
            string whitelist = "",
            bool regex = false,
            string cache = "",
            List<string>? match = null,
            List<string>? matchExports = null,
            List<string>? matchImports = null)
        {
            if (Output == null)
            {
                Output = Console.Out;
            }

            CLIOptions options = new CLIOptions
            {
                Verbose = verbose,
                Files = file,
                Folders = directory,
                ListParts = parts,
                PartDetails = detail,
                ImportDetails = importer,
                ExportDetails = exporter,
                RejectedDetails = rejected,
                GraphPath = graph,
                WhiteListFile = whitelist,
                UseRegex = regex,
                CacheFile = cache,
                MatchParts = match,
                MatchExports = matchExports,
                MatchImports = matchImports,
                Writer = Output,
            };
            await RunOptions(options);
        }

        public static async Task Runner(string[] args)
        {
            Output = new StreamWriter(OutputFileName);
            MethodInfo mainInfo = typeof(Program).GetMethod("Main");
            await CommandLine.InvokeMethodAsync(args, mainInfo);
        }

        /// <summary>
        /// Performs the operations and commands specified in the input arguments.
        /// </summary>
        private static async Task RunOptions(CLIOptions options)
        {
            ConfigCreator creator = new ConfigCreator(options);
            await creator.Initialize();
            if (creator.Catalog == null)
            {
                options.Writer.WriteLine("Couldn't find any parts in the input files and folders");
                return;
            }

            PartInfo infoGetter = new PartInfo(creator, options);
            infoGetter.PrintRequestedInfo();
            if (options.MatchParts != null && options.MatchParts.Count() > 0)
            {
                MatchChecker checker = new MatchChecker(creator, options);
                checker.PerformMatching();
            }

            // Perform rejection tracing as well as visualization if specified
            if (options.RejectedDetails != null && options.RejectedDetails.Count() > 0)
            {
                RejectionTracer tracer = new RejectionTracer(creator, options);
                tracer.PerformRejectionTracing();
            }
        }
    }
}
