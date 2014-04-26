namespace Microsoft.VisualStudio.Composition.Tasks
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.Build.Framework;

    public class CreateContainerFactoryBootstrapFile : Microsoft.Build.Utilities.Task
    {
        [Required]
        public string BootstrapFile { get; set; }

        [Required]
        public string ConfigurationAssemblyName { get; set; }

        [Required]
        public string RootNamespace { get; set; }

        public override bool Execute()
        {
            string sourceFileContent = this.GetSourceFileTemplate()
                .Replace("$rootnamespace$", this.RootNamespace)
                .Replace("$ConfigurationAssemblyName$", this.ConfigurationAssemblyName);

            File.WriteAllText(
                this.BootstrapFile,
               sourceFileContent);

            return !this.Log.HasLoggedErrors;
        }

        private string GetSourceFileTemplate()
        {
            var assembly = Assembly.GetExecutingAssembly();
            using (Stream resourceStream = assembly.GetManifestResourceStream(this.GetType(), "ExportProviderFactory.cs"))
            {
                using (var sr = new StreamReader(resourceStream))
                {
                    return sr.ReadToEnd();
                }
            }
        }
    }
}
