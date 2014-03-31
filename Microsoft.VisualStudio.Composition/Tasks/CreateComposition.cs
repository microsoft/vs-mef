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

    public class CreateComposition : Microsoft.Build.Utilities.Task
    {
        public ITaskItem[] CatalogAssemblies { get; set; }

        [Required]
        public string ConfigurationOutputPath { get; set; }

        public override bool Execute()
        {
            AppDomain.CurrentDomain.AssemblyResolve += CurrentDomain_AssemblyResolve;
            try
            {
                var discovery = new AttributedPartDiscovery();
                var parts = discovery.CreateParts(this.CatalogAssemblies.Select(item => Assembly.LoadFile(item.ItemSpec)));
                var catalog = ComposableCatalog.Create(parts);
                var configuration = CompositionConfiguration.Create(catalog);
                string path = Path.GetFullPath(this.ConfigurationOutputPath);
                this.Log.LogMessage("Producing IoC container \"{0}\"", path);
                configuration.SaveAsync(path).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                this.Log.LogError(ex.Message);
            }
            finally
            {
                AppDomain.CurrentDomain.AssemblyResolve -= CurrentDomain_AssemblyResolve;
            }

            return !this.Log.HasLoggedErrors;
        }

        private Assembly CurrentDomain_AssemblyResolve(object sender, ResolveEventArgs args)
        {
            var name = new AssemblyName(args.Name);
            if (name.Name == "System.Composition.AttributedModel")
            {
                string basePath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                string path = Path.Combine(basePath, name.Name + ".dll");
                return Assembly.LoadFile(path);
            }

            return null;
        }
    }
}
