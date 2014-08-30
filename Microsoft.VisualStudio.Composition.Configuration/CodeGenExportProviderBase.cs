namespace Microsoft.VisualStudio.Composition
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.ComponentModel;
    using System.Linq;
    using System.Reflection;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.VisualStudio.Composition.Reflection;
    using Validation;

    [EditorBrowsable(EditorBrowsableState.Never)]
    public abstract class CodeGenExportProviderBase : ExportProvider
    {
        private static readonly IAssemblyLoader BuiltInAssemblyLoader = new AssemblyLoaderByFullName();

        /// <summary>
        /// An array initialized by the generated code derived class that contains the value of 
        /// AssemblyName.FullName for each assembly that must be reflected into.
        /// </summary>
        protected string[] assemblyNames;

        /// <summary>
        /// An array initialized by the generated code derived class that contains the value of 
        /// AssemblyName.CodeBasePath for each assembly that must be reflected into.
        /// </summary>
        protected string[] assemblyCodeBasePaths;

        /// <summary>
        /// An array of manifest modules required for access by reflection.
        /// </summary>
        /// <remarks>
        /// This field is initialized to an array of appropriate size by the derived code-gen'd class.
        /// Its elements are individually lazily initialized.
        /// </remarks>
        protected Module[] cachedManifests;

        /// <summary>
        /// An array initialized by the generated code derived class that contains the value of
        /// TypeRef's used within the generated code.
        /// </summary>
        protected TypeRef[] typeRefs;

        private readonly Lazy<IAssemblyLoader> assemblyLoadProvider;

        private readonly ThreadLocal<bool> initializingAssemblyLoader = new ThreadLocal<bool>();

        protected CodeGenExportProviderBase(ExportProvider parent, IReadOnlyCollection<string> freshSharingBoundaries)
            : base(parent, freshSharingBoundaries)
        {
            this.assemblyLoadProvider = new Lazy<IAssemblyLoader>(
                () => ImmutableList.CreateRange(this.GetExports<IAssemblyLoader, IReadOnlyDictionary<string, object>>())
                    .Sort((first, second) => -GetOrderMetadata(first.Metadata).CompareTo(GetOrderMetadata(second.Metadata))).Select(v => v.Value).FirstOrDefault() ?? BuiltInAssemblyLoader);
        }

        /// <summary>
        /// Gets a value that will be translated to System.Type when the metadata value is pulled on by the client.
        /// </summary>
        protected static object GetMetadataValueForType(TypeRef typeRef)
        {
            return new LazyMetadataWrapper.TypeSubstitution(typeRef);
        }

        /// <summary>
        /// Gets a value that will be translated to System.Type[] when the metadata value is pulled on by the client.
        /// </summary>
        protected static object GetMetadataValueForTypeArray(IReadOnlyList<TypeRef> typeRefArray)
        {
            return new LazyMetadataWrapper.TypeArraySubstitution(typeRefArray);
        }

        /// <summary>
        /// Gets the manifest module for an assembly.
        /// </summary>
        /// <param name="assemblyId">The index into the cached manifest array.</param>
        /// <returns>The manifest module.</returns>
        protected Module GetAssemblyManifest(int assemblyId)
        {
            Module result = this.cachedManifests[assemblyId];
            if (result == null)
            {
                // We have to be very careful about getting the assembly loader because it may itself be
                // a MEF component that is in an assembly that must be loaded.
                // So we'll go ahead and try to use the right loader, but if we get re-entered in the meantime,
                // on the same thread, we'll fallback to using our built-in one.
                // The requirement then is that any assembly loader provider must be in an assembly that can be
                // loaded using our built-in one.
                IAssemblyLoader loader;
                if (!this.assemblyLoadProvider.IsValueCreated)
                {
                    if (this.initializingAssemblyLoader.Value)
                    {
                        loader = BuiltInAssemblyLoader;
                    }
                    else
                    {
                        this.initializingAssemblyLoader.Value = true;
                        try
                        {
                            loader = this.assemblyLoadProvider.Value;
                        }
                        finally
                        {
                            this.initializingAssemblyLoader.Value = false;
                        }
                    }
                }
                else
                {
                    loader = this.assemblyLoadProvider.Value;
                }

                Assembly assembly = loader.LoadAssembly(
                    this.assemblyNames[assemblyId],
                    this.assemblyCodeBasePaths[assemblyId]);

                // We don't need to worry about thread-safety here because if two threads assign the
                // reference to the loaded assembly to the array slot, that's just fine.
                result = assembly.ManifestModule;
                this.cachedManifests[assemblyId] = result;
            }

            return result;
        }

        protected IMetadataDictionary GetTypeRefResolvingMetadata(ImmutableDictionary<string, object> metadata)
        {
            Requires.NotNull(metadata, "metadata");
            return new LazyMetadataWrapper(metadata, LazyMetadataWrapper.Direction.ToOriginalValue);
        }

        protected ExportInfo CreateExport(ImportDefinition importDefinition, IReadOnlyDictionary<string, object> metadata, TypeRef partOpenGenericTypeRef, Type valueFactoryMethodDeclaringType, string valueFactoryMethodName, string partSharingBoundary, bool nonSharedInstanceRequired, MemberInfo exportingMember)
        {
            Requires.NotNull(partOpenGenericTypeRef, "partOpenGenericTypeRef");
            Requires.NotNull(importDefinition, "importDefinition");
            Requires.NotNull(metadata, "metadata");

            var typeArgs = (Type[])importDefinition.Metadata[CompositionConstants.GenericParametersMetadataName];
            var valueFactoryOpenGenericMethodInfo = this.GetMethodWithArity(valueFactoryMethodDeclaringType, valueFactoryMethodName, typeArgs.Length);
            var valueFactoryMethodInfo = valueFactoryOpenGenericMethodInfo.MakeGenericMethod(typeArgs);
            var valueFactory = (Func<ExportProvider, Dictionary<TypeRef, object>, bool, object>)valueFactoryMethodInfo.CreateDelegate(typeof(Func<ExportProvider, Dictionary<TypeRef, object>, bool, object>), null);

            Type partOpenGenericType = partOpenGenericTypeRef.Resolve();
            TypeRef partType = partOpenGenericTypeRef.MakeGenericType(typeArgs.Select(TypeRef.Get).ToImmutableArray());

            return this.CreateExport(importDefinition, metadata, partType, valueFactory, partSharingBoundary, nonSharedInstanceRequired, exportingMember);
        }

        protected sealed override IEnumerable<ExportInfo> GetExportsCore(ImportDefinition importDefinition)
        {
            return this.GetExportsCore(importDefinition, PartCreationPolicyConstraint.IsNonSharedInstanceRequired(importDefinition));
        }

        protected abstract IEnumerable<ExportInfo> GetExportsCore(ImportDefinition importDefinition, bool nonSharedInstanceRequired);

        private class AssemblyLoaderByFullName : IAssemblyLoader
        {
            public Assembly LoadAssembly(string assemblyFullName, string codeBasePath)
            {
                // We can't use codeBasePath here because this is a PCL, and the
                // facade assembly we reference doesn't expose AssemblyName.CodeBasePath.
                // That's why the MS.VS.Composition.Configuration.dll has another IAssemblyLoader
                // that we prefer over this one. It does the codebasepath thing.
                return Assembly.Load(new AssemblyName(assemblyFullName));
            }
        }
    }
}
