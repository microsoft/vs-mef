// Copyright (c) Microsoft. All rights reserved.

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
        private static readonly ImportMetadataViewConstraint EmptyInstance = new ImportMetadataViewConstraint(ImmutableDictionary<string, MetadatumRequirement>.Empty);

        public ImportMetadataViewConstraint(IReadOnlyDictionary<string, MetadatumRequirement> metadataNamesAndTypes)
        {
            Requires.NotNull(metadataNamesAndTypes, nameof(metadataNamesAndTypes));

            this.Requirements = ImmutableDictionary.CreateRange(metadataNamesAndTypes);
        }

        public ImmutableDictionary<string, MetadatumRequirement> Requirements { get; private set; }

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

            return new ImportMetadataViewConstraint(requirements);
        }

        public bool IsSatisfiedBy(ExportDefinition exportDefinition)
        {
            Requires.NotNull(exportDefinition, nameof(exportDefinition));

            // Fast path since immutable dictionaries are slow to enumerate.
            if (this.Requirements.IsEmpty)
            {
                return true;
            }

            foreach (var entry in this.Requirements)
            {
                object value;
                if (!exportDefinition.Metadata.TryGetValue(entry.Key, out value))
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

                Type metadatumValueType = entry.Value.MetadatumValueType;
                if (value == null)
                {
                    if (metadatumValueType.GetTypeInfo().IsValueType)
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

                if (typeof(object[]).IsEquivalentTo(value.GetType()) && (entry.Value.MetadatumValueTypeRef.IsArray || (metadatumValueType.GetTypeInfo().IsGenericType && typeof(IEnumerable<>).GetTypeInfo().IsAssignableFrom(metadatumValueType.GetTypeInfo().GetGenericTypeDefinition().GetTypeInfo()))))
                {
                    // When ExportMetadata(IsMultiple=true), the value is an object[]. Check that each individual value is assignable.
                    var receivingElementType = PartDiscovery.GetElementTypeFromMany(metadatumValueType).GetTypeInfo();
                    foreach (object item in (object[])value)
                    {
                        if (item == null)
                        {
                            if (receivingElementType.IsValueType)
                            {
                                // We cannot assign null to a struct type.
                                return false;
                            }
                            else
                            {
                                // Null can always be assigned to a reference type.
                                continue;
                            }
                        }

                        if (!receivingElementType.IsAssignableFrom(item.GetType().GetTypeInfo()))
                        {
                            return false;
                        }
                    }

                    // We're fully validated now.
                    continue;
                }

                if (!metadatumValueType.GetTypeInfo().IsAssignableFrom(value.GetType().GetTypeInfo()))
                {
                    return false;
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

        public bool Equals(IImportSatisfiabilityConstraint obj)
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
            if (metadataView.GetTypeInfo().IsInterface && !metadataView.Equals(typeof(IDictionary<string, object>)) && !metadataView.Equals(typeof(IReadOnlyDictionary<string, object>)))
            {
                var requiredMetadata = ImmutableDictionary.CreateBuilder<string, MetadatumRequirement>();

                foreach (var property in metadataView.EnumProperties().WherePublicInstance())
                {
                    bool required = !property.IsAttributeDefined<DefaultValueAttribute>();
                    requiredMetadata.Add(property.Name, new MetadatumRequirement(TypeRef.Get(ReflectionHelpers.GetMemberType(property), resolver), required));
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