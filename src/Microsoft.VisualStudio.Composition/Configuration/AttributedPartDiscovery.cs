﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.Composition
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Composition;
    using System.Diagnostics.CodeAnalysis;
    using System.Globalization;
    using System.Linq;
    using System.Reflection;
    using Microsoft.VisualStudio.Composition.Reflection;

    public class AttributedPartDiscovery : PartDiscovery
    {
        public AttributedPartDiscovery(Resolver resolver, bool isNonPublicSupported = false)
            : base(resolver)
        {
            this.IsNonPublicSupported = isNonPublicSupported;
        }

        /// <summary>
        /// Gets a value indicating whether non-public types and members will be explored.
        /// </summary>
        /// <remarks>
        /// The Microsoft.Composition NuGet package ignores non-publics.
        /// </remarks>
        public bool IsNonPublicSupported { get; }

        /// <summary>
        /// Gets the flags that select just public members or public and non-public as appropriate.
        /// </summary>
        protected BindingFlags PublicVsNonPublicFlags
        {
            get
            {
                var baseline = BindingFlags.Public;
                if (this.IsNonPublicSupported)
                {
                    baseline |= BindingFlags.NonPublic;
                }

                return baseline;
            }
        }

        protected override ComposablePartDefinition? CreatePart(Type partType, bool typeExplicitlyRequested)
        {
            Requires.NotNull(partType, nameof(partType));

            var partTypeInfo = partType.GetTypeInfo();
            if (!typeExplicitlyRequested)
            {
                bool isPublic = partType.IsNested ? partTypeInfo.IsNestedPublic : partTypeInfo.IsPublic;
                if (!this.IsNonPublicSupported && !isPublic)
                {
                    // Skip non-public types.
                    return null;
                }
            }

            BindingFlags instanceLocal = BindingFlags.DeclaredOnly | BindingFlags.Instance | this.PublicVsNonPublicFlags;
            var declaredProperties = partTypeInfo.GetProperties(instanceLocal);
            var exportingProperties = from member in declaredProperties
                                      from export in member.GetAttributes<ExportAttribute>()
                                      where member.GetMethod != null // MEFv2 quietly omits exporting properties with no getter
                                      select new KeyValuePair<MemberInfo, ExportAttribute>(member, export);
            var exportedTypes = from export in partTypeInfo.GetAttributes<ExportAttribute>()
                                select new KeyValuePair<MemberInfo, ExportAttribute>(partTypeInfo, export);
            var exportsByMember = (from export in exportingProperties.Concat(exportedTypes)
                                   group export.Value by export.Key into exportsByType
                                   select exportsByType).Select(g => new KeyValuePair<MemberInfo, ExportAttribute[]>(g.Key, g.ToArray())).ToArray();

            if (exportsByMember.Length == 0)
            {
                return null;
            }

            // Check for PartNotDiscoverable only after we've established it's an interesting part.
            // This optimizes for the fact that most types have no exports, in which case it's not a discoverable
            // part anyway. Checking for the PartNotDiscoverableAttribute first, which is rarely defined,
            // doesn't usually pay for itself in terms of short-circuiting. But it does add an extra
            // attribute to look for that we don't need to find for all the types that have no export attributes either.
            if (!typeExplicitlyRequested && partTypeInfo.IsAttributeDefined<PartNotDiscoverableAttribute>())
            {
                return null;
            }

            foreach (var exportingMember in exportsByMember)
            {
                this.ThrowOnInvalidExportingMember(exportingMember.Key);
            }

            TypeRef partTypeRef = TypeRef.Get(partType, this.Resolver);
            Type? partTypeAsGenericTypeDefinition = partTypeInfo.IsGenericType ? partType.GetGenericTypeDefinition() : null;

            string? sharingBoundary = null;
            var sharedAttribute = partTypeInfo.GetFirstAttribute<SharedAttribute>();
            if (sharedAttribute != null)
            {
                sharingBoundary = sharedAttribute.SharingBoundary ?? string.Empty;
            }

            CreationPolicy partCreationPolicy = sharingBoundary != null ? CreationPolicy.Shared : CreationPolicy.NonShared;
            var allExportsMetadata = ImmutableDictionary.CreateRange(PartCreationPolicyConstraint.GetExportMetadata(partCreationPolicy));

            var exportsOnType = ImmutableList.CreateBuilder<ExportDefinition>();
            var exportsOnMembers = ImmutableDictionary.CreateBuilder<MemberRef, IReadOnlyCollection<ExportDefinition>>();

            foreach (var export in exportsByMember)
            {
                var member = export.Key;
                var memberExportMetadata = allExportsMetadata.AddRange(this.GetExportMetadata(member));

                if (member is TypeInfo)
                {
                    foreach (var exportAttribute in export.Value)
                    {
                        Type exportedType = exportAttribute.ContractType ?? partTypeAsGenericTypeDefinition ?? partType;
                        ExportDefinition exportDefinition = CreateExportDefinition(memberExportMetadata, exportAttribute, exportedType);
                        exportsOnType.Add(exportDefinition);
                    }
                }
                else // property
                {
                    var property = (PropertyInfo)member;
                    Verify.Operation(!partTypeInfo.IsGenericTypeDefinition, Strings.ExportsOnMembersNotAllowedWhenDeclaringTypeGeneric);
                    var exportDefinitions = ImmutableList.CreateBuilder<ExportDefinition>();
                    foreach (var exportAttribute in export.Value)
                    {
                        Type exportedType = exportAttribute.ContractType ?? property.PropertyType;
                        ExportDefinition exportDefinition = CreateExportDefinition(memberExportMetadata, exportAttribute, exportedType);
                        exportDefinitions.Add(exportDefinition);
                    }

                    exportsOnMembers.Add(MemberRef.Get(member, this.Resolver), exportDefinitions.ToImmutable());
                }
            }

            var imports = ImmutableList.CreateBuilder<ImportDefinitionBinding>();
            AddImportsFromMembers(declaredProperties, partTypeRef, imports);
            Type? baseType = partTypeInfo.BaseType;
            while (baseType != null && baseType != typeof(object))
            {
                AddImportsFromMembers(baseType.GetProperties(instanceLocal), partTypeRef, imports);
                baseType = baseType.GetTypeInfo().BaseType;
            }

            void AddImportsFromMembers(PropertyInfo[] declaredProperties, TypeRef partTypeRef, IList<ImportDefinitionBinding> imports)
            {
                foreach (var member in declaredProperties)
                {
                    try
                    {
                        var importAttribute = member.GetFirstAttribute<ImportAttribute>();
                        var importManyAttribute = member.GetFirstAttribute<ImportManyAttribute>();
                        Requires.Argument(!(importAttribute != null && importManyAttribute != null), nameof(partType), Strings.MemberContainsBothImportAndImportMany, member.Name);

                        var importConstraints = GetImportConstraints(member);
                        ImportDefinition? importDefinition;
                        if (this.TryCreateImportDefinition(ReflectionHelpers.GetMemberType(member), member, importConstraints, out importDefinition))
                        {
                            var importDefinitionBinding = new ImportDefinitionBinding(
                                importDefinition,
                                partTypeRef,
                                MemberRef.Get(member, this.Resolver),
                                TypeRef.Get(member.PropertyType, this.Resolver),
                                TypeRef.Get(GetImportingSiteTypeWithoutCollection(importDefinition, member.PropertyType), this.Resolver));
                            imports.Add(importDefinitionBinding);
                        }
                    }
                    catch (Exception ex)
                    {
                        throw ThrowErrorScanningMember(member, ex);
                    }
                }
            }

            // MEFv2 is willing to find `internal` OnImportsSatisfied methods, so we should too regardless of our NonPublic flag.
            var onImportsSatisfied = ImmutableList.CreateBuilder<MethodRef>();
            Type? currentType = partTypeInfo;
            while (currentType is object && currentType != typeof(object))
            {
                foreach (var method in currentType.GetMethods(BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
                {
                    try
                    {
                        if (method.IsAttributeDefined<OnImportsSatisfiedAttribute>())
                        {
                            Verify.Operation(method.GetParameters().Length == 0, Strings.OnImportsSatisfiedTakeNoParameters);
                            onImportsSatisfied.Add(MethodRef.Get(method, this.Resolver));
                        }
                    }
                    catch (Exception ex)
                    {
                        throw ThrowErrorScanningMember(method, ex);
                    }
                }

                currentType = currentType.GetTypeInfo().BaseType;
            }

            var importingConstructorParameters = ImmutableList.CreateBuilder<ImportDefinitionBinding>();
            var importingCtor = GetImportingConstructor<ImportingConstructorAttribute>(partType, publicOnly: !this.IsNonPublicSupported);
            Verify.Operation(importingCtor != null, Strings.NoImportingConstructorFound);
            foreach (var parameter in importingCtor.GetParameters())
            {
                var import = this.CreateImport(parameter, GetImportConstraints(parameter));
                if (import.ImportDefinition.Cardinality == ImportCardinality.ZeroOrMore)
                {
                    Verify.Operation(PartDiscovery.IsImportManyCollectionTypeCreateable(import), Strings.CollectionMustBePublicAndPublicCtorWhenUsingImportingCtor);
                }

                importingConstructorParameters.Add(import);
            }

            var partMetadata = ImmutableDictionary.CreateBuilder<string, object?>();
            foreach (var partMetadataAttribute in partTypeInfo.GetAttributes<PartMetadataAttribute>())
            {
                partMetadata[partMetadataAttribute.Name] = partMetadataAttribute.Value;
            }

            var assemblyNamesForMetadataAttributes = ImmutableHashSet.CreateBuilder<AssemblyName>(ByValueEquality.AssemblyName);
            foreach (var export in exportsByMember)
            {
                GetAssemblyNamesFromMetadataAttributes<MetadataAttributeAttribute>(export.Key, assemblyNamesForMetadataAttributes);
            }

            return new ComposablePartDefinition(
                TypeRef.Get(partType, this.Resolver),
                partMetadata.ToImmutable(),
                exportsOnType.ToImmutable(),
                exportsOnMembers.ToImmutable(),
                imports.ToImmutable(),
                sharingBoundary,
                onImportsSatisfied.ToImmutable(),
                MethodRef.Get(importingCtor, this.Resolver),
                importingConstructorParameters.ToImmutable(),
                partCreationPolicy,
                isSharingBoundaryInferred: false,
                extraInputAssemblies: assemblyNamesForMetadataAttributes);

            static Exception ThrowErrorScanningMember(MemberInfo member, Exception ex) => throw new PartDiscoveryException(Strings.FormatErrorWhileScanningMember(member.Name), ex);
        }

        public override bool IsExportFactoryType(Type type)
        {
            if (type != null && type.GetTypeInfo().IsGenericType)
            {
                var typeDefinition = type.GetGenericTypeDefinition();
                if (typeDefinition.Equals(typeof(ExportFactory<>)) || typeDefinition.Equals(typeof(ExportFactory<,>)))
                {
                    return true;
                }
            }

            return false;
        }

        protected override IEnumerable<Type> GetTypes(Assembly assembly)
        {
            Requires.NotNull(assembly, nameof(assembly));

            return this.IsNonPublicSupported ? assembly.GetTypes() : assembly.GetExportedTypes();
        }

        private ImmutableDictionary<string, object?> GetExportMetadata(ICustomAttributeProvider member)
        {
            Requires.NotNull(member, nameof(member));

            var result = ImmutableDictionary.CreateBuilder<string, object?>();
            var namesOfMetadataWithMultipleValues = new HashSet<string>(StringComparer.Ordinal);
            foreach (var attribute in member.GetAttributes<Attribute>())
            {
                var attrType = attribute.GetType().GetTypeInfo();
                var exportMetadataAttribute = attribute as ExportMetadataAttribute;
                if (exportMetadataAttribute != null)
                {
                    UpdateMetadataDictionary(result, namesOfMetadataWithMultipleValues, exportMetadataAttribute.Name, exportMetadataAttribute.Value, null);
                }
                else
                {
                    // Perf optimization, relies on short circuit evaluation, often a property attribute is an ExportAttribute
                    if (attrType != typeof(ExportAttribute).GetTypeInfo() && attrType.IsAttributeDefined<MetadataAttributeAttribute>(inherit: true))
                    {
                        var properties = attrType.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                        foreach (var property in properties.Where(p => p.DeclaringType != typeof(Attribute)))
                        {
                            UpdateMetadataDictionary(result, namesOfMetadataWithMultipleValues, property.Name, property.GetValue(attribute), ReflectionHelpers.GetMemberType(property));
                        }
                    }
                }
            }

            return result.ToImmutable();
        }

        private static void UpdateMetadataDictionary(IDictionary<string, object?> result, HashSet<string> namesOfMetadataWithMultipleValues, string name, object? value, Type? elementType)
        {
            object? priorValue;
            if (result.TryGetValue(name, out priorValue))
            {
                if (namesOfMetadataWithMultipleValues.Add(name))
                {
                    // This is exactly the second metadatum we've observed with this name.
                    // Convert the first value to an element in an array.
                    priorValue = AddElement(null, priorValue, elementType);
                }

                result[name] = AddElement((Array?)priorValue, value, elementType);
            }
            else
            {
                result.Add(name, value);
            }
        }

        private bool TryCreateImportDefinition(Type importingType, ICustomAttributeProvider member, ImmutableHashSet<IImportSatisfiabilityConstraint> importConstraints, [NotNullWhen(true)] out ImportDefinition? importDefinition)
        {
            Requires.NotNull(importingType, nameof(importingType));
            Requires.NotNull(member, nameof(member));

            var importAttribute = member.GetFirstAttribute<ImportAttribute>();
            var importManyAttribute = member.GetFirstAttribute<ImportManyAttribute>();

            // Importing constructors get implied attributes on their parameters.
            if (importAttribute == null && importManyAttribute == null && member is ParameterInfo)
            {
                importAttribute = new ImportAttribute();
            }

            var sharingBoundaries = ImmutableHashSet.Create<string>();
            var sharingBoundaryAttribute = member.GetFirstAttribute<SharingBoundaryAttribute>();
            if (sharingBoundaryAttribute != null)
            {
                Verify.Operation(importingType.IsExportFactoryTypeV2(), Strings.IsExpectedOnlyOnImportsOfExportFactoryOfTV2, typeof(SharingBoundaryAttribute).Name, importingType.FullName);
                sharingBoundaries = sharingBoundaries.Union(sharingBoundaryAttribute.SharingBoundaryNames);
            }

            if (member is PropertyInfo importingMember && importingMember.SetMethod == null)
            {
                // MEFv2 quietly ignores such importing members.
                importDefinition = null;
                return false;
            }

            if (importAttribute != null)
            {
                this.ThrowOnInvalidImportingMemberOrParameter(member, isImportMany: false);

                Type contractType = GetTypeIdentityFromImportingType(importingType, importMany: false);
                if (contractType.IsAnyLazyType() || contractType.IsExportFactoryTypeV2())
                {
                    contractType = contractType.GetTypeInfo().GetGenericArguments()[0];
                }

                importConstraints = importConstraints
                    .Union(this.GetMetadataViewConstraints(importingType, importMany: false))
                    .Union(GetExportTypeIdentityConstraints(contractType));
                importDefinition = new ImportDefinition(
                    string.IsNullOrEmpty(importAttribute.ContractName) ? GetContractName(contractType) : importAttribute.ContractName,
                    importAttribute.AllowDefault ? ImportCardinality.OneOrZero : ImportCardinality.ExactlyOne,
                    GetImportMetadataForGenericTypeImport(contractType),
                    importConstraints,
                    sharingBoundaries);
                return true;
            }
            else if (importManyAttribute != null)
            {
                this.ThrowOnInvalidImportingMemberOrParameter(member, isImportMany: true);

                Type contractType = GetTypeIdentityFromImportingType(importingType, importMany: true);
                importConstraints = importConstraints
                    .Union(this.GetMetadataViewConstraints(importingType, importMany: true))
                    .Union(GetExportTypeIdentityConstraints(contractType));
                importDefinition = new ImportDefinition(
                    string.IsNullOrEmpty(importManyAttribute.ContractName) ? GetContractName(contractType) : importManyAttribute.ContractName,
                    ImportCardinality.ZeroOrMore,
                    GetImportMetadataForGenericTypeImport(contractType),
                    importConstraints,
                    sharingBoundaries);
                return true;
            }
            else
            {
                importDefinition = null;
                return false;
            }
        }

        private ImportDefinitionBinding CreateImport(ParameterInfo parameter, ImmutableHashSet<IImportSatisfiabilityConstraint> importConstraints)
        {
            Assumes.True(this.TryCreateImportDefinition(parameter.ParameterType, parameter, importConstraints, out ImportDefinition? importDefinition));
            return new ImportDefinitionBinding(
                importDefinition,
                TypeRef.Get(parameter.Member.DeclaringType!, this.Resolver),
                ParameterRef.Get(parameter, this.Resolver),
                TypeRef.Get(parameter.ParameterType, this.Resolver),
                TypeRef.Get(GetImportingSiteTypeWithoutCollection(importDefinition, parameter.ParameterType), this.Resolver));
        }

        /// <summary>
        /// Creates a set of import constraints for an import site.
        /// </summary>
        /// <param name="importSite">The importing member or parameter.</param>
        /// <returns>A set of import constraints.</returns>
        private static ImmutableHashSet<IImportSatisfiabilityConstraint> GetImportConstraints(ICustomAttributeProvider importSite)
        {
            Requires.NotNull(importSite, nameof(importSite));

            var constraints = ImmutableHashSet.CreateRange<IImportSatisfiabilityConstraint>(
                from importConstraint in importSite.GetAttributes<ImportMetadataConstraintAttribute>()
                select new ExportMetadataValueImportConstraint(importConstraint.Name, importConstraint.Value));

            return constraints;
        }

        private static ExportDefinition CreateExportDefinition(ImmutableDictionary<string, object?> memberExportMetadata, ExportAttribute exportAttribute, Type exportedType)
        {
            string contractName = string.IsNullOrEmpty(exportAttribute.ContractName) ? GetContractName(exportedType) : exportAttribute.ContractName;
            var exportMetadata = memberExportMetadata
                .Add(CompositionConstants.ExportTypeIdentityMetadataName, ContractNameServices.GetTypeIdentity(exportedType));
            var exportDefinition = new ExportDefinition(contractName, exportMetadata);
            return exportDefinition;
        }
    }
}
