namespace Microsoft.VisualStudio.Composition
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Diagnostics;
    using System.Globalization;
    using System.Linq;
    using System.Reflection;
    using System.Text;
    using System.Threading.Tasks;

    [DebuggerDisplay("{Definition.Type.Name}")]
    public class ComposedPart
    {
        public ComposedPart(ComposablePartDefinition definition, IReadOnlyDictionary<ImportDefinitionBinding, IReadOnlyList<ExportDefinitionBinding>> satisfyingExports, IImmutableSet<string> requiredSharingBoundaries)
        {
            Requires.NotNull(definition, "definition");
            Requires.NotNull(satisfyingExports, "satisfyingExports");
            Requires.NotNull(requiredSharingBoundaries, "requiredSharingBoundaries");

#if DEBUG   // These checks are expensive. Don't do them in production.
            // Make sure we have entries for every import.
            Requires.Argument(satisfyingExports.Count == definition.Imports.Count() && definition.Imports.All(d => satisfyingExports.ContainsKey(d)), "satisfyingExports", "There should be exactly one entry for every import.");
            Requires.Argument(satisfyingExports.All(kv => kv.Value != null), "satisfyingExports", "All values must be non-null.");
#endif

            this.Definition = definition;
            this.SatisfyingExports = satisfyingExports;
            this.RequiredSharingBoundaries = requiredSharingBoundaries;
        }

        public ComposablePartDefinition Definition { get; private set; }

        /// <summary>
        /// Gets a map of this part's imports, and the exports which satisfy them.
        /// </summary>
        public IReadOnlyDictionary<ImportDefinitionBinding, IReadOnlyList<ExportDefinitionBinding>> SatisfyingExports { get; private set; }

        /// <summary>
        /// Gets the set of sharing boundaries that this part must be instantiated within.
        /// </summary>
        public IImmutableSet<string> RequiredSharingBoundaries { get; private set; }

        public IEnumerable<KeyValuePair<ImportDefinitionBinding, IReadOnlyList<ExportDefinitionBinding>>> GetImportingConstructorImports()
        {
            if (this.Definition.ImportingConstructorInfo != null)
            {
                foreach (var import in this.Definition.ImportingConstructorImports)
                {
                    var key = this.SatisfyingExports.Keys.Single(k => k.ImportDefinition == import.ImportDefinition);
                    yield return new KeyValuePair<ImportDefinitionBinding, IReadOnlyList<ExportDefinitionBinding>>(key, this.SatisfyingExports[key]);
                }
            }
        }

        public IEnumerable<ComposedPartDiagnostic> Validate(IReadOnlyDictionary<Type, ExportDefinitionBinding> metadataViews)
        {
            Requires.NotNull(metadataViews, "metadataViews");

            if (this.Definition.ExportDefinitions.Any(ed => CompositionConfiguration.ExportDefinitionPracticallyEqual.Default.Equals(ExportProvider.ExportProviderExportDefinition, ed.Value)) &&
                !this.Definition.Equals(ExportProvider.ExportProviderPartDefinition))
            {
                yield return new ComposedPartDiagnostic(this, "{0}: Export of ExportProvider is not allowed.", this.Definition.Type.FullName);
            }

            var importsWithGenericTypeParameters = this.Definition.Imports
                .Where(import => import.ImportingSiteElementType.ContainsGenericParameters);
            if (importsWithGenericTypeParameters.Any())
            {
                foreach (var import in importsWithGenericTypeParameters)
                {
                    yield return new ComposedPartDiagnostic(
                        this,
                        "{0}: imports that use generic type parameters are not supported.",
                        GetDiagnosticLocation(import));
                }
            }

            foreach (var pair in this.SatisfyingExports)
            {
                var importDefinition = pair.Key.ImportDefinition;
                switch (importDefinition.Cardinality)
                {
                    case ImportCardinality.ExactlyOne:
                        if (pair.Value.Count != 1)
                        {
                            yield return new ComposedPartDiagnostic(
                                this,
                                "{0}: expected exactly 1 export of {1} but found {2}.{3}",
                                GetDiagnosticLocation(pair.Key),
                                pair.Key.ImportingSiteElementType,
                                pair.Value.Count,
                                GetExportsList(pair.Value));
                        }

                        break;
                    case ImportCardinality.OneOrZero:
                        if (pair.Value.Count > 1)
                        {
                            yield return new ComposedPartDiagnostic(
                                this,
                                "{0}: expected 1 or 0 exports but found {1}.{2}",
                                GetDiagnosticLocation(pair.Key),
                                pair.Value.Count,
                                GetExportsList(pair.Value));
                        }

                        break;
                }

                foreach (var export in pair.Value)
                {
                    if (!ReflectionHelpers.IsAssignableTo(pair.Key, export))
                    {
                        yield return new ComposedPartDiagnostic(
                            this,
                            "{0}: is not assignable from exported MEF value {1}.",
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
                                "{0}: cannot import exported value from {1} because the exporting part cannot be instantiated. Is it missing an importing constructor?",
                                GetDiagnosticLocation(pair.Key),
                                GetDiagnosticLocation(export));
                        }
                    }
                }

                if (pair.Key.ImportDefinition.Cardinality == ImportCardinality.ZeroOrMore && pair.Key.ImportingParameter != null && !IsAllowedImportManyParameterType(pair.Key.ImportingParameter.ParameterType))
                {
                    yield return new ComposedPartDiagnostic(
                        this,
                        "Importing constructor has an unsupported parameter type for an [ImportMany]. Only T[] and IEnumerable<T> are supported.");
                }

                if (pair.Key.MetadataType != null && !metadataViews.ContainsKey(pair.Key.MetadataType))
                {
                    yield return new ComposedPartDiagnostic(
                        this,
                        "{0}: metadata type {1} is not supported.",
                        GetDiagnosticLocation(pair.Key),
                        pair.Key.MetadataType.FullName);
                }
            }
        }

        private static string GetDiagnosticLocation(ImportDefinitionBinding import)
        {
            Requires.NotNull(import, "import");

            return string.Format(
                CultureInfo.CurrentCulture,
                "{0}.{1}",
                import.ComposablePartType.FullName,
                import.ImportingMember == null ? ("ctor(" + import.ImportingParameter.Name + ")") : import.ImportingMember.Name);
        }

        private static string GetDiagnosticLocation(ExportDefinitionBinding export)
        {
            Requires.NotNull(export, "export");

            if (export.ExportingMember != null)
            {
                return string.Format(
                    CultureInfo.CurrentCulture,
                    "{0}.{1}",
                    export.PartDefinition.Type.FullName,
                    export.ExportingMember.Name);
            }
            else
            {
                return export.PartDefinition.Type.FullName;
            }
        }

        private static string GetExportsList(IEnumerable<ExportDefinitionBinding> exports)
        {
            Requires.NotNull(exports, "exports");

            return exports.Any()
                ? Environment.NewLine + string.Join(Environment.NewLine, exports.Select(export => "    " + GetDiagnosticLocation(export)))
                : string.Empty;
        }

        private static bool IsAllowedImportManyParameterType(Type importSiteType)
        {
            Requires.NotNull(importSiteType, "importSiteType");
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
