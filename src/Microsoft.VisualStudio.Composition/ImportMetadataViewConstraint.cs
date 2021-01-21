// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.Composition
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.ComponentModel;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.VisualStudio.Composition.Reflection;

    public class ImportMetadataViewConstraint : IImportSatisfiabilityConstraint, IDescriptiveToString
    {
        private static readonly ImportMetadataViewConstraint EmptyInstance = new ImportMetadataViewConstraint(ImmutableDictionary<string, MetadatumRequirement>.Empty, resolver: null);

        /// <summary>
        /// Initializes a new instance of the <see cref="ImportMetadataViewConstraint"/> class.
        /// </summary>
        /// <param name="metadataNamesAndTypes">The metadata names and requirements.</param>
        /// <param name="resolver">A resolver to use when handling <see cref="TypeRef"/> objects. Must not be null unless <paramref name="metadataNamesAndTypes"/> is empty.</param>
        public ImportMetadataViewConstraint(IReadOnlyDictionary<string, MetadatumRequirement> metadataNamesAndTypes, Resolver? resolver)
        {
            Requires.NotNull(metadataNamesAndTypes, nameof(metadataNamesAndTypes));
            if (metadataNamesAndTypes.Count > 0)
            {
                Requires.NotNull(resolver!, nameof(resolver));
            }

            this.Requirements = ImmutableDictionary.CreateRange(metadataNamesAndTypes);
            this.Resolver = resolver;
        }

        public ImmutableDictionary<string, MetadatumRequirement> Requirements { get; private set; }

        /// <summary>
        /// Gets the <see cref="Composition.Resolver"/> to use.
        /// May be <c>null</c> if <see cref="Requirements"/> is empty.
        /// </summary>
        public Resolver? Resolver { get; }

        /// <summary>
        /// Creates a constraint for the specified metadata type.
        /// </summary>
        /// <param name="metadataTypeRef">The metadata type.</param>
        /// <param name="resolver">The assembly loader.</param>
        /// <returns>A constraint to match the metadata type.</returns>
        public static ImportMetadataViewConstraint GetConstraint(TypeRef metadataTypeRef, Resolver resolver)
        {
            if (metadataTypeRef == null)
            {
                return EmptyInstance;
            }

            var requirements = GetRequiredMetadata(metadataTypeRef, resolver);
            if (requirements.IsEmpty)
            {
                return EmptyInstance;
            }

            return new ImportMetadataViewConstraint(requirements, resolver);
        }

        public bool IsSatisfiedBy(ExportDefinition exportDefinition)
        {
            Requires.NotNull(exportDefinition, nameof(exportDefinition));

            // Fast path since immutable dictionaries are slow to enumerate.
            if (this.Requirements.IsEmpty)
            {
                return true;
            }

            Assumes.NotNull(this.Resolver);
            foreach (var entry in this.Requirements)
            {
                object? value;
                if (!LazyMetadataWrapper.TryGetLoadSafeValueTypeRef(exportDefinition.Metadata, entry.Key, this.Resolver, out value))
                {
                    if (entry.Value.IsMetadataumValueRequired)
                    {
                        return false;
                    }
                    else
                    {
                        // It's not required, and it's not present. No more validation necessary.
                        continue;
                    }
                }

                TypeRef metadatumValueTypeRef = entry.Value.MetadatumValueTypeRef;
                if (value == null)
                {
                    if (metadatumValueTypeRef.IsValueType)
                    {
                        // A null reference for a value type is not a compatible match.
                        return false;
                    }
                    else
                    {
                        // Null is assignable to any reference type.
                        continue;
                    }
                }

                if (value is TypeRef valueTypeRef)
                {
                    if (!metadatumValueTypeRef.ElementTypeRef.IsAssignableFrom(valueTypeRef.ElementTypeRef))
                    {
                        return false;
                    }

                    continue;
                }

                if (value is TypeRef[] valueTypeRefArray && metadatumValueTypeRef.ElementTypeRef != metadatumValueTypeRef)
                {
                    var receivingElementTypeRef = metadatumValueTypeRef.ElementTypeRef;
                    foreach (var item in valueTypeRefArray)
                    {
                        if (item == null)
                        {
                            if (receivingElementTypeRef.IsValueType)
                            {
                                return false;
                            }
                            else
                            {
                                continue;
                            }
                        }

                        if (!receivingElementTypeRef.IsAssignableFrom(item))
                        {
                            return false;
                        }
                    }

                    continue;
                }
            }

            return true;
        }

        public void ToString(TextWriter writer)
        {
            var indentingWriter = IndentingTextWriter.Get(writer);
            foreach (var requirement in this.Requirements)
            {
                indentingWriter.WriteLine("{0} = {1} (required: {2})", requirement.Key, ReflectionHelpers.GetTypeName(requirement.Value.MetadatumValueType, false, true, null, null), requirement.Value.IsMetadataumValueRequired);
            }
        }

        public bool Equals(IImportSatisfiabilityConstraint? obj)
        {
            var other = obj as ImportMetadataViewConstraint;
            if (other == null)
            {
                return false;
            }

            return ByValueEquality.Dictionary<string, MetadatumRequirement>().Equals(this.Requirements, other.Requirements);
        }

        private static ImmutableDictionary<string, MetadatumRequirement> GetRequiredMetadata(TypeRef metadataViewRef, Resolver resolver)
        {
            Requires.NotNull(metadataViewRef, nameof(metadataViewRef));
            Requires.NotNull(resolver, nameof(resolver));

            var metadataView = metadataViewRef.Resolve();
            bool hasMetadataViewImplementation = MetadataViewImplProxy.HasMetadataViewImplementation(metadataView);
            if (metadataView.GetTypeInfo().IsInterface && !metadataView.Equals(typeof(IDictionary<string, object>)) && !metadataView.Equals(typeof(IReadOnlyDictionary<string, object>)))
            {
                var requiredMetadata = ImmutableDictionary.CreateBuilder<string, MetadatumRequirement>();

                foreach (var property in metadataView.EnumProperties().WherePublicInstance())
                {
                    bool required = !property.IsAttributeDefined<DefaultValueAttribute>();

                    // Ignore properties that have a default value and have a metadataview implementation.
                    if (required || !hasMetadataViewImplementation)
                    {
                        requiredMetadata.Add(property.Name, new MetadatumRequirement(TypeRef.Get(ReflectionHelpers.GetMemberType(property), resolver), required));
                    }
                }

                return requiredMetadata.ToImmutable();
            }

            return ImmutableDictionary<string, MetadatumRequirement>.Empty;
        }

        public struct MetadatumRequirement
        {
            public MetadatumRequirement(TypeRef valueType, bool required)
                : this()
            {
                this.MetadatumValueTypeRef = valueType;
                this.IsMetadataumValueRequired = required;
            }

            public TypeRef MetadatumValueTypeRef { get; private set; }

            public Type MetadatumValueType => this.MetadatumValueTypeRef.Resolve();

            public bool IsMetadataumValueRequired { get; private set; }
        }
    }
}