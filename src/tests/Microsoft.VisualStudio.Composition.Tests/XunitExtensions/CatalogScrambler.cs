// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.Composition.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Diagnostics.CodeAnalysis;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.VisualStudio.Composition.Reflection;

    /// <summary>
    /// Forces VS MEF to use slow reflection by simulating a mismatching MVID.
    /// </summary>
    internal class CatalogScrambler
    {
        private readonly Random random = new Random();
        private readonly HashSet<int> scrambedTokens = new HashSet<int>();
        private readonly Resolver resolver;
        private readonly Dictionary<TypeRef, TypeRef> scrambledTypeRefs = new Dictionary<TypeRef, TypeRef>();
        private readonly Dictionary<StrongAssemblyIdentity, StrongAssemblyIdentity> scrambledAssemblyIds = new Dictionary<StrongAssemblyIdentity, StrongAssemblyIdentity>();

        private CatalogScrambler(Resolver resolver)
        {
            Requires.NotNull(resolver, nameof(resolver));
            this.resolver = resolver;
        }

        /// <summary>
        /// Scrambles the MVID and metadata tokens in the catalog to simulate a cache that was
        /// created then invalidated due to an assembly that was recompiled, such that its MVID and metadata tokens changed.
        /// This should effectively force "slow reflection" to be used automatically, and verify metadata tokens are ignored.
        /// </summary>
        /// <param name="catalog">The catalog to scramble.</param>
        /// <returns>The scrambled catalog.</returns>
        internal static ComposableCatalog SimulateRecompiledAssembly(ComposableCatalog catalog)
        {
            Requires.NotNull(catalog, nameof(catalog));

            var scrambler = new CatalogScrambler(TestUtilities.Resolver);
            var scrambledCatalog = ComposableCatalog.Create(scrambler.resolver)
                .AddParts(new DiscoveredParts(catalog.Parts.Select(scrambler.Scramble), catalog.DiscoveredParts.DiscoveryErrors));

            return scrambledCatalog;
        }

        private ComposablePartDefinition Scramble(ComposablePartDefinition part)
        {
            return new ComposablePartDefinition(
                this.Scramble(part.TypeRef),
                part.Metadata,
                part.ExportedTypes,
                part.ExportingMembers.Select(this.Scramble).ToDictionary(kv => kv.Key, kv => kv.Value),
                part.ImportingMembers.Select(this.Scramble).ToList(),
                part.SharingBoundary,
                this.Scramble(part.OnImportsSatisfiedRef),
                this.Scramble(part.ImportingConstructorOrFactoryRef),
                part.ImportingConstructorImports?.Select(this.Scramble).ToList(),
                part.CreationPolicy,
                part.IsSharingBoundaryInferred);
        }

        [return: NotNullIfNotNull("methodRef")]
        private MethodRef? Scramble(MethodRef? methodRef)
        {
            if (methodRef == null)
            {
                return default(MethodRef);
            }

            return new MethodRef(
                this.Scramble(methodRef.DeclaringType),
                this.GetScrambledMetadataToken(methodRef.MetadataToken),
                methodRef.Name,
                methodRef.IsStatic,
                methodRef.ParameterTypes.Select(this.Scramble).ToImmutableArray()!,
                methodRef.GenericMethodArguments.Select(this.Scramble).ToImmutableArray()!);
        }

        private KeyValuePair<MemberRef, IReadOnlyCollection<ExportDefinition>> Scramble(KeyValuePair<MemberRef, IReadOnlyCollection<ExportDefinition>> kv)
        {
            return new KeyValuePair<MemberRef, IReadOnlyCollection<ExportDefinition>>(
                this.Scramble(kv.Key),
                kv.Value);
        }

        [return: NotNullIfNotNull("typeRef")]
        private TypeRef? Scramble(TypeRef? typeRef)
        {
            if (typeRef == null)
            {
                return null;
            }

            if (!this.scrambledTypeRefs.TryGetValue(typeRef, out TypeRef? scrambled))
            {
                scrambled = TypeRef.Get(
                    this.resolver,
                    this.Scramble(typeRef.AssemblyId),
                    typeRef.MetadataToken,
                    typeRef.FullName,
                    typeRef.TypeFlags,
                    typeRef.GenericTypeParameterCount,
                    typeRef.GenericTypeArguments.Select(this.Scramble).ToImmutableArray()!,
                    typeRef.IsShallow,
                    typeRef.IsShallow ? ImmutableArray<TypeRef>.Empty : typeRef.BaseTypes.Select(this.Scramble).ToImmutableArray()!,
                    typeRef.ElementTypeRef);
                this.scrambledTypeRefs.Add(typeRef, scrambled);
            }

            return scrambled;
        }

        [return: NotNullIfNotNull("assemblyId")]
        private StrongAssemblyIdentity? Scramble(StrongAssemblyIdentity assemblyId)
        {
            if (assemblyId == null)
            {
                return null;
            }

            if (!this.scrambledAssemblyIds.TryGetValue(assemblyId, out StrongAssemblyIdentity? scrambled))
            {
                scrambled = new StrongAssemblyIdentity(
                    assemblyId.Name,
                    Guid.NewGuid());

                this.scrambledAssemblyIds.Add(assemblyId, scrambled);
            }

            return scrambled;
        }

        private ImportDefinitionBinding Scramble(ImportDefinitionBinding importDefinitionBinding)
        {
            if (importDefinitionBinding.ImportingMemberRef == null)
            {
                // It's an importing parameter
                return new ImportDefinitionBinding(
                    importDefinitionBinding.ImportDefinition,
                    this.Scramble(importDefinitionBinding.ComposablePartTypeRef),
                    this.Scramble(importDefinitionBinding.ImportingParameterRef!),
                    this.Scramble(importDefinitionBinding.ImportingSiteTypeRef),
                    this.Scramble(importDefinitionBinding.ImportingSiteTypeWithoutCollectionRef));
            }
            else
            {
                return new ImportDefinitionBinding(
                    importDefinitionBinding.ImportDefinition,
                    this.Scramble(importDefinitionBinding.ComposablePartTypeRef),
                    this.Scramble(importDefinitionBinding.ImportingMemberRef),
                    this.Scramble(importDefinitionBinding.ImportingSiteTypeRef),
                    this.Scramble(importDefinitionBinding.ImportingSiteTypeWithoutCollectionRef));
            }
        }

        [return: NotNullIfNotNull("importingParameterRef")]
        private ParameterRef? Scramble(ParameterRef? importingParameterRef)
        {
            if (importingParameterRef == null)
            {
                return default(ParameterRef);
            }

            return new ParameterRef(
                this.Scramble(importingParameterRef.Method),
                importingParameterRef.ParameterIndex);
        }

        [return: NotNullIfNotNull("fieldRef")]
        private FieldRef? Scramble(FieldRef? fieldRef)
        {
            if (fieldRef == null)
            {
                return default(FieldRef);
            }

            return new FieldRef(
                this.Scramble(fieldRef.DeclaringType),
                this.Scramble(fieldRef.FieldTypeRef),
                this.GetScrambledMetadataToken(fieldRef.MetadataToken),
                fieldRef.Name,
                fieldRef.IsStatic);
        }

        [return: NotNullIfNotNull("propertyRef")]
        private PropertyRef? Scramble(PropertyRef? propertyRef)
        {
            if (propertyRef == null)
            {
                return default(PropertyRef);
            }

            return new PropertyRef(
                this.Scramble(propertyRef.DeclaringType),
                this.Scramble(propertyRef.PropertyTypeRef),
                this.GetScrambledMetadataToken(propertyRef.MetadataToken),
                this.GetScrambledMetadataToken(propertyRef.GetMethodMetadataToken),
                this.GetScrambledMetadataToken(propertyRef.SetMethodMetadataToken),
                propertyRef.Name,
                propertyRef.IsStatic);
        }

        [return: NotNullIfNotNull("importingMemberRef")]
        private MemberRef? Scramble(MemberRef? importingMemberRef)
        {
            switch (importingMemberRef)
            {
                case FieldRef fieldRef:
                    return this.Scramble(fieldRef);
                case MethodRef methodRef:
                    return this.Scramble(methodRef);
                case PropertyRef propertyRef:
                    return this.Scramble(propertyRef);
                case null:
                    return null;
                default:
                    throw new NotImplementedException();
            }
        }

        private int GetScrambledMetadataToken(int original)
        {
            if (original == 0)
            {
                return 0;
            }

            // As documented by http://msdn.microsoft.com/en-us/library/ms231937(v=vs.110).aspx
            // We need to keep the same type of metadata token to simulate a defensible change in metadata token.
            const uint typeMask = 0xff000000;
            const uint valueMask = 0x00ffffff;

            // Take care to never return a token used previously to avoid unrealistic scenarios
            // that may cause tests to fail non-deterministically.
            int newToken;
            do
            {
                newToken = (int)((this.random.Next() & valueMask) | (original & typeMask));
            }
            while (!this.scrambedTokens.Add(newToken));

            return newToken;
        }

        private int? GetScrambledMetadataToken(int? original)
        {
            return original.HasValue ? (int?)this.GetScrambledMetadataToken(original.Value) : null;
        }
    }
}
