namespace Microsoft.VisualStudio.Composition
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.ComponentModel;
    using System.Linq;
    using System.Reflection;
    using System.Text;
    using System.Threading.Tasks;
    using Validation;

    public class ImportMetadataViewConstraint : IImportSatisfiabilityConstraint
    {
        private static readonly ImportMetadataViewConstraint EmptyInstance = new ImportMetadataViewConstraint(ImmutableDictionary<string, MetadatumRequirement>.Empty);

        private readonly ImmutableDictionary<string, MetadatumRequirement> metadataNamesAndTypes;

        private ImportMetadataViewConstraint(IReadOnlyDictionary<string, MetadatumRequirement> metadataNamesAndTypes)
        {
            Requires.NotNull(metadataNamesAndTypes, "metadataNamesAndTypes");

            this.metadataNamesAndTypes = ImmutableDictionary.CreateRange(metadataNamesAndTypes);
        }

        /// <summary>
        /// Creates a constraint for the specified metadata type.
        /// </summary>
        /// <param name="metadataType">The metadata type.</param>
        /// <returns>A constraint to match the metadata type.</returns>
        public static ImportMetadataViewConstraint GetConstraint(Type metadataType)
        {
            if (metadataType == null)
            {
                return EmptyInstance;
            }

            var requirements = GetRequiredMetadata(metadataType);
            if (requirements.IsEmpty)
            {
                return EmptyInstance;
            }

            return new ImportMetadataViewConstraint(requirements);
        }

        public bool IsSatisfiedBy(ExportDefinition exportDefinition)
        {
            Requires.NotNull(exportDefinition, "exportDefinition");

            // Fast path since immutable dictionaries are slow to enumerate.
            if (this.metadataNamesAndTypes.IsEmpty)
            {
                return true;
            }

            foreach (var entry in this.metadataNamesAndTypes)
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

                if (value == null)
                {
                    if (entry.Value.MetadatumValueType.GetTypeInfo().IsValueType)
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

                if (typeof(object[]).IsEquivalentTo(value.GetType()) && (entry.Value.MetadatumValueType.IsArray || (entry.Value.MetadatumValueType.GetTypeInfo().IsGenericType && typeof(IEnumerable<>).GetTypeInfo().IsAssignableFrom(entry.Value.MetadatumValueType.GetTypeInfo().GetGenericTypeDefinition().GetTypeInfo()))))
                {
                    // When ExportMetadata(IsMultiple=true), the value is an object[]. Check that each individual value is assignable.
                    var receivingElementType = PartDiscovery.GetElementTypeFromMany(entry.Value.MetadatumValueType).GetTypeInfo();
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

                if (!entry.Value.MetadatumValueType.GetTypeInfo().IsAssignableFrom(value.GetType().GetTypeInfo()))
                {
                    return false;
                }
            }

            return true;
        }

        private static ImmutableDictionary<string, MetadatumRequirement> GetRequiredMetadata(Type metadataView)
        {
            Requires.NotNull(metadataView, "metadataView");

            if (metadataView.GetTypeInfo().IsInterface && !metadataView.Equals(typeof(IDictionary<string, object>)) && !metadataView.Equals(typeof(IReadOnlyDictionary<string, object>)))
            {
                var requiredMetadata = ImmutableDictionary.CreateBuilder<string, MetadatumRequirement>();

                foreach (var property in metadataView.EnumProperties().WherePublicInstance())
                {
                    bool required = property.GetCustomAttribute<DefaultValueAttribute>() == null;
                    requiredMetadata.Add(property.Name, new MetadatumRequirement(property.PropertyType, required));
                }

                return requiredMetadata.ToImmutable();
            }

            return ImmutableDictionary<string, MetadatumRequirement>.Empty;
        }

        private struct MetadatumRequirement
        {
            internal MetadatumRequirement(Type valueType, bool required)
                : this()
            {
                this.MetadatumValueType = valueType;
                this.IsMetadataumValueRequired = required;
            }

            public Type MetadatumValueType { get; private set; }

            public bool IsMetadataumValueRequired { get; private set; }
        }
    }
}