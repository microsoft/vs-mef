// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.VisualStudio.Composition
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Diagnostics;
    using System.Globalization;
    using System.Linq;
    using System.Reflection;
    using System.Runtime.CompilerServices;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Threading.Tasks.Dataflow;
    using Microsoft.VisualStudio.Composition.Reflection;

    public abstract class PartDiscovery
    {
        protected PartDiscovery(Resolver resolver)
        {
            Requires.NotNull(resolver, nameof(resolver));

            this.Resolver = resolver;
        }

        public Resolver Resolver { get; }

        /// <summary>
        /// Creates an aggregate <see cref="PartDiscovery"/> instance that delegates to a series of other part discovery extensions.
        /// </summary>
        /// <param name="discoveryMechanisms">The discovery extensions to use. In some cases, extensions defined earlier in the list are preferred.</param>
        /// <returns>The aggregate PartDiscovery instance.</returns>
        public static PartDiscovery Combine(params PartDiscovery[] discoveryMechanisms)
        {
            Requires.NotNull(discoveryMechanisms, nameof(discoveryMechanisms));

            if (discoveryMechanisms.Length == 1)
            {
                return discoveryMechanisms[0];
            }

            return new CombinedPartDiscovery(discoveryMechanisms);
        }

        /// <summary>
        /// Reflects on a type and returns metadata on its role as a MEF part, if applicable.
        /// </summary>
        /// <param name="partType">The type to reflect over.</param>
        /// <returns>A new instance of <see cref="ComposablePartDefinition"/> if <paramref name="partType"/>
        /// represents a MEF part; otherwise <c>null</c>.</returns>
        public ComposablePartDefinition CreatePart(Type partType)
        {
            return this.CreatePart(partType, true);
        }

        public Task<DiscoveredParts> CreatePartsAsync(params Type[] partTypes)
        {
            return this.CreatePartsAsync(partTypes, CancellationToken.None);
        }

        public async Task<DiscoveredParts> CreatePartsAsync(IEnumerable<Type> partTypes, CancellationToken cancellationToken = default(CancellationToken))
        {
            Requires.NotNull(partTypes, nameof(partTypes));

            var tuple = this.CreateDiscoveryBlockChain(true, null, cancellationToken);
            foreach (Type type in partTypes)
            {
                await tuple.Item1.SendAsync(type);
            }

            tuple.Item1.Complete();
            var parts = await tuple.Item2;
            return parts;
        }

        /// <summary>
        /// Reflects over an assembly and produces MEF parts for every applicable type.
        /// </summary>
        /// <param name="assembly">The assembly to search for MEF parts.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A set of generated parts.</returns>
        public Task<DiscoveredParts> CreatePartsAsync(Assembly assembly, CancellationToken cancellationToken = default(CancellationToken))
        {
            Requires.NotNull(assembly, nameof(assembly));

            return this.CreatePartsAsync(new[] { assembly }, null, cancellationToken);
        }

        public abstract bool IsExportFactoryType(Type type);

        /// <summary>
        /// Reflects over a set of assemblies and produces MEF parts for every applicable type.
        /// </summary>
        /// <param name="assemblies">The assemblies to search for MEF parts.</param>
        /// <param name="progress">An optional way to receive progress updates on how discovery is progressing.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A set of generated parts.</returns>
        public async Task<DiscoveredParts> CreatePartsAsync(IEnumerable<Assembly> assemblies, IProgress<DiscoveryProgress> progress = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            Requires.NotNull(assemblies, nameof(assemblies));

            var tuple = this.CreateAssemblyDiscoveryBlockChain(progress, cancellationToken);
            foreach (var assembly in assemblies)
            {
                await tuple.Item1.SendAsync(assembly);
            }

            tuple.Item1.Complete();
            var result = await tuple.Item2;
            return result;
        }

        /// <summary>
        /// Reflects over a set of assemblies and produces MEF parts for every applicable type.
        /// </summary>
        /// <param name="assemblyPaths">The paths to assemblies to search for MEF parts.</param>
        /// <param name="progress">An optional way to receive progress updates on how discovery is progressing.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A set of generated parts.</returns>
        public async Task<DiscoveredParts> CreatePartsAsync(IEnumerable<string> assemblyPaths, IProgress<DiscoveryProgress> progress = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            Requires.NotNull(assemblyPaths, nameof(assemblyPaths));

            var exceptions = new List<PartDiscoveryException>();
            var tuple = this.CreateAssemblyDiscoveryBlockChain(progress, cancellationToken);
            var assemblyLoader = new TransformManyBlock<string, Assembly>(
                path =>
                {
                    try
                    {
#if NET45
                        return new Assembly[] { Assembly.Load(AssemblyName.GetAssemblyName(path)) };
#elif NETCOREAPP1_0
                        return new Assembly[] { System.Runtime.Loader.AssemblyLoadContext.Default.LoadFromAssemblyPath(path) };
#else
                        throw new NotSupportedException();
#endif
                    }
                    catch (Exception ex)
                    {
                        lock (exceptions)
                        {
                            exceptions.Add(new PartDiscoveryException(string.Format(CultureInfo.CurrentCulture, Strings.UnableToLoadAssembly, path), ex) { AssemblyPath = path });
                        }

                        return Enumerable.Empty<Assembly>();
                    }
                },
                new ExecutionDataflowBlockOptions
                {
                    CancellationToken = cancellationToken,
                    MaxDegreeOfParallelism = Environment.ProcessorCount,
                });
            assemblyLoader.LinkTo(tuple.Item1, new DataflowLinkOptions { PropagateCompletion = true });
            foreach (var assemblyPath in assemblyPaths)
            {
                await assemblyLoader.SendAsync(assemblyPath);
            }

            assemblyLoader.Complete();
            var result = await tuple.Item2;
            return result.Merge(new DiscoveredParts(Enumerable.Empty<ComposablePartDefinition>(), exceptions));
        }

        internal static void GetAssemblyNamesFromMetadataAttributes<TMetadataAttribute>(MemberInfo member, ISet<AssemblyName> assemblyNames)
            where TMetadataAttribute : class
        {
            Requires.NotNull(member, nameof(member));
            Requires.NotNull(assemblyNames, nameof(assemblyNames));

            foreach (var attribute in member.GetAttributes<Attribute>())
            {
                Type attrType = attribute.GetType();
                if (attrType.GetTypeInfo().IsAttributeDefined<TMetadataAttribute>(inherit: true))
                {
                    assemblyNames.Add(attrType.GetTypeInfo().Assembly.GetName());
                }
            }
        }

        protected internal static string GetContractName(Type type)
        {
            return ContractNameServices.GetTypeIdentity(type);
        }

        protected internal static Type GetTypeIdentityFromImportingType(Type type, bool importMany)
        {
            Requires.NotNull(type, nameof(type));

            if (importMany)
            {
                type = GetElementTypeFromMany(type);
            }

            if (type.IsAnyLazyType() || type.IsExportFactoryTypeV1() || type.IsExportFactoryTypeV2())
            {
                return type.GetTypeInfo().GenericTypeArguments[0];
            }

            return type;
        }

        protected internal static Type GetElementTypeFromMany(Type type)
        {
            Requires.NotNull(type, nameof(type));

            if (type.IsArray)
            {
                return type.GetElementType(); // T[] -> T
            }
            else
            {
                // Discover the ICollection<T> or ICollection<Lazy<T, TMetadata>> interface implemented by this type.
                var icollectionTypes =
                    from iface in new[] { type }.Concat(type.GetTypeInfo().ImplementedInterfaces)
                    let ifaceInfo = iface.GetTypeInfo()
                    where ifaceInfo.IsGenericType
                    let genericTypeDef = ifaceInfo.GetGenericTypeDefinition()
                    where genericTypeDef.Equals(typeof(ICollection<>)) || genericTypeDef.Equals(typeof(IEnumerable<>)) || genericTypeDef.Equals(typeof(IList<>))
                    select ifaceInfo;
                var icollectionType = icollectionTypes.First();
                return icollectionType.GenericTypeArguments[0]; // IEnumerable<T> -> T
            }
        }

        protected static ConstructorInfo GetImportingConstructor<TImportingConstructorAttribute>(Type type, bool publicOnly)
            where TImportingConstructorAttribute : Attribute
        {
            Requires.NotNull(type, nameof(type));

            var ctors = type.GetTypeInfo().DeclaredConstructors.Where(ctor => !ctor.IsStatic && (ctor.IsPublic || !publicOnly));
            var taggedCtor = ctors.SingleOrDefault(ctor => ctor.IsAttributeDefined<TImportingConstructorAttribute>());
            var defaultCtor = ctors.SingleOrDefault(ctor => ctor.GetParameters().Length == 0);
            var importingCtor = taggedCtor ?? defaultCtor;
            return importingCtor;
        }

        protected ImmutableHashSet<IImportSatisfiabilityConstraint> GetMetadataViewConstraints(Type receivingType, bool importMany)
        {
            Requires.NotNull(receivingType, nameof(receivingType));

            var result = ImmutableHashSet.Create<IImportSatisfiabilityConstraint>();

            Type elementType = importMany ? PartDiscovery.GetElementTypeFromMany(receivingType) : receivingType;
            Type metadataType = GetMetadataType(elementType);
            if (metadataType != null)
            {
                result = result.Add(ImportMetadataViewConstraint.GetConstraint(TypeRef.Get(metadataType, this.Resolver), this.Resolver));
            }

            return result;
        }

        protected internal static ImmutableHashSet<IImportSatisfiabilityConstraint> GetExportTypeIdentityConstraints(Type contractType)
        {
            Requires.NotNull(contractType, nameof(contractType));

            var constraints = ImmutableHashSet<IImportSatisfiabilityConstraint>.Empty;

            if (!contractType.IsEquivalentTo(typeof(object)))
            {
                constraints = constraints.Add(new ExportTypeIdentityConstraint(contractType));
            }

            return constraints;
        }

        protected internal static ImmutableDictionary<string, object> GetImportMetadataForGenericTypeImport(Type contractType)
        {
            Requires.NotNull(contractType, nameof(contractType));
            if (contractType.IsConstructedGenericType)
            {
                return ImmutableDictionary.Create<string, object>()
                    .Add(CompositionConstants.GenericContractMetadataName, GetContractName(contractType.GetGenericTypeDefinition()))
                    .Add(CompositionConstants.GenericParametersMetadataName, contractType.GenericTypeArguments);
            }
            else
            {
                return ImmutableDictionary<string, object>.Empty;
            }
        }

        /// <summary>
        /// Creates an array that contains the contents of a prior array (if any) and one additional element.
        /// </summary>
        /// <param name="priorArray">The previous version of the array. May be <c>null</c>. This will not be modified by this method.</param>
        /// <param name="value">The value to add to the array. May be <c>null</c>.</param>
        /// <param name="elementType">The element type for the array, if it is created fresh. May be <c>null</c>.</param>
        /// <returns>A new array.</returns>
        protected static Array AddElement(Array priorArray, object value, Type elementType)
        {
            Type valueType;
            Array newValue;
            if (priorArray != null)
            {
                Type priorArrayElementType = priorArray.GetType().GetElementType();
                valueType = priorArrayElementType == typeof(object) && value != null ? value.GetType() : priorArrayElementType;
                newValue = Array.CreateInstance(valueType, priorArray.Length + 1);
                Array.Copy(priorArray, newValue, priorArray.Length);
            }
            else
            {
                valueType = elementType ?? (value != null ? value.GetType() : typeof(object));
                newValue = Array.CreateInstance(valueType, 1);
            }

            newValue.SetValue(value, newValue.Length - 1);
            return newValue;
        }

        /// <summary>
        /// Gets the types to consider for MEF parts.
        /// </summary>
        /// <param name="assembly">The assembly to read.</param>
        /// <returns>A sequence of types.</returns>
        protected abstract IEnumerable<Type> GetTypes(Assembly assembly);

        /// <summary>
        /// Reflects on a type and returns metadata on its role as a MEF part, if applicable.
        /// </summary>
        /// <param name="partType">The type to reflect over.</param>
        /// <param name="typeExplicitlyRequested">A value indicating whether this type was explicitly requested for inclusion in the catalog.</param>
        /// <returns>A new instance of <see cref="ComposablePartDefinition"/> if <paramref name="partType"/>
        /// represents a MEF part; otherwise <c>null</c>.</returns>
        protected abstract ComposablePartDefinition CreatePart(Type partType, bool typeExplicitlyRequested);

        /// <summary>
        /// Checks whether an import many collection is creatable.
        /// </summary>
        internal static bool IsImportManyCollectionTypeCreateable(ImportDefinitionBinding import)
        {
            Requires.NotNull(import, nameof(import));
            return IsImportManyCollectionTypeCreateable(import.ImportingSiteType, import.ImportingSiteTypeWithoutCollection);
        }

        /// <summary>
        /// Checks whether an import many collection is creatable.
        /// </summary>
        /// <param name="collectionType">The value from ImportingSiteType.</param>
        /// <param name="elementType">The value from ImportingSiteTypeWithoutCollection.</param>
        /// <returns><c>true</c> if the collection is creatable; <c>false</c> otherwise.</returns>
        internal static bool IsImportManyCollectionTypeCreateable(Type collectionType, Type elementType)
        {
            Requires.NotNull(collectionType, nameof(collectionType));
            Requires.NotNull(elementType, nameof(elementType));

            var icollectionOfT = typeof(ICollection<>).MakeGenericType(elementType);
            var ienumerableOfT = typeof(IEnumerable<>).MakeGenericType(elementType);
            var ilistOfT = typeof(IList<>).MakeGenericType(elementType);

            if (collectionType.IsArray || collectionType.Equals(ienumerableOfT) || collectionType.Equals(ilistOfT) || collectionType.Equals(icollectionOfT))
            {
                return true;
            }

            Verify.Operation(icollectionOfT.GetTypeInfo().IsAssignableFrom(collectionType.GetTypeInfo()), Strings.CollectionTypeMustDeriveFromICollectionOfT);

            var defaultCtor = collectionType.GetTypeInfo().DeclaredConstructors.FirstOrDefault(ctor => !ctor.IsStatic && ctor.GetParameters().Length == 0);
            if (defaultCtor != null && defaultCtor.IsPublic)
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// Gets the Type of the interface that serves as a metadata view for a given import.
        /// </summary>
        /// <param name="receivingType">The type of the importing member or parameter, without its ImportMany collection if it had one.</param>
        /// <returns>The metadata view, <see cref="IDictionary{String, Object}"/>, or <c>null</c> if there is none.</returns>
        private static Type GetMetadataType(Type receivingType)
        {
            Requires.NotNull(receivingType, nameof(receivingType));

            if (receivingType.IsAnyLazyType() || receivingType.IsExportFactoryType())
            {
                var args = receivingType.GetTypeInfo().GenericTypeArguments;
                if (args.Length == 2)
                {
                    return args[1];
                }
            }

            return null;
        }

        private Tuple<ITargetBlock<Type>, Task<DiscoveredParts>> CreateDiscoveryBlockChain(bool typeExplicitlyRequested, IProgress<DiscoveryProgress> progress, CancellationToken cancellationToken)
        {
            string status = Strings.ScanningMEFAssemblies;
            int typesScanned = 0;
            var transformBlock = new TransformBlock<Type, object>(
                type =>
                {
                    try
                    {
                        return this.CreatePart(type, typeExplicitlyRequested);
                    }
                    catch (Exception ex)
                    {
                        return new PartDiscoveryException(string.Format(CultureInfo.CurrentCulture, Strings.FailureWhileScanningType, type.FullName), ex) { AssemblyPath = type.GetTypeInfo().Assembly.Location, ScannedType = type };
                    }
                },
                new ExecutionDataflowBlockOptions
                {
                    MaxDegreeOfParallelism = Debugger.IsAttached ? 1 : Environment.ProcessorCount,
                    CancellationToken = cancellationToken,
                    MaxMessagesPerTask = 10,
                    BoundedCapacity = 100,
                });
            var parts = ImmutableHashSet.CreateBuilder<ComposablePartDefinition>();
            var errors = ImmutableList.CreateBuilder<PartDiscoveryException>();
            var aggregatingBlock = new ActionBlock<object>(partOrException =>
            {
                var part = partOrException as ComposablePartDefinition;
                var error = partOrException as PartDiscoveryException;
                Debug.Assert(partOrException is Exception == partOrException is PartDiscoveryException, "Wrong exception type returned.");
                if (part != null)
                {
                    parts.Add(part);
                }
                else if (error != null)
                {
                    errors.Add(error);
                }

                progress.ReportNullSafe(new DiscoveryProgress(++typesScanned, 0, status));
            });
            transformBlock.LinkTo(aggregatingBlock, new DataflowLinkOptions { PropagateCompletion = true });

            var tcs = new TaskCompletionSource<DiscoveredParts>();
            Task.Run(async delegate
            {
                try
                {
                    await aggregatingBlock.Completion;
                    tcs.SetResult(new DiscoveredParts(parts.ToImmutable(), errors.ToImmutable()));
                }
                catch (Exception ex)
                {
                    tcs.SetException(ex);
                }
            });

            return Tuple.Create<ITargetBlock<Type>, Task<DiscoveredParts>>(transformBlock, tcs.Task);
        }

        private Tuple<ITargetBlock<Assembly>, Task<DiscoveredParts>> CreateAssemblyDiscoveryBlockChain(IProgress<DiscoveryProgress> progress, CancellationToken cancellationToken)
        {
            var progressFilter = new ProgressFilter(progress);

            var tuple = this.CreateDiscoveryBlockChain(false, progressFilter, cancellationToken);
            var exceptions = new List<PartDiscoveryException>();
            var assemblyBlock = new TransformManyBlock<Assembly, Type>(
                a =>
                {
                    IReadOnlyCollection<Type> types;
                    try
                    {
                        // Fully realize any enumerable now so that we can catch the exception rather than
                        // leave it to dataflow to catch it.
                        types = this.GetTypes(a).ToList();
                    }
                    catch (ReflectionTypeLoadException ex)
                    {
                        var partDiscoveryException = new PartDiscoveryException(string.Format(CultureInfo.CurrentCulture, Strings.ReflectionTypeLoadExceptionWhileEnumeratingTypes, a.Location), ex) { AssemblyPath = a.Location };
                        lock (exceptions)
                        {
                            exceptions.Add(partDiscoveryException);
                        }

                        types = ex.Types.Where(t => t != null).ToList();
                    }
                    catch (Exception ex)
                    {
                        var partDiscoveryException = new PartDiscoveryException(string.Format(CultureInfo.CurrentCulture, Strings.UnableToEnumerateTypes, a.Location), ex) { AssemblyPath = a.Location };
                        lock (exceptions)
                        {
                            exceptions.Add(partDiscoveryException);
                        }

                        return Enumerable.Empty<Type>();
                    }

                    progressFilter.OnDiscoveredMoreTypes(types.Count);
                    return types;
                },
                new ExecutionDataflowBlockOptions
                {
                    MaxDegreeOfParallelism = Debugger.IsAttached ? 1 : Environment.ProcessorCount,
                    CancellationToken = cancellationToken,
                });
            assemblyBlock.LinkTo(tuple.Item1, new DataflowLinkOptions { PropagateCompletion = true });

            var tcs = new TaskCompletionSource<DiscoveredParts>();
            Task.Run(async delegate
            {
                try
                {
                    var parts = await tuple.Item2;
                    tcs.SetResult(parts.Merge(new DiscoveredParts(Enumerable.Empty<ComposablePartDefinition>(), exceptions)));
                }
                catch (Exception ex)
                {
                    tcs.SetException(ex);
                }
            });

            return Tuple.Create<ITargetBlock<Assembly>, Task<DiscoveredParts>>(assemblyBlock, tcs.Task);
        }

        private class ProgressFilter : IProgress<DiscoveryProgress>
        {
            private readonly IProgress<DiscoveryProgress> upstreamReceiver;

            private int totalTypes;

            private DiscoveryProgress lastReportedProgress;

            internal ProgressFilter(IProgress<DiscoveryProgress> upstreamReceiver)
            {
                this.upstreamReceiver = upstreamReceiver;
            }

            internal void OnDiscoveredMoreTypes(int count)
            {
                Interlocked.Add(ref this.totalTypes, count);
            }

            public void Report(DiscoveryProgress value)
            {
                if (this.upstreamReceiver != null)
                {
                    // Update with the total types we get out of band.
                    value = new DiscoveryProgress(value.CompletedSteps, this.totalTypes, value.Status);

                    bool update = false;
                    lock (this)
                    {
                        // Only report progress if completion or status has changed significantly.
                        if (Math.Abs(value.Completion - this.lastReportedProgress.Completion) > .01 || value.Status != this.lastReportedProgress.Status)
                        {
                            this.lastReportedProgress = value;
                            update = true;
                        }
                    }

                    if (update)
                    {
                        this.upstreamReceiver.Report(this.lastReportedProgress);
                    }
                }
            }
        }

        private class CombinedPartDiscovery : PartDiscovery
        {
            private readonly IReadOnlyList<PartDiscovery> discoveryMechanisms;

            internal CombinedPartDiscovery(IReadOnlyList<PartDiscovery> discoveryMechanisms)
                : base(Resolver.DefaultInstance)
            {
                Requires.NotNull(discoveryMechanisms, nameof(discoveryMechanisms));
                this.discoveryMechanisms = discoveryMechanisms;
            }

            protected override ComposablePartDefinition CreatePart(Type partType, bool typeExplicitlyRequested)
            {
                Requires.NotNull(partType, nameof(partType));

                foreach (var discovery in this.discoveryMechanisms)
                {
                    var result = discovery.CreatePart(partType, typeExplicitlyRequested);
                    if (result != null)
                    {
                        return result;
                    }
                }

                return null;
            }

            public override bool IsExportFactoryType(Type type)
            {
                Requires.NotNull(type, nameof(type));

                return this.discoveryMechanisms.Any(discovery => discovery.IsExportFactoryType(type));
            }

            protected override IEnumerable<Type> GetTypes(Assembly assembly)
            {
                // Don't ask each PartDiscovery component for types
                // because Assembly.GetTypes() is expensive and we don't want to call it multiple times.
                // Also, even if the individual modules returned a filtered set of types,
                // they'll all see the union of types returned from this method anyway,
                // so they have to be prepared for arbitrary types.
                return assembly.GetTypes();
            }
        }
    }
}
