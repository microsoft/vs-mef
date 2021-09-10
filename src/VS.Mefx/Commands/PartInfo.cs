namespace VS.Mefx.Commands
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Microsoft.VisualStudio.Composition;

    /// <summary>
    /// Class to list requested information about the specified parts.
    /// </summary>
    internal class PartInfo : Command
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="PartInfo"/> class.
        /// </summary>
        /// <param name="derivedInfo">ConfigCreator for input parts.</param>
        /// <param name="arguments">Input arguments specified by the user.</param>
        public PartInfo(ConfigCreator derivedInfo, CLIOptions arguments)
            : base(derivedInfo, arguments)
        {
        }

        /// <summary>
        /// Method to the read the arguments to the input options and output the requested info
        /// to the user.
        /// </summary>
        public void PrintRequestedInfo()
        {
            // Listing all the parts present in the input files/folders.
            if (this.Options.ListParts)
            {
                Console.WriteLine("Parts in Catalog are ");
                this.ListAllParts();
                Console.WriteLine();
            }

            // Get more detailed information about a specific part.
            if (this.Options.PartDetails != null && this.Options.PartDetails.Count > 0)
            {
                foreach (string partName in this.Options.PartDetails)
                {
                    this.GetPartInfo(partName);
                    Console.WriteLine();
                }
            }

            // Get parts that export a given type
            if (this.Options.ExportDetails != null && this.Options.ExportDetails.Count > 0)
            {
                foreach (string exportType in this.Options.ExportDetails)
                {
                    this.ListTypeExporter(exportType);
                    Console.WriteLine();
                }
            }

            // Get parts that import a given part or type
            if (this.Options.ImportDetails != null && this.Options.ImportDetails.Count > 0)
            {
                foreach (string importType in this.Options.ImportDetails)
                {
                    this.ListTypeImporter(importType);
                    Console.WriteLine();
                }
            }
        }

        /// <summary>
        /// Method to print basic information associated with all the parts in the catalog.
        /// </summary>
        private void ListAllParts()
        {
            var allParts = this.Creator.PartInformation;
            foreach (var partPair in allParts)
            {
                Console.WriteLine(this.GetName(partPair.Value, "[Part]"));
            }
        }

        /// <summary>
        /// Method to present detailed information about the imports/exports of a given part.
        /// </summary>
        /// <param name="partName"> The name of the part we want more information about.</param>
        private void GetPartInfo(string partName)
        {
            Console.WriteLine("Printing out details for part " + partName);
            ComposablePartDefinition definition = this.Creator.GetPart(partName);
            if (definition == null)
            {
                Console.WriteLine("Couldn't find part with name " + partName);
                return;
            }

            // Print details about the exports of the given part
            foreach (var exportPair in definition.ExportDefinitions)
            {
                string exportName = exportPair.Value.ContractName;
                if (exportPair.Key == null)
                {
                    Console.WriteLine("[Export] " + exportName);
                }
                else
                {
                    string exportField = exportPair.Key.Name;
                    Console.WriteLine("[Export] Field: " + exportField + ", Contract Name: " + exportName);
                }
            }

            // Print details about the parts/type the current part imports
            foreach (var import in definition.Imports)
            {
                string importName = import.ImportDefinition.ContractName;
                string importField = "Constructor";
                if (import.ImportingMember != null)
                {
                    importField = import.ImportingMember.Name;
                }

                Console.WriteLine("[Import] Field: " + importField + ", Contract Name: " + importName);
            }
        }

        /// <summary>
        /// Method to get a list of all the parts that contain a export with the given contract name.
        /// </summary>
        /// <param name="contractName">The contract name whose exporting parts we want.</param>
        /// <returns>A list of all the parts that export the given contract name.</returns>
        private List<ComposablePartDefinition> GetContractExporters(string contractName)
        {
            List<ComposablePartDefinition> exportingParts = new List<ComposablePartDefinition>();
            foreach (var partPair in this.Creator.PartInformation)
            {
                var part = partPair.Value;
                foreach (var export in part.ExportDefinitions)
                {
                    if (export.Value.ContractName.Equals(contractName))
                    {
                        exportingParts.Add(part);
                        break;
                    }
                }
            }

            return exportingParts;
        }

        /// <summary>
        /// Method to output all the exporting parts of a given contract name.
        /// </summary>
        /// <param name="contractName">The contract name whose exporters we want.</param>
        private void ListTypeExporter(string contractName)
        {
            var exportingParts = this.GetContractExporters(contractName);
            if (exportingParts.Count() == 0)
            {
                Console.WriteLine("Couldn't find any parts exporting " + contractName);
            }
            else
            {
                Console.WriteLine("Exporting parts for " + contractName + ":");
                foreach (var part in exportingParts)
                {
                    Console.WriteLine(this.GetName(part, "[Part]"));
                }
            }
        }

        /// <summary>
        /// Method to get a list of all the parts that contain a import with the given contract name.
        /// </summary>
        /// <param name="contractName">The contract name whose importing parts we want.</param>
        /// <returns>A list of all the parts that import the given contract name.</returns>
        private List<ComposablePartDefinition> GetContractImporters(string contractName)
        {
            List<ComposablePartDefinition> importingParts = new List<ComposablePartDefinition>();
            foreach (var partPair in this.Creator.PartInformation)
            {
                var part = partPair.Value;
                foreach (var import in part.Imports)
                {
                    if (import.ImportDefinition.ContractName.Equals(contractName))
                    {
                        importingParts.Add(part);
                        break;
                    }
                }
            }

            return importingParts;
        }

        /// <summary>
        /// Method to output all the importing parts of a given contract name.
        /// </summary>
        /// <param name="contractName"> The contract name we want to analyze.</param>
        private void ListTypeImporter(string contractName)
        {
            var importingParts = this.GetContractImporters(contractName);
            if (importingParts.Count() == 0)
            {
                Console.WriteLine("Couldn't find any parts importing " + contractName);
            }
            else
            {
                Console.WriteLine("Importing parts for " + contractName + ":");
                foreach (var part in importingParts)
                {
                    Console.WriteLine(this.GetName(part, "[Part]"));
                }
            }
        }
    }
}
