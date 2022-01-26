// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.Composition.VSMefx.Commands
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
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
        internal PartInfo(ConfigCreator derivedInfo, CLIOptions arguments)
            : base(derivedInfo, arguments)
        {
        }

        /// <summary>
        /// Method to the read the arguments to the input options and output the requested info
        /// to the user.
        /// </summary>
        internal void PrintRequestedInfo()
        {
            // Listing all the parts present in the input files/folders.
            if (this.Options.ListParts)
            {
                this.Options.Writer.WriteLine(Strings.PartsDescription);
                this.ListAllParts();
                this.Options.Writer.WriteLine();
            }

            // Get more detailed information about a specific part.
            if (this.Options.PartDetails != null && this.Options.PartDetails.Count > 0)
            {
                foreach (string partName in this.Options.PartDetails)
                {
                    this.GetPartInfo(partName);
                    this.Options.Writer.WriteLine();
                }
            }

            // Get parts that export a given type
            if (this.Options.ExportDetails != null && this.Options.ExportDetails.Count > 0)
            {
                foreach (string exportType in this.Options.ExportDetails)
                {
                    this.ListTypeExporter(exportType);
                    this.Options.Writer.WriteLine();
                }
            }

            // Get parts that import a given part or type
            if (this.Options.ImportDetails != null && this.Options.ImportDetails.Count > 0)
            {
                foreach (string importType in this.Options.ImportDetails)
                {
                    this.ListTypeImporter(importType);
                    this.Options.Writer.WriteLine();
                }
            }
        }

        /// <summary>
        /// Method to print basic information associated with all the parts in the catalog.
        /// </summary>
        private void ListAllParts()
        {
            var allParts = this.Creator.PartInformation;

            if (allParts == null)
            {
                return;
            }

            string[] parts = new string[allParts.Count];
            int index = 0;
            foreach (var partPair in allParts)
            {
                parts[index] = this.GetName(partPair.Value, Strings.VerbosePartLabel);
                index += 1;
            }

            Array.Sort(parts);
            Array.ForEach(parts, part => this.Options.Writer.WriteLine(part));
        }

        /// <summary>
        /// Method to present detailed information about the imports/exports of a given part.
        /// </summary>
        /// <param name="partName"> The name of the part we want more information about.</param>
        private void GetPartInfo(string partName)
        {
            string detailPreview = string.Format(
                        CultureInfo.CurrentCulture,
                        Strings.PartDetailFormat,
                        partName);
            this.Options.Writer.WriteLine(detailPreview);
            ComposablePartDefinition? definition = this.Creator.GetPart(partName);
            if (definition == null)
            {
                string missingPart = string.Format(
                        CultureInfo.CurrentCulture,
                        Strings.MissingPartFormat,
                        partName);
                this.Options.ErrorWriter.WriteLine(missingPart);
                return;
            }

            List<string> exportOutputs = new List<string>();

            // Print details about the exports of the given part
            foreach (var exportPair in definition.ExportDefinitions)
            {
                string exportName = exportPair.Value.ContractName;
                string exportField = partName;
                if (exportPair.Key != null)
                {
                    exportField = exportPair.Key.Name;
                }

                string exportDetail = string.Format(
                        CultureInfo.CurrentCulture,
                        Strings.ExportDetailFormat,
                        exportField,
                        exportName);
                exportOutputs.Add(exportDetail);
            }

            this.WriteLines(exportOutputs);
            List<string> importOutputs = new List<string>();

            foreach (var import in definition.Imports)
            {
                string importName = import.ImportDefinition.ContractName;
                string importField = partName;
                if (import.ImportingMember != null)
                {
                    importField = import.ImportingMember.Name;
                }

                string importDetail = string.Format(
                        CultureInfo.CurrentCulture,
                        Strings.ImportDetailFormat,
                        importField,
                        importName);
                importOutputs.Add(importDetail);
            }

            this.WriteLines(importOutputs);
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
            string exportPreview = string.Format(
                        CultureInfo.CurrentCulture,
                        Strings.ExportingContractsFormat,
                        contractName);
            this.Options.Writer.WriteLine(exportPreview);
            List<string> exportOutputs = new List<string>();
            foreach (var part in exportingParts)
            {
                exportOutputs.Add(this.GetName(part, Strings.VerbosePartLabel));
            }

            this.WriteLines(exportOutputs);
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
            string importPreview = string.Format(
                CultureInfo.CurrentCulture,
                Strings.ImportingContractsFormat,
                contractName);
            this.Options.Writer.WriteLine(importPreview);
            List<string> importOutputs = new List<string>();
            foreach (var part in importingParts)
            {
                importOutputs.Add(this.GetName(part, Strings.VerbosePartLabel));
            }

            this.WriteLines(importOutputs);
        }
    }
}
