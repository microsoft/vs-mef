namespace Microsoft.VisualStudio.Composition
{
    using System;
    using System.CodeDom;
    using System.CodeDom.Compiler;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Diagnostics;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Linq.Expressions;
    using System.Reflection;
    using System.Text;
    using System.Threading.Tasks;
    using System.Xml;
    using System.Xml.Linq;
    using Validation;
    using Task = System.Threading.Tasks.Task;

    public class CompositionConfiguration
    {
        private ImmutableDictionary<ComposablePartDefinition, string> effectiveSharingBoundaryOverrides;

        private CompositionConfiguration(ComposableCatalog catalog, ISet<ComposedPart> parts, ImmutableDictionary<ComposablePartDefinition, string> effectiveSharingBoundaryOverrides)
        {
            Requires.NotNull(catalog, "catalog");
            Requires.NotNull(parts, "parts");
            Requires.NotNull(effectiveSharingBoundaryOverrides, "effectiveSharingBoundaryOverrides");

            this.Catalog = catalog;
            this.Parts = parts;
            this.AdditionalReferenceAssemblies = ImmutableHashSet<Assembly>.Empty;
            this.effectiveSharingBoundaryOverrides = effectiveSharingBoundaryOverrides;
        }

        public ComposableCatalog Catalog { get; private set; }

        public ISet<ComposedPart> Parts { get; private set; }

        public ImmutableHashSet<Assembly> AdditionalReferenceAssemblies { get; private set; }

        public static CompositionConfiguration Create(ComposableCatalog catalog)
        {
            Requires.NotNull(catalog, "catalog");

            ValidateIndividualParts(catalog.Parts);

            // We consider all the parts in the catalog, plus the specially synthesized one
            // so that folks can import the ExportProvider itself.
            catalog = catalog.WithPart(ExportProvider.ExportProviderPartDefinition);

            // Construct our part builders, initialized with all their imports satisfied.
            var partBuilders = new Dictionary<ComposablePartDefinition, PartBuilder>();
            foreach (ComposablePartDefinition partDefinition in catalog.Parts)
            {
                var satisfyingImports = partDefinition.Imports.ToImmutableDictionary(i => i, i => catalog.GetExports(i.ImportDefinition));
                partBuilders.Add(partDefinition, new PartBuilder(partDefinition, satisfyingImports));
            }

            // Create a lookup table that gets all immediate importers for each part.
            foreach (PartBuilder partBuilder in partBuilders.Values)
            {
                var importedPartsExcludingFactories =
                    (from entry in partBuilder.SatisfyingExports
                     where !entry.Key.IsExportFactory
                     from export in entry.Value
                     select export.PartDefinition).Distinct();
                foreach (var importedPartDefinition in importedPartsExcludingFactories)
                {
                    var importedPartBuilder = partBuilders[importedPartDefinition];
                    importedPartBuilder.ReportImportingPart(partBuilder);
                }
            }

            // Propagate sharing boundaries defined on parts to all importers (transitive closure).
            foreach (PartBuilder partBuilder in partBuilders.Values)
            {
                partBuilder.ApplySharingBoundary();
            }

            var sharingBoundaryOverrides = ComputeInferredSharingBoundaries(partBuilders.Values);

            // Build up our set of composed parts.
            var partsBuilder = ImmutableHashSet.CreateBuilder<ComposedPart>();
            foreach (var partBuilder in partBuilders.Values)
            {
                var composedPart = new ComposedPart(partBuilder.PartDefinition, partBuilder.SatisfyingExports, partBuilder.RequiredSharingBoundaries.ToImmutableHashSet());
                partsBuilder.Add(composedPart);
            }

            var parts = partsBuilder.ToImmutable();

            // Validate configuration.
            var exceptions = new List<Exception>();
            foreach (var part in parts)
            {
                try
                {
                    part.Validate();
                }
                catch (Exception ex)
                {
                    exceptions.Add(ex);
                }
            }

            if (exceptions.Count > 0)
            {
                throw new CompositionFailedException("Catalog fails to create a well-formed configuration.", new AggregateException(exceptions));
            }

            // Detect loops of all non-shared parts.
            if (IsLoopPresent(parts))
            {
                Verify.FailOperation("Loop detected.");
            }

            return new CompositionConfiguration(catalog, parts, sharingBoundaryOverrides);
        }

        public static CompositionConfiguration Create(PartDiscovery partDiscovery, params Type[] parts)
        {
            Requires.NotNull(partDiscovery, "partDiscovery");
            Requires.NotNull(parts, "parts");

            return Create(ComposableCatalog.Create(partDiscovery, parts));
        }

        public static IExportProviderFactory Load(AssemblyName assemblyRef)
        {
            return new CompiledExportProviderFactory(Assembly.Load(assemblyRef));
        }

        public static IExportProviderFactory Load(Assembly assembly)
        {
            return new CompiledExportProviderFactory(assembly);
        }

        public CompositionConfiguration WithReferenceAssemblies(ImmutableHashSet<Assembly> additionalReferenceAssemblies)
        {
            Requires.NotNull(additionalReferenceAssemblies, "additionalReferenceAssemblies");

            return new CompositionConfiguration(this.Catalog, this.Parts, this.effectiveSharingBoundaryOverrides)
            {
                AdditionalReferenceAssemblies = this.AdditionalReferenceAssemblies.Union(additionalReferenceAssemblies)
            };
        }

        public string GetEffectiveSharingBoundary(ComposablePartDefinition partDefinition)
        {
            Requires.NotNull(partDefinition, "partDefinition");
            Requires.Argument(partDefinition.IsShared, "partDefinition", "Part is not shared.");

            return this.effectiveSharingBoundaryOverrides.GetValueOrDefault(partDefinition) ?? partDefinition.SharingBoundary;
        }

        private static bool IsLoopPresent(ImmutableHashSet<ComposedPart> parts)
        {
            var partsAndDirectImports = new Dictionary<ComposedPart, ImmutableHashSet<ComposedPart>>();

            // First create a map of each NonShared part and the NonShared parts it directly imports.
            foreach (var part in parts.Where(p => !p.Definition.IsShared))
            {
                var directlyImportedParts = (from exportList in part.SatisfyingExports.Values
                                             from export in exportList
                                             let exportingPart = parts.Single(p => p.Definition == export.PartDefinition)
                                             where !exportingPart.Definition.IsShared
                                             select exportingPart).ToImmutableHashSet();
                partsAndDirectImports.Add(part, directlyImportedParts);
            }

            // Now create a map of each part and all the parts it transitively imports.
            return IsLoopPresent(partsAndDirectImports.Keys, p => partsAndDirectImports[p]);
        }

        private static bool IsLoopPresent<T>(IEnumerable<T> values, Func<T, IEnumerable<T>> getDirectLinks)
        {
            Requires.NotNull(values, "values");
            Requires.NotNull(getDirectLinks, "getDirectLinks");

            var visitedNodes = new HashSet<T>();
            var queue = new Queue<T>();
            foreach (T value in values)
            {
                visitedNodes.Clear();
                queue.Clear();

                queue.Enqueue(value);
                while (queue.Count > 0)
                {
                    var node = queue.Dequeue();
                    if (!visitedNodes.Add(node))
                    {
                        // Only claim to have detected a loop if we got back to the *original* part.
                        // This is because they may be multiple legit routes from the original part
                        // to the part we're looking at now.
                        if (value.Equals(node))
                        {
                            return true;
                        }
                    }

                    foreach (var directLink in getDirectLinks(node).Distinct())
                    {
                        queue.Enqueue(directLink);
                    }
                }
            }

            return false;
        }

        private static void ValidateIndividualParts(IImmutableSet<ComposablePartDefinition> parts)
        {
            Requires.NotNull(parts, "parts");
            var partsExportingExportProvider = parts.Where(p => p.ExportDefinitions.Any(ed => ExportDefinitionPracticallyEqual.Default.Equals(ExportProvider.ExportProviderExportDefinition, ed.Value)));
            if (partsExportingExportProvider.Any())
            {
                throw new CompositionFailedException();
            }
        }

        /// <summary>
        /// Returns a map of those MEF parts that are missing explicit sharing boundaries, and the sharing boundary that can be inferred.
        /// </summary>
        /// <param name="partBuilders">The part builders to build the map for.</param>
        /// <returns>A map of those parts with inferred boundaries where the key is the part and the value is its designated sharing boundary.</returns>
        private static ImmutableDictionary<ComposablePartDefinition, string> ComputeInferredSharingBoundaries(IEnumerable<PartBuilder> partBuilders)
        {
            var sharingBoundariesAndMetadata = ComputeSharingBoundaryMetadata(partBuilders);

            var sharingBoundaryOverrides = ImmutableDictionary.CreateBuilder<ComposablePartDefinition, string>();
            foreach (PartBuilder partBuilder in partBuilders)
            {
                if (partBuilder.PartDefinition.IsSharingBoundaryInferred)
                {
                    // ALGORITHM selects: the ONE sharing boundary that 
                    //  * FILTER 1: does not create ANY of the others
                    //  * FILTER 2: can reach ALL the others by following UP the sharing boundary export factory chains.
                    var filter = from boundary in partBuilder.RequiredSharingBoundaries
                                 let others = partBuilder.RequiredSharingBoundaries.ToImmutableHashSet().Remove(boundary)
                                 where !others.Any(other => sharingBoundariesAndMetadata[other].ParentBoundariesUnion.Contains(boundary)) // filter 1
                                 where others.All(other => sharingBoundariesAndMetadata[boundary].ParentBoundariesIntersection.Contains(other)) // filter 2
                                 select boundary;
                    var qualifyingSharingBoundaries = filter.ToList();

                    if (qualifyingSharingBoundaries.Count == 1)
                    {
                        sharingBoundaryOverrides.Add(partBuilder.PartDefinition, qualifyingSharingBoundaries[0]);
                    }
                    else if (qualifyingSharingBoundaries.Count > 1)
                    {
                        throw new CompositionFailedException(
                            string.Format(
                                CultureInfo.CurrentCulture,
                                "Unable to determine the primary sharing boundary for MEF part \"{0}\".",
                                ReflectionHelpers.GetTypeName(partBuilder.PartDefinition.Type, false, true, null, null)));
                    }
                }
            }

            return sharingBoundaryOverrides.ToImmutable();
        }

        /// <summary>
        /// Constructs a map of all sharing boundaries and information about the boundaries that create them.
        /// </summary>
        /// <param name="partBuilders">A set of part builders.</param>
        /// <returns>A map where the key is the name of a sharing boundary and the value is its metadata.</returns>
        private static ImmutableDictionary<string, SharingBoundaryMetadata> ComputeSharingBoundaryMetadata(IEnumerable<PartBuilder> partBuilders)
        {
            Requires.NotNull(partBuilders, "partBuilders");

            // First build up a dictionary of all sharing boundaries and the parent boundaries that consistently exist.
            var sharingBoundaryExportFactories = from partBuilder in partBuilders
                                                 from import in partBuilder.PartDefinition.Imports
                                                 from sharingBoundary in import.ImportDefinition.ExportFactorySharingBoundaries
                                                 select new { ParentSharingBoundaries = partBuilder.RequiredSharingBoundaries, ChildSharingBoundary = sharingBoundary };
            var childSharingBoundariesAndTheirParents = ImmutableDictionary.CreateBuilder<string, SharingBoundaryMetadata>();
            foreach (var parentChild in sharingBoundaryExportFactories)
            {
                SharingBoundaryMetadata priorMetadata, newMetadata;
                if (childSharingBoundariesAndTheirParents.TryGetValue(parentChild.ChildSharingBoundary, out priorMetadata))
                {
                    newMetadata = priorMetadata.AdditionalFactoryEncountered(parentChild.ParentSharingBoundaries);
                }
                else
                {
                    newMetadata = SharingBoundaryMetadata.InitialFactoryEncountered(parentChild.ParentSharingBoundaries);
                }

                childSharingBoundariesAndTheirParents[parentChild.ChildSharingBoundary] = newMetadata;
            }

            return childSharingBoundariesAndTheirParents.ToImmutable();
        }

        public XDocument CreateDgml()
        {
            return CreateDgml(this.Parts);
        }

        private static XDocument CreateDgml(ISet<ComposedPart> parts)
        {
            Requires.NotNull(parts, "parts");

            XElement nodes, links;
            var dgml = Dgml.Create(out nodes, out links, direction: "RightToLeft");

            foreach (var part in parts)
            {
                nodes.Add(Dgml.Node(part.Definition.Id, ReflectionHelpers.GetTypeName(part.Definition.Type, false, true, null, null)));
                foreach (var import in part.SatisfyingExports.Keys)
                {
                    foreach (ExportDefinitionBinding export in part.SatisfyingExports[import])
                    {
                        string linkLabel = !export.ExportedValueType.Equals(export.PartDefinition.Type)
                            ? export.ExportedValueType.ToString()
                            : null;
                        links.Add(Dgml.Link(export.PartDefinition.Id, part.Definition.Id, linkLabel));
                    }
                }
            }

            return dgml;
        }

        private class CompiledExportProviderFactory : IExportProviderFactory
        {
            private Func<ExportProvider> createFactory;

            internal CompiledExportProviderFactory(Assembly assembly)
            {
                Requires.NotNull(assembly, "assembly");

                var exportFactoryType = assembly.GetType("CompiledExportProvider");
                this.createFactory = () => (ExportProvider)Activator.CreateInstance(exportFactoryType);
            }

            public ExportProvider CreateExportProvider()
            {
                return this.createFactory();
            }
        }

        [DebuggerDisplay("{PartDefinition.Type.Name}")]
        private class PartBuilder
        {
            internal PartBuilder(ComposablePartDefinition partDefinition, IReadOnlyDictionary<ImportDefinitionBinding, IReadOnlyList<ExportDefinitionBinding>> importedParts)
            {
                Requires.NotNull(partDefinition, "partDefinition");
                Requires.NotNull(importedParts, "importedParts");

                this.PartDefinition = partDefinition;
                this.RequiredSharingBoundaries = ImmutableHashSet.CreateBuilder<string>();
                this.SatisfyingExports = importedParts;
                this.ImportingParts = new HashSet<PartBuilder>();
            }

            /// <summary>
            /// Gets the part definition tracked by this instance.
            /// </summary>
            public ComposablePartDefinition PartDefinition { get; set; }

            /// <summary>
            /// Gets the sharing boundaries required to instantiate this part.
            /// </summary>
            /// <remarks>
            /// This is the union of the part's own explicitly declared sharing boundary 
            /// and the boundaries of all parts it imports (transitively).
            /// </remarks>
            public ISet<string> RequiredSharingBoundaries { get; private set; }

            /// <summary>
            /// Gets the set of parts that import this one.
            /// </summary>
            public HashSet<PartBuilder> ImportingParts { get; private set; }

            /// <summary>
            /// Gets the set of parts imported by this one.
            /// </summary>
            public IReadOnlyDictionary<ImportDefinitionBinding, IReadOnlyList<ExportDefinitionBinding>> SatisfyingExports { get; private set; }

            public void ApplySharingBoundary()
            {
                this.ApplySharingBoundary(this.PartDefinition.SharingBoundary);
            }

            private void ApplySharingBoundary(string sharingBoundary)
            {
                if (!string.IsNullOrEmpty(sharingBoundary))
                {
                    if (this.RequiredSharingBoundaries.Add(sharingBoundary))
                    {
                        // Since this is new to us, be sure that all our importers belong to this sharing boundary as well.
                        foreach (var importingPart in this.ImportingParts)
                        {
                            importingPart.ApplySharingBoundary(sharingBoundary);
                        }
                    }
                }
            }

            public void ReportImportingPart(PartBuilder part)
            {
                this.ImportingParts.Add(part);
            }
        }

        [DebuggerDisplay("{Name}")]
        private class SharingBoundaryTree
        {
            public SharingBoundaryTree(string name, ImmutableHashSet<SharingBoundaryTree> children)
            {
                Requires.NotNull(name, "name");
                Requires.NotNull(children, "children");

                this.Name = name;
                this.Children = children;
            }

            public string Name { get; private set; }

            [DebuggerBrowsable(DebuggerBrowsableState.RootHidden)]
            public ImmutableHashSet<SharingBoundaryTree> Children { get; private set; }
        }

        private class SharingBoundaryMetadata
        {
            private SharingBoundaryMetadata(ISet<string> initialParentBoundaries)
            {
                Requires.NotNull(initialParentBoundaries, "initialParentBoundaries");

                this.ParentBoundariesUnion = initialParentBoundaries.ToImmutableHashSet();
                this.ParentBoundariesIntersection = this.ParentBoundariesUnion;
            }

            private SharingBoundaryMetadata(ImmutableHashSet<string> parentBoundariesUnion, ImmutableHashSet<string> parentBoundariesIntersection)
            {
                Requires.NotNull(parentBoundariesUnion, "parentBoundariesUnion");
                Requires.NotNull(parentBoundariesIntersection, "parentBoundariesIntersection");

                this.ParentBoundariesUnion = parentBoundariesUnion;
                this.ParentBoundariesIntersection = parentBoundariesIntersection;
            }

            /// <summary>
            /// Gets the set of parent boundaries that MAY be present at the construction of this sharing boundary.
            /// </summary>
            internal ImmutableHashSet<string> ParentBoundariesUnion { get; private set; }

            /// <summary>
            /// Gets the set of parent boundaries that ARE always present at the construction of this sharing boundary.
            /// </summary>
            internal ImmutableHashSet<string> ParentBoundariesIntersection { get; private set; }

            internal static SharingBoundaryMetadata InitialFactoryEncountered(ISet<string> parentBoundaries)
            {
                return new SharingBoundaryMetadata(parentBoundaries);
            }

            internal SharingBoundaryMetadata AdditionalFactoryEncountered(ISet<string> parentBoundaries)
            {
                return new SharingBoundaryMetadata(
                    this.ParentBoundariesUnion.Union(parentBoundaries),
                    this.ParentBoundariesIntersection.Intersect(parentBoundaries));
            }
        }

        private class ExportDefinitionPracticallyEqual : IEqualityComparer<ExportDefinition>
        {
            private ExportDefinitionPracticallyEqual() { }

            internal static ExportDefinitionPracticallyEqual Default = new ExportDefinitionPracticallyEqual();

            public bool Equals(ExportDefinition x, ExportDefinition y)
            {
                return string.Equals(x.ContractName, y.ContractName, StringComparison.Ordinal)
                    && string.Equals(x.Metadata.GetValueOrDefault(CompositionConstants.ExportTypeIdentityMetadataName) as string, y.Metadata.GetValueOrDefault(CompositionConstants.ExportTypeIdentityMetadataName) as string, StringComparison.Ordinal);
            }

            public int GetHashCode(ExportDefinition obj)
            {
                return obj.ContractName.GetHashCode();
            }
        }
    }
}
