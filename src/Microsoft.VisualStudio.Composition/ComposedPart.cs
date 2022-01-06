// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.Composition
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Diagnostics;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.VisualStudio.Composition.Reflection;

    [DebuggerDisplay("{" + nameof(Definition) + "." + nameof(ComposablePartDefinition.Type) + ".Name}")]
    public class ComposedPart
    {
        private ImmutableDictionary<ImportDefinitionBinding, IReadOnlyList<ExportDefinitionBinding>> satisfyingExports;

        public ComposedPart(ComposablePartDefinition definition, IReadOnlyDictionary<ImportDefinitionBinding, IReadOnlyList<ExportDefinitionBinding>> satisfyingExports, IImmutableSet<string> requiredSharingBoundaries)
        {
            Requires.NotNull(definition, nameof(definition));
            Requires.NotNull(satisfyingExports, nameof(satisfyingExports));
            Requires.NotNull(requiredSharingBoundaries, nameof(requiredSharingBoundaries));

#if DEBUG   // These checks are expensive. Don't do them in production.
            // Make sure we have entries for every import.
            Requires.Argument(satisfyingExports.Count == definition.Imports.Count() && definition.Imports.All(d => satisfyingExports.ContainsKey(d)), "satisfyingExports", Strings.ExactlyOneEntryForEveryImport);
            Requires.Argument(satisfyingExports.All(kv => kv.Value != null), "satisfyingExports", Strings.AllValuesMustBeNonNull);
#endif

            this.Definition = definition;
            this.satisfyingExports = ImmutableDictionary.CreateRange(satisfyingExports);
            this.RequiredSharingBoundaries = requiredSharingBoundaries;
        }

        public ComposablePartDefinition Definition { get; private set; }

        /// <summary>
        /// Gets a map of this part's imports, and the exports which satisfy them.
        /// </summary>
        public IReadOnlyDictionary<ImportDefinitionBinding, IReadOnlyList<ExportDefinitionBinding>> SatisfyingExports => this.satisfyingExports;

        /// <summary>
        /// Gets the set of sharing boundaries that this part must be instantiated within.
        /// </summary>
        public IImmutableSet<string> RequiredSharingBoundaries { get; private set; }

        internal Resolver Resolver => this.Definition.TypeRef.Resolver;

        public IEnumerable<KeyValuePair<ImportDefinitionBinding, IReadOnlyList<ExportDefinitionBinding>>> GetImportingConstructorImports()
        {
            if (this.Definition.ImportingConstructorOrFactoryRef != null)
            {
                Assumes.NotNull(this.Definition.ImportingConstructorImports);
                foreach (var import in this.Definition.ImportingConstructorImports)
                {
                    var key = this.satisfyingExports.Keys.Single(k => k.ImportDefinition == import.ImportDefinition);
                    yield return new KeyValuePair<ImportDefinitionBinding, IReadOnlyList<ExportDefinitionBinding>>(key, this.satisfyingExports[key]);
                }
            }
        }

        public IEnumerable<ComposedPartDiagnostic> Validate(IReadOnlyDictionary<Type, ExportDefinitionBinding> metadataViews)
        {
            Requires.NotNull(metadataViews, nameof(metadataViews));

            if (this.Definition.ExportDefinitions.Any(ed => CompositionConfiguration.ExportDefinitionPracticallyEqual.Default.Equals(ExportProvider.ExportProviderExportDefinition, ed.Value)) &&
                !this.Definition.Equals(ExportProvider.ExportProviderPartDefinition))
            {
                yield return new ComposedPartDiagnostic(this, Strings.ExportOfExportProviderNotAllowed, this.Definition.Type.FullName);
            }

            var importsWithGenericTypeParameters = this.Definition.Imports
                .Where(import => import.ImportingSiteElementTypeRef.GenericTypeParameterCount != 0).ToList();
            foreach (var import in importsWithGenericTypeParameters)
            {
                yield return new ComposedPartDiagnostic(
                    this,
                    Strings.ImportsThatUseGenericTypeParametersNotSupported,
                    GetDiagnosticLocation(import));
            }

            foreach (var pair in this.satisfyingExports)
            {
                var importDefinition = pair.Key.ImportDefinition;
                switch (importDefinition.Cardinality)
                {
                    case ImportCardinality.ExactlyOne:
                        if (pair.Value.Count != 1)
                        {
                            yield return new ComposedPartDiagnostic(
                                this,
                                Strings.ExpectedExactlyOneExportButFound,
                                GetDiagnosticLocation(pair.Key),
                                GetImportConstraints(pair.Key.ImportDefinition),
                                pair.Value.Count,
                                GetExportsList(pair.Value));
                        }

                        break;
                    case ImportCardinality.OneOrZero:
                        if (pair.Value.Count > 1)
                        {
                            yield return new ComposedPartDiagnostic(
                                this,
                                Strings.ExpectedOneOrZeroExportsButFound,
                                GetDiagnosticLocation(pair.Key),
                                GetImportConstraints(pair.Key.ImportDefinition),
                                pair.Value.Count,
                                GetExportsList(pair.Value));
                        }

                        break;
                }

                foreach (var export in pair.Value)
                {
                    if (ReflectionHelpers.IsAssignableTo(pair.Key, export) == ReflectionHelpers.Assignability.DefinitelyNot)
                    {
                        yield return new ComposedPartDiagnostic(
                            this,
                            Strings.IsNotAssignableFromExportedMEFValue,
                            GetDiagnosticLocation(pair.Key),
                            GetDiagnosticLocation(export));
                    }

                    // Some parts exist exclusively for their metadata and the parts themselves are not instantiable.
                    // But that only makes sense if all importers do it lazily. If this part imports one of these
                    // non-instantiable parts in a non-lazy fashion, it's doomed to fail at runtime, so call it a graph error.
                    if (!pair.Key.IsLazy && !export.IsStaticExport && !export.PartDefinition.IsInstantiable)
                    {
                        // Special case around our export provider.
                        if (export.ExportDefinition != ExportProvider.ExportProviderExportDefinition)
                        {
                            yield return new ComposedPartDiagnostic(
                                this,
                                Strings.CannotImportBecauseExportingPartCannotBeInstantiated,
                                GetDiagnosticLocation(pair.Key),
                                GetDiagnosticLocation(export));
                        }
                    }
                }

                if (pair.Key.ImportDefinition.Cardinality == ImportCardinality.ZeroOrMore && pair.Key.ImportingParameterRef != null && !IsAllowedImportManyParameterType(pair.Key.ImportingParameterRef.Resolve().ParameterType))
                {
                    yield return new ComposedPartDiagnostic(this, Strings.ImportingCtorHasUnsupportedParameterTypeForImportMany);
                }

                var metadataType = pair.Key.MetadataType;
                if (metadataType != null && !metadataViews.ContainsKey(metadataType))
                {
                    yield return new ComposedPartDiagnostic(
                        this,
                        Strings.MetadataTypeNotSupported,
                        GetDiagnosticLocation(pair.Key),
                        metadataType.FullName);
                }
            }
        }

        /// <summary>
        /// Removes satisfying exports coming from the given set of parts from this part's imports.
        /// Find and removes any invalidated parts binded with an <see cref="ImportCardinality.ExactlyOne"/> from the <see cref="SatisfyingExports"/> dictionary.
        /// </summary>
        /// <param name="parts">The parts whose exports should not be imported by this part.</param>
        /// <returns>The set of <see cref="ComposedPartDiagnostic"/> with the encountered errors.</returns>
        internal ComposedPartDiagnostic[] RemoveSatisfyingExports(ICollection<ComposablePartDefinition> parts)
        {
            List<ComposedPartDiagnostic>? errorsFound = null;

            foreach (var pair in this.satisfyingExports)
            {
                var importDefinition = pair.Key.ImportDefinition;

                List<ExportDefinitionBinding>? invalidExports = null;

                foreach (var export in pair.Value)
                {
                    // If the part is invalid it should be removed from the satisfying exports.
                    if (parts.Contains(export.PartDefinition))
                    {
                        invalidExports ??= new List<ExportDefinitionBinding>();

                        invalidExports.Add(export);

                        // Only report error if the cardinality is exactly one.
                        // For multiple or optional we just remove the satisfying export.
                        if (importDefinition.Cardinality == ImportCardinality.ExactlyOne)
                        {
                            errorsFound ??= new List<ComposedPartDiagnostic>();

                            errorsFound.Add(new ComposedPartDiagnostic(
                                this,
                                Strings.RequiredImportHasBeenInvalidated,
                                GetDiagnosticLocation(pair.Key),
                                GetImportConstraints(pair.Key.ImportDefinition)));
                        }
                    }
                }

                if (invalidExports is not null)
                {
                    var validExports = this.satisfyingExports[pair.Key].Where(exp => !invalidExports.Contains(exp)).ToList();
                    this.satisfyingExports = this.satisfyingExports.SetItem(pair.Key, validExports);
                }
            }

            return errorsFound is not null ? errorsFound.ToArray() : Array.Empty<ComposedPartDiagnostic>();
        }

        private static string GetImportConstraints(ImportDefinition importDefinition)
        {
            Requires.NotNull(importDefinition, nameof(importDefinition));

            var stringWriter = new StringWriter();
            var indentingWriter = IndentingTextWriter.Get(stringWriter);
            using (indentingWriter.Indent())
            {
                indentingWriter.WriteLine("Contract name: {0}", importDefinition.ContractName);
                foreach (var exportConstraint in importDefinition.ExportConstraints.OfType<IDescriptiveToString>())
                {
                    exportConstraint.ToString(indentingWriter);
                }
            }

            return stringWriter.ToString();
        }

        private static string GetDiagnosticLocation(ImportDefinitionBinding import)
        {
            Requires.NotNull(import, nameof(import));

            var memberName = import.ImportingParameter is object ? ("ctor(" + import.ImportingParameter.Name + ")") :
                             import.ImportingMemberRef is object ? import.ImportingMemberRef.Name :
                             "(unknown)";
            return string.Format(
                CultureInfo.CurrentCulture,
                "{0}.{1}",
                import.ComposablePartType.FullName,
                memberName);
        }

        private static string? GetDiagnosticLocation(ExportDefinitionBinding export)
        {
            Requires.NotNull(export, nameof(export));

            if (export.ExportingMemberRef != null)
            {
                return string.Format(
                    CultureInfo.CurrentCulture,
                    Strings.TypeNameWithAssemblyLocation,
                    export.PartDefinition.Type.FullName,
                    export.ExportingMemberRef.Name,
                    export.PartDefinition.Type.GetTypeInfo().Assembly.FullName);
            }
            else
            {
                return export.PartDefinition.Type.FullName;
            }
        }

        private static string GetExportsList(IEnumerable<ExportDefinitionBinding> exports)
        {
            Requires.NotNull(exports, nameof(exports));

            return exports.Any()
                ? Environment.NewLine + string.Join(Environment.NewLine, exports.Select(export => "    " + GetDiagnosticLocation(export)))
                : string.Empty;
        }

        private static bool IsAllowedImportManyParameterType(Type importSiteType)
        {
            Requires.NotNull(importSiteType, nameof(importSiteType));
            if (importSiteType.IsArray)
            {
                return true;
            }

            if (importSiteType.GetTypeInfo().IsGenericType && importSiteType.GetTypeInfo().GetGenericTypeDefinition().IsEquivalentTo(typeof(IEnumerable<>)))
            {
                return true;
            }

            return false;
        }
    }
}
