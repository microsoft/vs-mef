namespace Microsoft.VisualStudio.Composition
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Diagnostics;
    using System.Linq;
    using System.Reflection;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Threading.Tasks.Dataflow;
    using Validation;

    public abstract class PartDiscovery
    {
        /// <summary>
        /// Creates an aggregate <see cref="PartDiscovery"/> instance that delegates to a series of other part discovery extensions.
        /// </summary>
        /// <param name="discoveryMechanisms">The discovery extensions to use. In some cases, extensions defined earlier in the list are preferred.</param>
        /// <returns>The aggregate PartDiscovery instance.</returns>
        public static PartDiscovery Combine(params PartDiscovery[] discoveryMechanisms)
        {
            Requires.NotNull(discoveryMechanisms, "discoveryMechanisms");

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
        public abstract ComposablePartDefinition CreatePart(Type partType);

        public Task<IReadOnlyCollection<ComposablePartDefinition>> CreatePartsAsync(params Type[] partTypes)
        {
            return this.CreatePartsAsync(partTypes, CancellationToken.None);
        }

        public async Task<IReadOnlyCollection<ComposablePartDefinition>> CreatePartsAsync(IEnumerable<Type> partTypes, CancellationToken cancellationToken = default(CancellationToken))
        {
            Requires.NotNull(partTypes, "partTypes");

            var tuple = this.CreateDiscoveryBlockChain(cancellationToken);
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
        /// <returns>A set of generated parts.</returns>
        public async Task<IReadOnlyCollection<ComposablePartDefinition>> CreatePartsAsync(Assembly assembly, CancellationToken cancellationToken = default(CancellationToken))
        {
            Requires.NotNull(assembly, "assembly");

            try
            {
                return await this.CreatePartsAsync(this.GetTypes(assembly), cancellationToken);
            }
            catch
            {
                return ImmutableHashSet<ComposablePartDefinition>.Empty;
            }
        }

        public abstract bool IsExportFactoryType(Type type);

        /// <summary>
        /// Reflects over a set of assemblies and produces MEF parts for every applicable type.
        /// </summary>
        /// <param name="assemblies">The assemblies to search for MEF parts.</param>
        /// <returns>A set of generated parts.</returns>
        public async Task<IReadOnlyCollection<ComposablePartDefinition>> CreatePartsAsync(IEnumerable<Assembly> assemblies, CancellationToken cancellationToken = default(CancellationToken))
        {
            Requires.NotNull(assemblies, "assemblies");

            var tuple = this.CreateDiscoveryBlockChain(cancellationToken);
            var assemblyBlock = new TransformManyBlock<Assembly, Type>(
                a => this.GetTypes(a),
                new ExecutionDataflowBlockOptions
                {
                    MaxDegreeOfParallelism = Debugger.IsAttached ? 1 : Environment.ProcessorCount,
                    CancellationToken = cancellationToken,
                });
            assemblyBlock.LinkTo(tuple.Item1, new DataflowLinkOptions { PropagateCompletion = true });

            foreach (var assembly in assemblies)
            {
                await assemblyBlock.SendAsync(assembly);
            }

            assemblyBlock.Complete();
            var parts = await tuple.Item2;
            return parts;
        }

        protected internal static string GetContractName(Type type)
        {
            return ContractNameServices.GetTypeIdentity(type);
        }

        protected internal static Type GetTypeIdentityFromImportingType(Type type, bool importMany)
        {
            Requires.NotNull(type, "type");

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
            Requires.NotNull(type, "type");

            if (type.IsArray)
            {
                return type.GetElementType(); // T[] -> T
            }
            else
            {
                // Discover the ICollection<T> or ICollection<Lazy<T, TMetadata>> interface implemented by this type.
                var icollectionTypes =
                    from iface in ImmutableList.Create(type).AddRange(type.GetTypeInfo().ImplementedInterfaces)
                    let ifaceInfo = iface.GetTypeInfo()
                    where ifaceInfo.IsGenericType
                    let genericTypeDef = ifaceInfo.GetGenericTypeDefinition()
                    where genericTypeDef.Equals(typeof(ICollection<>)) || genericTypeDef.Equals(typeof(IEnumerable<>)) || genericTypeDef.Equals(typeof(IList<>))
                    select ifaceInfo;
                var icollectionType = icollectionTypes.First();
                return icollectionType.GenericTypeArguments[0]; // IEnumerable<T> -> T
            }
        }

        protected static ConstructorInfo GetImportingConstructor(Type type, Type importingConstructorAttributeType, bool publicOnly)
        {
            Requires.NotNull(type, "type");
            Requires.NotNull(importingConstructorAttributeType, "importingConstructorAttributeType");

            var ctors = type.GetTypeInfo().DeclaredConstructors.Where(ctor => !ctor.IsStatic && (ctor.IsPublic || !publicOnly));
            var taggedCtor = ctors.SingleOrDefault(ctor => ctor.GetCustomAttribute(importingConstructorAttributeType) != null);
            var defaultCtor = ctors.SingleOrDefault(ctor => ctor.GetParameters().Length == 0);
            var importingCtor = taggedCtor ?? defaultCtor;
            return importingCtor;
        }

        protected static ImmutableHashSet<IImportSatisfiabilityConstraint> GetMetadataViewConstraints(Type receivingType, bool importMany)
        {
            Requires.NotNull(receivingType, "receivingType");

            var result = ImmutableHashSet.Create<IImportSatisfiabilityConstraint>();

            Type elementType = importMany ? PartDiscovery.GetElementTypeFromMany(receivingType) : receivingType;
            Type metadataType = GetMetadataType(elementType);
            if (metadataType != null)
            {
                result = result.Add(new ImportMetadataViewConstraint(metadataType));
            }

            return result;
        }

        protected internal static ImmutableHashSet<IImportSatisfiabilityConstraint> GetExportTypeIdentityConstraints(Type contractType)
        {
            Requires.NotNull(contractType, "contractType");

            var constraints = ImmutableHashSet<IImportSatisfiabilityConstraint>.Empty;

            if (!contractType.IsEquivalentTo(typeof(object)))
            {
                constraints = constraints.Add(new ExportTypeIdentityConstraint(contractType));
            }

            return constraints;
        }

        protected internal static ImmutableDictionary<string, object> GetImportMetadataForGenericTypeImport(Type contractType)
        {
            Requires.NotNull(contractType, "contractType");
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

        internal static bool IsImportManyCollectionTypeCreateable(ImportDefinitionBinding import)
        {
            Requires.NotNull(import, "import");

            var importDefinition = import.ImportDefinition;
            var collectionType = import.ImportingSiteType;
            var elementType = import.ImportingSiteTypeWithoutCollection;
            var icollectionOfT = typeof(ICollection<>).MakeGenericType(elementType);
            var ienumerableOfT = typeof(IEnumerable<>).MakeGenericType(elementType);
            var ilistOfT = typeof(IList<>).MakeGenericType(elementType);

            if (collectionType.IsArray || collectionType.Equals(ienumerableOfT) || collectionType.Equals(ilistOfT) || collectionType.Equals(icollectionOfT))
            {
                return true;
            }

            Verify.Operation(icollectionOfT.GetTypeInfo().IsAssignableFrom(collectionType.GetTypeInfo()), "Collection type must derive from ICollection<T>");

            var defaultCtor = collectionType.GetTypeInfo().DeclaredConstructors.FirstOrDefault(ctor => ctor.GetParameters().Length == 0);
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
        /// <returns>The metadata view, <see cref="IDictionary{string, object}"/>, or <c>null</c> if there is none.</returns>
        private static Type GetMetadataType(Type receivingType)
        {
            Requires.NotNull(receivingType, "receivingType");

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

        private Tuple<ITargetBlock<Type>, Task<ImmutableHashSet<ComposablePartDefinition>>> CreateDiscoveryBlockChain(CancellationToken cancellationToken)
        {
            var transformBlock = new TransformBlock<Type, ComposablePartDefinition>(
                type =>
                {
                    try
                    {
                        return this.CreatePart(type);
                    }
                    catch
                    {
                        return null;
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
            var aggregatingBlock = new ActionBlock<ComposablePartDefinition>(part => { if (part != null) parts.Add(part); });
            transformBlock.LinkTo(aggregatingBlock, new DataflowLinkOptions { PropagateCompletion = true });

            var tcs = new TaskCompletionSource<ImmutableHashSet<ComposablePartDefinition>>();
            Task.Run(async delegate
            {
                try
                {
                    await aggregatingBlock.Completion;
                    tcs.SetResult(parts.ToImmutable());
                }
                catch (Exception ex)
                {
                    tcs.SetException(ex);
                }
            });

            return Tuple.Create<ITargetBlock<Type>, Task<ImmutableHashSet<ComposablePartDefinition>>>(transformBlock, tcs.Task);
        }

        private class CombinedPartDiscovery : PartDiscovery
        {
            private readonly IReadOnlyList<PartDiscovery> discoveryMechanisms;

            internal CombinedPartDiscovery(IReadOnlyList<PartDiscovery> discoveryMechanisms)
            {
                Requires.NotNull(discoveryMechanisms, "discoveryMechanisms");
                this.discoveryMechanisms = discoveryMechanisms;
            }

            public override ComposablePartDefinition CreatePart(Type partType)
            {
                Requires.NotNull(partType, "partType");

                foreach (var discovery in this.discoveryMechanisms)
                {
                    var result = discovery.CreatePart(partType);
                    if (result != null)
                    {
                        return result;
                    }
                }

                return null;
            }

            public override bool IsExportFactoryType(Type type)
            {
                Requires.NotNull(type, "type");

                return this.discoveryMechanisms.Any(discovery => discovery.IsExportFactoryType(type));
            }

            protected override IEnumerable<Type> GetTypes(Assembly assembly)
            {
                return this.discoveryMechanisms
                    .SelectMany(discovery => discovery.GetTypes(assembly))
                    .Distinct();
            }
        }
    }
}
