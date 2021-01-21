// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.Composition.AppHost
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
        public string BootstrapFile { get; set; } = null!;

        [Required]
        public string CompositionCacheFile { get; set; } = null!;

        [Required]
        public string RootNamespace { get; set; } = null!;

        public override bool Execute()
        {
            string sourceFileContent = this.GetSourceFileTemplate()
                .Replace("$rootnamespace$", this.RootNamespace)
                .Replace("$ConfigurationAssemblyName$", this.CompositionCacheFile);

            File.WriteAllText(
                this.BootstrapFile,
                sourceFileContent);

            return !this.Log.HasLoggedErrors;
        }

        private string GetSourceFileTemplate()
        {
            var assembly = typeof(CreateContainerFactoryBootstrapFile).GetTypeInfo().Assembly;
            using (Stream resourceStream = assembly.GetManifestResourceStream(ThisAssembly.RootNamespace + ".ExportProviderFactory.cs"))
            {
                using (var sr = new StreamReader(resourceStream))
                {
                    return sr.ReadToEnd();
                }
            }
        }
    }
}
