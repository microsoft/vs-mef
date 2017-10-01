// Copyright (c) Microsoft. All rights reserved.

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
    using Microsoft.VisualStudio.Composition.Reflection;
    using Task = System.Threading.Tasks.Task;

    public class CompositionConfiguration
    {
        private static readonly ImmutableHashSet<ComposablePartDefinition> AlwaysBundledParts = ImmutableHashSet.Create(
            ExportProvider.ExportProviderPartDefinition,
            PassthroughMetadataViewProvider.PartDefinition,
            MetadataViewClassProvider.PartDefinition,
            ExportMetadataViewInterfaceEmitProxy.PartDefinition)
#if NET45
            .Add(MetadataViewImplProxy.PartDefinition)
#endif
            ;

        private ImmutableDictionary<ComposablePartDefinition, string> effectiveSharingBoundaryOverrides;

        private CompositionConfiguration(ComposableCatalog catalog, ISet<ComposedPart> parts, IReadOnlyDictionary<Type, ExportDefinitionBinding> metadataViewsAndProviders, IImmutableStack<IReadOnlyCollection<ComposedPartDiagnostic>> compositionErrors, ImmutableDictionary<ComposablePartDefinition, string> effectiveSharingBoundaryOverrides)
        {
            Requires.NotNull(catalog, nameof(catalog));
            Requires.NotNull(parts, nameof(parts));
            Requires.NotNull(metadataViewsAndProviders, nameof(metadataViewsAndProviders));
            Requires.NotNull(compositionErrors, nameof(compositionErrors));
            Requires.NotNull(effectiveSharingBoundaryOverrides, nameof(effectiveSharingBoundaryOverrides));

            this.Catalog = catalog;
            this.Parts = parts;
            this.MetadataViewsAndProviders = metadataViewsAndProviders;
            this.CompositionErrors = compositionErrors;
            this.effectiveSharingBoundaryOverrides = effectiveSharingBoundaryOverrides;
        }

        /// <summary>
        /// Gets the catalog that backs this configuration.
        /// This may be a smaller catalog than the one passed in to create this configuration
        /// if invalid parts were removed.
        /// </summary>
        public ComposableCatalog Catalog { get; private set; }

        /// <summary>
        /// Gets the composed parts, with exports satisfied, that make up this configuration.
        /// </summary>
        public ISet<ComposedPart> Parts { get; private set; }

        /// <summary>
        /// Gets a map of metadata views and their matching providers.
        /// </summary>
        public IReadOnlyDictionary<Type, ExportDefinitionBinding> MetadataViewsAndProviders { get; private set; }

        /// <summary>
        /// Gets the compositional errors detected while creating this configuration that led to the removal
        /// of parts from the catalog backing this configuration.
        /// </summary>
        /// <remarks>
        /// The errors are collected as a stack. The topmost stack element represents the first level of errors detected.
        /// As errors are detected and parts removed to achieve a 'stable composition', each cycle of removing parts
        /// and detecting additional errors gets a deeper element in the stack.
        /// Therefore the 'root cause' of all failures is generally found in the topmost stack element.
        /// </remarks>
        public IImmutableStack<IReadOnlyCollection<ComposedPartDiagnostic>> CompositionErrors { get; private set; }

        internal Resolver Resolver => this.Catalog.Resolver;

        public static CompositionConfiguration Create(ComposableCatalog catalog)
        {
            Requires.NotNull(catalog, nameof(catalog));

            // We consider all the parts in the catalog, plus the specially synthesized ones
            // that should always be applied.
            var customizedCatalog = catalog.AddParts(AlwaysBundledParts);

            // Construct our part builders, initialized with all their imports satisfied.
            // We explicitly use reference equality because ComposablePartDefinition.Equals is too slow, and unnecessary for this.
            var partBuilders = new Dictionary<ComposablePartDefinition, PartBuilder>(ReferenceEquality<ComposablePartDefinition>.Default);
            foreach (ComposablePartDefinition partDefinition in customizedCatalog.Parts)
            {
                var satisfyingImports = partDefinition.Imports.ToImmutableDictionary(i => i, i => customizedCatalog.GetExports(i.ImportDefinition));
                partBuilders.Add(partDefinition, new PartBuilder(partDefinition, satisfyingImports));
            }

            // Create a lookup table that gets all immediate importers for each part.
            foreach (PartBuilder partBuilder in partBuilders.Values)
            {
                // We want to understand who imports each part so we can properly propagate sharing boundaries
                // for MEFv1 attributed parts. ExportFactory's that create sharing boundaries are an exception
                // because if a part has a factory that creates new sharing boundaries, the requirement for
                // that sharing boundary of the child scope shouldn't be interpreted as a requirement for that
                // same boundary by the parent part.
                // However, if the ExportFactory does not create sharing boundaries, it does in fact need all
                // the same sharing boundaries as the parts it constructs.
                var importedPartsExcludingFactoriesWithSharingBoundaries =
                    (from entry in partBuilder.SatisfyingExports
                     where !entry.Key.IsExportFactory || entry.Key.ImportDefinition.ExportFactorySharingBoundaries.Count == 0
                     from export in entry.Value
                     select export.PartDefinition).Distinct(ReferenceEquality<ComposablePartDefinition>.Default);
                foreach (var importedPartDefinition in importedPartsExcludingFactoriesWithSharingBoundaries)
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

            // Determine which metadata views to use for each applicable import.
            var metadataViewsAndProviders = GetMetadataViewProvidersMap(customizedCatalog);

            // Validate configuration.
            var errors = new List<ComposedPartDiagnostic>();
            foreach (var part in parts)
            {
                errors.AddRange(part.Validate(metadataViewsAndProviders));
            }

            // Detect loops of all non-shared parts.
            errors.AddRange(FindLoops(parts));

            if (errors.Count > 0)
            {
                var invalidParts = ImmutableHashSet.CreateRange(errors.SelectMany(error => error.Parts).Select(p => p.Definition));
                if (invalidParts.IsEmpty)
                {
                    // If we can't identify the faulty parts but we still have errors, we have to just throw.
                    throw new CompositionFailedException(Strings.FailStableComposition, ImmutableStack.Create<IReadOnlyCollection<ComposedPartDiagnostic>>(errors));
                }

                var salvagedParts = catalog.Parts.Except(invalidParts);
                var salvagedCatalog = ComposableCatalog.Create(catalog.Resolver).AddParts(salvagedParts);
                var configuration = Create(salvagedCatalog);
                return configuration.WithErrors(errors);
            }

            return new CompositionConfiguration(
                catalog,
                parts,
                metadataViewsAndProviders,
                ImmutableStack<IReadOnlyCollection<ComposedPartDiagnostic>>.Empty,
                sharingBoundaryOverrides);
        }

        private static ImmutableDictionary<Type, ExportDefinitionBinding> GetMetadataViewProvidersMap(ComposableCatalog customizedCatalog)
        {
            Requires.NotNull(customizedCatalog, nameof(customizedCatalog));

            var providers = (
                from part in customizedCatalog.Parts
                from export in part.ExportDefinitions
                where export.Value.ContractName == ContractNameServices.GetTypeIdentity(typeof(IMetadataViewProvider))
                orderby ExportProvider.GetOrderMetadata(export.Value.Metadata) descending
                let exportDefinitionBinding = new ExportDefinitionBinding(export.Value, part, default(MemberRef))
                let provider = (IMetadataViewProvider)part.ImportingConstructorOrFactory.Instantiate(Type.EmptyTypes)
                select Tuple.Create(provider, exportDefinitionBinding)).ToList();

            var metadataTypes = new HashSet<Type>(
                from part in customizedCatalog.Parts
                from import in part.Imports
                where import.MetadataType != null
                select import.MetadataType);

            // Make sure that a couple of "primitive" metadata types are included.
            metadataTypes.Add(typeof(IDictionary<string, object>));
            metadataTypes.Add(typeof(IReadOnlyDictionary<string, object>));

            // Find metadata view providers for each metadata type.
            // Don't worry about the ones we can't find. Part validation happens later
            // and they will notice when metadata view providers aren't available and create errors at that time.
            var metadataViewsAndProviders = ImmutableDictionary.CreateBuilder<Type, ExportDefinitionBinding>();
            foreach (var metadataType in metadataTypes)
            {
                var provider = providers.FirstOrDefault(p => p.Item1.IsMetadataViewSupported(metadataType));
                if (provider != null)
                {
                    metadataViewsAndProviders.Add(metadataType, provider.Item2);
                }
            }

            return metadataViewsAndProviders.ToImmutable();
        }

        public IExportProviderFactory CreateExportProviderFactory()
        {
            var composition = RuntimeComposition.CreateRuntimeComposition(this);
            return composition.CreateExportProviderFactory();
        }

        public string GetEffectiveSharingBoundary(ComposablePartDefinition partDefinition)
        {
            Requires.NotNull(partDefinition, nameof(partDefinition));
            Requires.Argument(partDefinition.IsShared, "partDefinition", Strings.PartIsNotShared);

            return this.effectiveSharingBoundaryOverrides.GetValueOrDefault(partDefinition) ?? partDefinition.SharingBoundary;
        }

        /// <summary>
        /// Returns the configuration if it is valid, otherwise throws an exception describing any compositional failures.
        /// </summary>
        /// <returns>This configuration.</returns>
        /// <exception cref="CompositionFailedException">Thrown if <see cref="CompositionErrors"/> is non-empty.</exception>
        /// <remarks>
        /// This method returns <c>this</c> so that it may be used in a 'fluent API' expression.
        /// </remarks>
        public CompositionConfiguration ThrowOnErrors()
        {
            this.Catalog.DiscoveredParts.ThrowOnErrors();

            if (this.CompositionErrors.IsEmpty)
            {
                return this;
            }

            throw new CompositionFailedException(Strings.ErrorsInComposition, this.CompositionErrors);
        }

        internal CompositionConfiguration WithErrors(IReadOnlyCollection<ComposedPartDiagnostic> errors)
        {
            Requires.NotNull(errors, nameof(errors));

            return new CompositionConfiguration(this.Catalog, this.Parts, this.MetadataViewsAndProviders, this.CompositionErrors.Push(errors), this.effectiveSharingBoundaryOverrides);
        }

        /// <summary>
        /// Detects whether a path exists between two nodes.
        /// </summary>
        /// <typeparam name="T">The type of node.</typeparam>
        /// <param name="origin">The node to start the search from.</param>
        /// <param name="target">The node to try to find a path to.</param>
        /// <param name="getDirectLinks">A function that enumerates the allowable steps to take from a given node.</param>
        /// <param name="visited">A reusable collection to use as part of the algorithm to avoid allocations for each call.</param>
        /// <returns>
        /// If a path is found, a non-empty stack describing the path including <paramref name="target"/> (as the deepest element)
        /// and excluding <paramref name="origin"/>.
        /// If a path is not found, an empty stack is returned.
        /// </returns>
        private static ImmutableStack<T> PathExistsBetween<T>(T origin, T target, Func<T, IEnumerable<T>> getDirectLinks, HashSet<T> visited)
        {
            Requires.NotNullAllowStructs(origin, nameof(origin));
            Requires.NotNullAllowStructs(target, nameof(target));
            Requires.NotNull(getDirectLinks, nameof(getDirectLinks));
            Requires.NotNull(visited, nameof(visited));

            if (visited.Add(origin))
            {
                foreach (var directLink in getDirectLinks(origin))
                {
                    if (directLink.Equals(target))
                    {
                        return ImmutableStack.Create(target);
                    }
                    else
                    {
                        var stack = PathExistsBetween(directLink, target, getDirectLinks, visited);
                        if (!stack.IsEmpty)
                        {
                            return stack.Push(directLink);
                        }
                    }
                }
            }

            return ImmutableStack<T>.Empty;
        }

        private static IEnumerable<ComposedPartDiagnostic> FindLoops(IEnumerable<ComposedPart> parts)
        {
            Requires.NotNull(parts, nameof(parts));

            var partByPartDefinition = parts.ToDictionary(p => p.Definition);
            var partByPartType = parts.ToDictionary(p => p.Definition.TypeRef);
            var partsAndDirectImports = new Dictionary<ComposedPart, IReadOnlyList<KeyValuePair<ImportDefinitionBinding, ComposedPart>>>();

            foreach (var part in parts)
            {
                var directlyImportedParts = (from importAndExports in part.SatisfyingExports
                                             from export in importAndExports.Value
                                             let exportingPart = partByPartDefinition[export.PartDefinition]
                                             select new KeyValuePair<ImportDefinitionBinding, ComposedPart>(importAndExports.Key, exportingPart)).ToList();
                partsAndDirectImports.Add(part, directlyImportedParts);
            }

            Func<Func<KeyValuePair<ImportDefinitionBinding, ComposedPart>, bool>, Func<ComposedPart, IEnumerable<ComposedPart>>> getDirectLinksWithFilter =
                filter => (part => partsAndDirectImports[part].Where(filter).Select(ip => ip.Value));
            var visited = new HashSet<ComposedPart>();

            // Find any loops of exclusively non-shared parts.
            var nonSharedPartsInLoops = new HashSet<ComposedPart>();
            foreach (var part in partsAndDirectImports.Keys)
            {
                if (nonSharedPartsInLoops.Contains(part))
                {
                    // Don't check and report parts already detected to be involved in a loop.
                    continue;
                }

                visited.Clear();
                var path = PathExistsBetween(part, part, getDirectLinksWithFilter(ip => !ip.Key.IsExportFactory && (!ip.Value.Definition.IsShared || PartCreationPolicyConstraint.IsNonSharedInstanceRequired(ip.Key.ImportDefinition))), visited);
                if (!path.IsEmpty)
                {
                    path = path.Push(part);
                    nonSharedPartsInLoops.UnionWith(path);
                    yield return new ComposedPartDiagnostic(path, Strings.LoopBetweenNonSharedParts);
                }
            }

            // Find loops even with shared parts where an importing constructor is involved.
            Func<KeyValuePair<ImportDefinitionBinding, ComposedPart>, bool> importingConstructorFilter = ip => !ip.Key.IsExportFactory && !ip.Key.IsLazy;
            foreach (var partAndImports in partsAndDirectImports)
            {
                var importingPart = partAndImports.Key;
                foreach (var import in partAndImports.Value)
                {
                    var importDefinitionBinding = import.Key;
                    var satisfyingPart = import.Value;
                    if (!importDefinitionBinding.ImportingParameterRef.IsEmpty && importingConstructorFilter(import))
                    {
                        visited.Clear();
                        var path = PathExistsBetween(satisfyingPart, importingPart, getDirectLinksWithFilter(importingConstructorFilter), visited);
                        if (!path.IsEmpty)
                        {
                            path = path.Push(satisfyingPart).Push(partByPartType[importDefinitionBinding.ComposablePartTypeRef]);
                            yield return new ComposedPartDiagnostic(path, Strings.LoopInvolvingImportingCtorArgumentAndAllNonLazyImports);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Returns a map of those MEF parts that are missing explicit sharing boundaries, and the sharing boundary that can be inferred.
        /// </summary>
        /// <param name="partBuilders">The part builders to build the map for.</param>
        /// <returns>A map of those parts with inferred boundaries where the key is the part and the value is its designated sharing boundary.</returns>
        private static ImmutableDictionary<ComposablePartDefinition, string> ComputeInferredSharingBoundaries(IEnumerable<PartBuilder> partBuilders)
        {
            Requires.NotNull(partBuilders, nameof(partBuilders));

            var sharingBoundariesAndMetadata = ComputeSharingBoundaryMetadata(partBuilders);

            var sharingBoundaryOverrides = ImmutableDictionary.CreateBuilder<ComposablePartDefinition, string>();
            foreach (PartBuilder partBuilder in partBuilders)
            {
                if (partBuilder.PartDefinition.IsSharingBoundaryInferred)
                {
                    // ALGORITHM selects: the ONE sharing boundary that
                    // * FILTER 1: does not create ANY of the others
                    // * FILTER 2: can reach ALL the others by following UP the sharing boundary export factory chains.
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
                                Strings.UnableToDeterminePrimarySharingBoundary,
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
            Requires.NotNull(partBuilders, nameof(partBuilders));

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
            Requires.NotNull(parts, nameof(parts));

            XElement nodes, links;
            var dgml = Dgml.Create(out nodes, out links, direction: "RightToLeft")
                .WithStyle(
                    "ExportFactory",
                    new Dictionary<string, string>
                    {
                        { "StrokeDashArray", "2,2" },
                    },
                    "Link")
                .WithStyle(
                    "VsMEFBuiltIn",
                    new Dictionary<string, string>
                    {
                        { "Visibility", "Hidden" },
                    });

            foreach (string sharingBoundary in parts.Select(p => p.Definition.SharingBoundary).Distinct())
            {
                if (!string.IsNullOrEmpty(sharingBoundary))
                {
                    nodes.Add(Dgml.Node(sharingBoundary, sharingBoundary, "Expanded"));
                }
            }

            foreach (var part in parts)
            {
                var node = Dgml.Node(part.Definition.Id, ReflectionHelpers.GetTypeName(part.Definition.Type, false, true, null, null));
                if (!string.IsNullOrEmpty(part.Definition.SharingBoundary))
                {
                    node.ContainedBy(part.Definition.SharingBoundary, dgml);
                }

                string[] partDgmlCategories;
                if (part.Definition.Metadata.TryGetValue(CompositionConstants.DgmlCategoryPartMetadataName, out partDgmlCategories))
                {
                    node = node.WithCategories(partDgmlCategories);
                }

                nodes.Add(node);
                foreach (var import in part.SatisfyingExports.Keys)
                {
                    foreach (ExportDefinitionBinding export in part.SatisfyingExports[import])
                    {
                        string linkLabel = !export.ExportedValueTypeRef.Equals(export.PartDefinition.TypeRef)
                            ? export.ExportedValueType.ToString()
                            : null;
                        var link = Dgml.Link(export.PartDefinition.Id, part.Definition.Id, linkLabel);
                        if (import.IsExportFactory)
                        {
                            link = link.WithCategories("ExportFactory");
                        }

                        links.Add(link);
                    }
                }
            }

            return dgml;
        }

        [DebuggerDisplay("{" + nameof(PartDefinition) + "." + nameof(ComposablePartDefinition.Type) + ".Name}")]
        private class PartBuilder
        {
            internal PartBuilder(ComposablePartDefinition partDefinition, IReadOnlyDictionary<ImportDefinitionBinding, IReadOnlyList<ExportDefinitionBinding>> importedParts)
            {
                Requires.NotNull(partDefinition, nameof(partDefinition));
                Requires.NotNull(importedParts, nameof(importedParts));

                this.PartDefinition = partDefinition;
                this.RequiredSharingBoundaries = ImmutableHashSet.CreateBuilder<string>();
                this.SatisfyingExports = importedParts;
                this.ImportingParts = new HashSet<PartBuilder>();
            }

            /// <summary>
            /// Gets or sets the part definition tracked by this instance.
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
                Requires.NotNull(name, nameof(name));
                Requires.NotNull(children, nameof(children));

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
                Requires.NotNull(initialParentBoundaries, nameof(initialParentBoundaries));

                this.ParentBoundariesUnion = initialParentBoundaries.ToImmutableHashSet();
                this.ParentBoundariesIntersection = this.ParentBoundariesUnion;
            }

            private SharingBoundaryMetadata(ImmutableHashSet<string> parentBoundariesUnion, ImmutableHashSet<string> parentBoundariesIntersection)
            {
                Requires.NotNull(parentBoundariesUnion, nameof(parentBoundariesUnion));
                Requires.NotNull(parentBoundariesIntersection, nameof(parentBoundariesIntersection));

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

        internal class ExportDefinitionPracticallyEqual : IEqualityComparer<ExportDefinition>
        {
            internal static ExportDefinitionPracticallyEqual Default = new ExportDefinitionPracticallyEqual();

            private ExportDefinitionPracticallyEqual()
            {
            }

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

        private class ReferenceEquality<T> : IEqualityComparer<T>
            where T : class
        {
            internal static readonly ReferenceEquality<T> Default = new ReferenceEquality<T>();

            private ReferenceEquality()
            {
            }

            public bool Equals(T x, T y)
            {
                return ReferenceEquals(x, y);
            }

            public int GetHashCode(T obj)
            {
                return obj.GetHashCode();
            }
        }
    }
}
