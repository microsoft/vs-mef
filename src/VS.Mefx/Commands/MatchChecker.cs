// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace VS.Mefx.Commands
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using Microsoft;
    using Microsoft.VisualStudio.Composition;

    /// <summary>
    /// Method to perform matching between parts.
    /// </summary>
    internal class MatchChecker : Command
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="MatchChecker"/> class.
        /// </summary>
        /// <param name="derivedInfo">ConfigCreator for the specified input parts.</param>
        /// <param name="arguments">Input arguments specified by the user.</param>
        public MatchChecker(ConfigCreator derivedInfo, CLIOptions arguments)
            : base(derivedInfo, arguments)
        {
        }

        /// <summary>
        /// Method to perform matching on the input options and output the result to the user.
        /// </summary>
        public void PerformMatching()
        {
            if (this.Options.MatchParts == null || this.Options.MatchParts.Count == 0)
            {
                return;
            }

            if (this.Options.MatchParts.Count() == 2)
            {
                string exportPartName = this.Options.MatchParts.ElementAt(0).Trim();
                string importPartName = this.Options.MatchParts.ElementAt(1).Trim();
                string matchPreview = string.Format(
                        CultureInfo.CurrentCulture,
                        Strings.MatchingPartsFormat,
                        exportPartName,
                        importPartName);
                this.Options.Writer.WriteLine(matchPreview);

                // Deal with the case that one of the parts doesn't exist
                ComposablePartDefinition? exportPart = this.Creator.GetPart(exportPartName);
                if (exportPart == null)
                {
                    string invalidExport = string.Format(
                        CultureInfo.CurrentCulture,
                        Strings.MissingPartFormat,
                        exportPartName);
                    this.Options.ErrorWriter.WriteLine(invalidExport);
                    return;
                }

                ComposablePartDefinition? importPart = this.Creator.GetPart(importPartName);
                if (importPart == null)
                {
                    string invalidImport = string.Format(
                        CultureInfo.CurrentCulture,
                        Strings.MissingPartFormat,
                        importPartName);
                    this.Options.ErrorWriter.WriteLine(invalidImport);
                    return;
                }

                // Perform either general matching or specific matching
                if (this.Options.MatchExports == null && this.Options.MatchImports == null)
                {
                    this.CheckGeneralMatch(exportPart, importPart);
                }
                else
                {
                    this.CheckSpecificMatch(exportPart, importPart, this.Options.MatchExports, this.Options.MatchImports);
                }
            }
            else
            {
                this.Options.ErrorWriter.WriteLine(Strings.MatchRequiresTwoMessage);
            }

            this.Options.Writer.WriteLine();
        }

        /// <summary>
        /// Method to get a basic description of a given constraint for output.
        /// </summary>
        /// <param name="constraint">The Constraint which we want information about.</param>
        /// <param name="export">The export we are matching the constraint against.</param>
        /// <returns>A string providing some details about the given constraint.</returns>
        private string GetConstraintString(IImportSatisfiabilityConstraint constraint, PartExport export)
        {
            if (constraint == null || export == null)
            {
                return Strings.NullText;
            }

            string constraintString = constraint.ToString()!;
            string actualValue = export.ToString()!;

            // Try to treat the constraint as an indentity constraint
            if (constraint is ExportTypeIdentityConstraint)
            {
                var identityConstraint = (ExportTypeIdentityConstraint)constraint;
                constraintString = string.Format(
                    CultureInfo.CurrentCulture,
                    Strings.TypeFormat,
                    identityConstraint.TypeIdentityName);
                actualValue = string.Format(
                    CultureInfo.CurrentCulture,
                    Strings.TypeFormat,
                    export.ExportingType);
            }
            else if (constraint is ExportMetadataValueImportConstraint)
            {
                // Try to treat the constraint as an metadata constraint
                var metadataConstraint = (ExportMetadataValueImportConstraint)constraint;
                var exportDetails = export.ExportDetails;
                string keyName = metadataConstraint.Name;
                constraintString = string.Format(
                    CultureInfo.CurrentCulture,
                    Strings.MetadataFormat,
                    keyName,
                    metadataConstraint.Value);
                string pairValue = Strings.MissingKeyText;
                if (exportDetails.Metadata.ContainsKey(keyName))
                {
                    var keyValue = exportDetails.Metadata[keyName];
                    pairValue = (keyValue != null ? keyValue.ToString() : Strings.NullText)!;
                }

                actualValue = string.Format(
                    CultureInfo.CurrentCulture,
                    Strings.MetadataFormat,
                    keyName,
                    pairValue);
            }

            // If it is neither just return the toString text of the two parameters
            return string.Format(
                CultureInfo.CurrentCulture,
                Strings.ExpectedFoundFormat,
                constraintString,
                actualValue);
        }

        /// <summary>
        /// Method to check if a export satifies the import requirements and print that result to the user.
        /// </summary>
        /// <param name="import">The ImportDefinition that we want to check against.</param>
        /// <param name="export">The export we want to compare with.</param>
        /// <returns>
        /// A Match Result object indicating if there was a sucessful matches along with messages to
        /// print out to the user.
        /// </returns>
        private MatchResult CheckDefinitionMatch(ImportDefinition import, PartExport export)
        {
            MatchResult output = new MatchResult();
            var exportDetails = export.ExportDetails;

            // Make sure that the contract name matches
            output.SucessfulMatch = import.ContractName.Equals(exportDetails.ContractName);
            if (!output.SucessfulMatch)
            {
                string contractConstraint = string.Format(
                    CultureInfo.CurrentCulture,
                    Strings.ContractNameFormat,
                    import.ContractName);
                string actualValue = string.Format(
                    CultureInfo.CurrentCulture,
                    Strings.ContractNameFormat,
                    exportDetails.ContractName);
                string contractFailure = string.Format(
                    CultureInfo.CurrentCulture,
                    Strings.ExpectedFoundFormat,
                    contractConstraint,
                    actualValue);
                output.Messages.Add(contractFailure);
                return output;
            }

            // Check all the Import Constraints
            foreach (var constraint in import.ExportConstraints)
            {
                if (!constraint.IsSatisfiedBy(exportDetails))
                {
                    string constraintMessage = this.GetConstraintString(constraint, export);
                    output.Messages.Add(constraintMessage);
                    output.SucessfulMatch = false;
                }
            }

            if (output.SucessfulMatch)
            {
                output.Messages.Add(Strings.ExportMatchingImport);
            }

            return output;
        }

        /// <summary>
        /// Method to output to the user if the given exports satisfy the import requirements.
        /// </summary>
        /// <param name="import">The ImportDefintion we want to match against.</param>
        /// <param name="matchingExports">A list of ExportDefinitions that we want to match against the import.</param>
        private void PerformDefinitionChecking(ImportDefinition import, List<PartExport> matchingExports)
        {
            int total = 0;
            foreach (var export in matchingExports)
            {
                string consideringExport = string.Format(
                    CultureInfo.CurrentCulture,
                    Strings.ConsideringExportFormat,
                    export.ExportingField);
                this.Options.Writer.WriteLine(consideringExport);
                var result = this.CheckDefinitionMatch(import, export);
                if (result.SucessfulMatch)
                {
                    total += 1;
                    this.Options.Writer.WriteLine(result.Messages.First());
                }
                else
                {
                    for (int i = 0; i < result.Messages.Count; i++)
                    {
                        string failedConstraint = string.Format(
                        CultureInfo.CurrentCulture,
                        Strings.FailedConstraintIdentifer,
                        i + 1);
                        this.Options.Writer.WriteLine(failedConstraint);
                        this.Options.Writer.WriteLine(result.Messages.ElementAt(i));
                    }
                }
            }
        }

        /// <summary>
        ///  Method to check if there is a relationship between two given parts
        ///  and print information regarding that match to the user.
        /// </summary>
        /// <param name="exportPart">The definition of part whose exports we want to consider.</param>
        /// <param name="importPart">The definition whose imports we want to consider.</param>
        private void CheckGeneralMatch(ComposablePartDefinition exportPart, ComposablePartDefinition importPart)
        {
            // Get all the exports of the exporting part, indexed by the export contract name
            Dictionary<string, List<PartExport>> allExportDefinitions;
            allExportDefinitions = new Dictionary<string, List<PartExport>>();
            foreach (var export in exportPart.ExportDefinitions)
            {
                var exportDetails = export.Value;
                string exportName = exportDetails.ContractName;
                if (!allExportDefinitions.ContainsKey(exportName))
                {
                    allExportDefinitions.Add(exportName, new List<PartExport>());
                }

                string exportLabel = exportPart.Type.FullName!;
                if (export.Key != null)
                {
                    exportLabel = export.Key.Name;
                }

                allExportDefinitions[exportName].Add(new PartExport(exportDetails, exportLabel));
            }

            bool foundMatch = false;

            // Find imports that have the same contract name as one of the exports and check if they match
            foreach (var import in importPart.Imports)
            {
                var currentImportDefintion = import.ImportDefinition;
                string currentContractName = currentImportDefintion.ContractName;
                if (allExportDefinitions.ContainsKey(currentContractName))
                {
                    this.Options.Writer.WriteLine();
                    string fieldName = importPart.Type.FullName!;
                    if (import.ImportingMember != null)
                    {
                        fieldName = import.ImportingMember.Name;
                    }

                    var potentialMatches = allExportDefinitions[currentContractName];
                    string matchPreview = string.Format(
                        CultureInfo.CurrentCulture,
                        Strings.ConsideringImportFormat,
                        fieldName);
                    this.Options.Writer.WriteLine(matchPreview);
                    foundMatch = true;
                    this.PerformDefinitionChecking(currentImportDefintion, potentialMatches);
                }
            }

            if (!foundMatch)
            {
                this.Options.Writer.WriteLine(Strings.FoundNoMatchesText);
            }
        }

        /// <summary>
        /// Perform matching using the specified fields.
        /// </summary>
        /// <param name="exportPart">The defintion of the part whose exports we want to consider.</param>
        /// <param name="importPart">The definition of the part whose imports we want to consider.</param>
        /// <param name="exportingFields">A list of all the exporting fields we want to consider.</param>
        /// <param name="importingFields">A list of all the importing fields we want to consider.</param>
        private void CheckSpecificMatch(
            ComposablePartDefinition exportPart,
            ComposablePartDefinition importPart,
            List<string>? exportingFields,
            List<string>? importingFields)
        {
            List<PartExport> consideringExports = new List<PartExport>();

            // Find all the exports we want to consider during the matching phase
            foreach (var export in exportPart.ExportDefinitions)
            {
                var exportDetails = export.Value;
                string exportLabel = exportPart.Type.FullName!;
                if (export.Key != null)
                {
                    exportLabel = export.Key.Name;
                }

                bool considerExport = (exportingFields == null) || exportingFields.Contains(exportLabel);
                if (considerExport)
                {
                    consideringExports.Add(new PartExport(exportDetails, exportLabel));
                    if (exportingFields != null)
                    {
                        exportingFields.Remove(exportLabel);
                    }
                }
            }

            // Print message about which exporting fields couldn't be found
            if (exportingFields != null && exportingFields.Count() > 0)
            {
                this.Options.ErrorWriter.WriteLine(Strings.MissingExportMessage);
                exportingFields.ForEach(field => this.Options.ErrorWriter.WriteLine(field));
            }

            consideringExports.Sort((first, second) => first.ExportingField.CompareTo(second.ExportingField));
            List<ImporterStorer> consideringImports = new List<ImporterStorer>();

            // Perform matching against all considering imports
            foreach (var import in importPart.Imports)
            {
                var currentImportDefintion = import.ImportDefinition;
                string importingField = importPart.Type.FullName!;
                if (import.ImportingMember != null)
                {
                    importingField = import.ImportingMember.Name;
                }

                bool performMatching = importingFields == null || importingFields.Contains(importingField);
                if (performMatching)
                {
                    consideringImports.Add(new ImporterStorer(currentImportDefintion, importingField));
                    if (importingFields != null)
                    {
                        importingFields.Remove(importingField);
                    }
                }
            }

            // Print message about which importing fields couldn't be found
            if (importingFields != null && importingFields.Count() > 0)
            {
                this.Options.ErrorWriter.WriteLine(Strings.MissingImportMessage);
                importingFields.ForEach(field => this.Options.ErrorWriter.WriteLine(field));
            }

            // Once we have found all the exports and imports to consider perform matching on them
            consideringImports.Sort((first, second) => first.ImportingField.CompareTo(second.ImportingField));
            foreach (var import in consideringImports)
            {
                string matchPreview = string.Format(
                    CultureInfo.CurrentCulture,
                    Strings.ConsideringImportFormat,
                    import.ImportingField);
                this.Options.Writer.WriteLine("\n" + matchPreview);

                this.PerformDefinitionChecking(import.Import, consideringExports);
            }
        }

        private class ImporterStorer
        {
            public ImporterStorer(ImportDefinition import, string importingField)
            {
                this.Import = import;
                this.ImportingField = importingField;
            }

            public ImportDefinition Import { get; private set; }

            public string ImportingField { get; private set; }
        }

        private class PartExport
        {
            /// <summary>
            /// Name of key to use when getting type of a given export.
            /// </summary>
            private static readonly string TypeKey = "ExportTypeIdentity";

            public PartExport(ExportDefinition details, string field)
            {
                this.ExportDetails = details;
                this.ExportingField = field;
                var exportType = details.ContractName;
                if (details.Metadata.ContainsKey(TypeKey))
                {
                    object value = details.Metadata[TypeKey]!;
                    exportType = value.ToString()!;
                }

                this.ExportingType = exportType;
            }

            public ExportDefinition ExportDetails { get; private set; }

            public string ExportingField { get; private set; }

            public string ExportingType { get; private set; }
        }

        private class MatchResult
        {
            public MatchResult()
            {
                this.SucessfulMatch = true;
                this.Messages = new List<string>();
            }

            public bool SucessfulMatch { get; set; }

            public List<string> Messages { get; set; }
        }
    }
}
