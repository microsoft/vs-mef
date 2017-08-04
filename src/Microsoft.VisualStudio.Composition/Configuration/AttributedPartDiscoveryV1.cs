// Copyright (c) Microsoft. All rights reserved.

#if NET45

namespace Microsoft.VisualStudio.Composition
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.ComponentModel.Composition;
    using System.Diagnostics;
    using System.Globalization;
    using System.Linq;
    using System.Reflection;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.VisualStudio.Composition.Reflection;
    using MefV1 = System.ComponentModel.Composition;

    public class AttributedPartDiscoveryV1 : PartDiscovery
    {
        private static readonly MethodInfo OnImportsSatisfiedMethodInfo = typeof(IPartImportsSatisfiedNotification).GetMethod("OnImportsSatisfied", BindingFlags.Public | BindingFlags.Instance);

        public AttributedPartDiscoveryV1(Resolver resolver)
            : base(resolver)
        {
        }

        protected override ComposablePartDefinition CreatePart(Type partType, bool typeExplicitlyRequested)
        {
            Requires.NotNull(partType, nameof(partType));

            // We want to ignore abstract classes, but we want to consider static classes.
            // Static classes claim to be both abstract and sealed. So to ignore just abstract
            // ones, we check that they are not sealed.
            if (partType.IsAbstract && !partType.IsSealed)
            {
                return null;
            }

            BindingFlags everythingLocal = BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
            BindingFlags instanceLocal = BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

            // If the type is abstract only find local static exports
            var exportBindingFlags = everythingLocal;
            if (partType.IsAbstract)
            {
                exportBindingFlags &= ~BindingFlags.Instance;
            }

            var declaredMethods = partType.GetMethods(exportBindingFlags); // methods can only export, not import
            var declaredProperties = partType.GetProperties(everythingLocal);
            var declaredFields = partType.GetFields(everythingLocal);

            var allLocalMembers = declaredMethods.Concat<MemberInfo>(declaredProperties).Concat(declaredFields);
            var exportingMembers = from member in allLocalMembers
                                   from export in member.GetAttributes<ExportAttribute>()
                                   select new KeyValuePair<MemberInfo, ExportAttribute>(member, export);
            var exportedTypes = from export in partType.GetAttributes<ExportAttribute>()
                                select new KeyValuePair<MemberInfo, ExportAttribute>(partType, export);
            var inheritedExportedTypes = from baseTypeOrInterface in partType.GetInterfaces().Concat(partType.EnumTypeAndBaseTypes().Skip(1))
                                         where baseTypeOrInterface != typeof(object)
                                         from export in baseTypeOrInterface.GetAttributes<InheritedExportAttribute>()
                                         select new KeyValuePair<MemberInfo, ExportAttribute>(baseTypeOrInterface, export);

            var exportsByMember = (from export in exportingMembers.Concat(exportedTypes).Concat(inheritedExportedTypes)
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
            if (!typeExplicitlyRequested && partType.IsAttributeDefined<PartNotDiscoverableAttribute>())
            {
                return null;
            }

            TypeRef partTypeRef = TypeRef.Get(partType, this.Resolver);
            Type partTypeAsGenericTypeDefinition = partType.IsGenericType ? partType.GetGenericTypeDefinition() : null;

            // Collect information for all imports.
            var imports = ImmutableList.CreateBuilder<ImportDefinitionBinding>();
            this.AddImportsFromMembers(declaredProperties, declaredFields, partTypeRef, imports);
            Type baseType = partType.BaseType;
            while (baseType != null && baseType != typeof(object))
            {
                this.AddImportsFromMembers(baseType.GetProperties(instanceLocal), baseType.GetFields(instanceLocal), partTypeRef, imports);
                baseType = baseType.BaseType;
            }

            var partCreationPolicy = CreationPolicy.Any;
            var partCreationPolicyAttribute = partType.GetFirstAttribute<PartCreationPolicyAttribute>();
            if (partCreationPolicyAttribute != null)
            {
                partCreationPolicy = (CreationPolicy)partCreationPolicyAttribute.CreationPolicy;
            }

            var allExportsMetadata = ImmutableDictionary.CreateRange(PartCreationPolicyConstraint.GetExportMetadata(partCreationPolicy));
            var inheritedExportContractNamesFromNonInterfaces = ImmutableHashSet.CreateBuilder<string>();
            var exportDefinitions = ImmutableList.CreateBuilder<KeyValuePair<MemberInfo, ExportDefinition>>();
            foreach (var export in exportsByMember)
            {
                var memberExportMetadata = allExportsMetadata.AddRange(GetExportMetadata(export.Key));

                if (export.Key is MethodInfo)
                {
                    var method = export.Key as MethodInfo;
                    var exportAttributes = export.Value;
                    if (exportAttributes.Any())
                    {
                        foreach (var exportAttribute in exportAttributes)
                        {
                            Type exportedType = exportAttribute.ContractType ?? ReflectionHelpers.GetContractTypeForDelegate(method);
                            string contractName = string.IsNullOrEmpty(exportAttribute.ContractName) ? GetContractName(exportedType) : exportAttribute.ContractName;
                            var exportMetadata = memberExportMetadata
                                .Add(CompositionConstants.ExportTypeIdentityMetadataName, ContractNameServices.GetTypeIdentity(exportedType));
                            var exportDefinition = new ExportDefinition(contractName, exportMetadata);
                            exportDefinitions.Add(new KeyValuePair<MemberInfo, ExportDefinition>(export.Key, exportDefinition));
                        }
                    }
                }
                else
                {
                    MemberInfo exportingTypeOrPropertyOrField = export.Key;
                    Verify.Operation(export.Key is Type || !partType.IsGenericTypeDefinition, Strings.ExportsOnMembersNotAllowedWhenDeclaringTypeGeneric);
                    Type exportSiteType = ReflectionHelpers.GetMemberType(exportingTypeOrPropertyOrField);
                    foreach (var exportAttribute in export.Value)
                    {
                        Type exportedType = exportAttribute.ContractType ?? partTypeAsGenericTypeDefinition ?? exportSiteType;
                        string contractName = string.IsNullOrEmpty(exportAttribute.ContractName) ? GetContractName(exportedType) : exportAttribute.ContractName;
                        if (export.Key is Type && exportAttribute is InheritedExportAttribute)
                        {
                            if (inheritedExportContractNamesFromNonInterfaces.Contains(contractName))
                            {
                                // We already have an export with this contract name on this type (from a more derived type)
                                // using InheritedExportAttribute.
                                continue;
                            }

                            if (!((Type)export.Key).IsInterface)
                            {
                                inheritedExportContractNamesFromNonInterfaces.Add(contractName);
                            }
                        }

                        var exportMetadata = memberExportMetadata
                            .Add(CompositionConstants.ExportTypeIdentityMetadataName, ContractNameServices.GetTypeIdentity(exportedType));
                        var exportDefinition = new ExportDefinition(contractName, exportMetadata);
                        exportDefinitions.Add(new KeyValuePair<MemberInfo, ExportDefinition>(export.Key, exportDefinition));
                    }
                }
            }

            MethodInfo onImportsSatisfied = null;
            if (typeof(IPartImportsSatisfiedNotification).IsAssignableFrom(partType))
            {
                onImportsSatisfied = OnImportsSatisfiedMethodInfo;
            }

            var importingConstructorParameters = ImmutableList.CreateBuilder<ImportDefinitionBinding>();
            var importingCtor = GetImportingConstructor<ImportingConstructorAttribute>(partType, publicOnly: false);
            if (importingCtor != null) // some parts have exports merely for metadata -- they can't be instantiated
            {
                foreach (var parameter in importingCtor.GetParameters())
                {
                    var import = this.CreateImport(parameter);
                    if (import.ImportDefinition.Cardinality == ImportCardinality.ZeroOrMore)
                    {
                        Verify.Operation(PartDiscovery.IsImportManyCollectionTypeCreateable(import), Strings.CollectionMustBePublicAndPublicCtorWhenUsingImportingCtor);
                    }

                    importingConstructorParameters.Add(import);
                }
            }

            var partMetadata = ImmutableDictionary.CreateBuilder<string, object>();
            foreach (var partMetadataAttribute in partType.GetAttributes<PartMetadataAttribute>())
            {
                partMetadata[partMetadataAttribute.Name] = partMetadataAttribute.Value;
            }

            var exportsOnType = exportDefinitions.Where(kv => kv.Key is Type).Select(kv => kv.Value).ToArray();
            var exportsOnMembers = (from kv in exportDefinitions
                                    where !(kv.Key is Type)
                                    group kv.Value by kv.Key into byMember
                                    select byMember).ToDictionary(g => MemberRef.Get(g.Key, this.Resolver), g => (IReadOnlyCollection<ExportDefinition>)g.ToArray());

            var assemblyNamesForMetadataAttributes = ImmutableHashSet.CreateBuilder<AssemblyName>();
            foreach (var export in exportsByMember)
            {
                GetAssemblyNamesFromMetadataAttributes<MetadataAttributeAttribute>(export.Key, assemblyNamesForMetadataAttributes);
            }

            return new ComposablePartDefinition(
                TypeRef.Get(partType, this.Resolver),
                partMetadata.ToImmutable(),
                exportsOnType,
                exportsOnMembers,
                imports.ToImmutable(),
                partCreationPolicy != CreationPolicy.NonShared ? string.Empty : null,
                MethodRef.Get(onImportsSatisfied, this.Resolver),
                MethodRef.Get(importingCtor, this.Resolver),
                importingCtor != null ? importingConstructorParameters.ToImmutable() : null, // some MEF parts are only for metadata
                partCreationPolicy,
                assemblyNamesForMetadataAttributes,
                partCreationPolicy != CreationPolicy.NonShared);
        }

        private void AddImportsFromMembers(PropertyInfo[] declaredProperties, FieldInfo[] declaredFields, TypeRef partTypeRef, IList<ImportDefinitionBinding> imports)
        {
            Requires.NotNull(declaredProperties, nameof(declaredProperties));
            Requires.NotNull(declaredFields, nameof(declaredFields));
            Requires.NotNull(partTypeRef, nameof(partTypeRef));
            Requires.NotNull(imports, nameof(imports));

            foreach (var member in declaredFields.Concat<MemberInfo>(declaredProperties))
            {
                if (!member.IsStatic())
                {
                    if (this.TryCreateImportDefinition(ReflectionHelpers.GetMemberType(member), member, out ImportDefinition importDefinition))
                    {
                        Type importingSiteType = ReflectionHelpers.GetMemberType(member);
                        var importDefinitionBinding = new ImportDefinitionBinding(
                            importDefinition,
                            partTypeRef,
                            MemberRef.Get(member, this.Resolver),
                            TypeRef.Get(importingSiteType, this.Resolver),
                            TypeRef.Get(GetImportingSiteTypeWithoutCollection(importDefinition, importingSiteType), this.Resolver));
                        imports.Add(importDefinitionBinding);
                    }
                }
            }
        }

        public override bool IsExportFactoryType(Type type)
        {
            if (type != null && type.GetTypeInfo().IsGenericType)
            {
                var typeDefinition = type.GetGenericTypeDefinition();
                if (typeDefinition.Equals(typeof(ExportFactory<>)) || typeDefinition.IsEquivalentTo(typeof(ExportFactory<,>)))
                {
                    return true;
                }
            }

            return false;
        }

        protected override IEnumerable<Type> GetTypes(Assembly assembly)
        {
            Requires.NotNull(assembly, nameof(assembly));

            return assembly.GetTypes();
        }

        private bool TryCreateImportDefinition(Type importingType, ICustomAttributeProvider member, out ImportDefinition importDefinition)
        {
            Requires.NotNull(importingType, nameof(importingType));
            Requires.NotNull(member, nameof(member));

            ImportAttribute importAttribute = member.GetFirstAttribute<ImportAttribute>();
            ImportManyAttribute importManyAttribute = member.GetFirstAttribute<ImportManyAttribute>();

            // Importing constructors get implied attributes on their parameters.
            if (importAttribute == null && importManyAttribute == null && member is ParameterInfo)
            {
                importAttribute = new ImportAttribute();
            }

            if (importAttribute != null)
            {
                if (importAttribute.Source != ImportSource.Any)
                {
                    throw new NotSupportedException(Strings.CustomImportSourceNotSupported);
                }

                var requiredCreationPolicy = importingType.IsExportFactoryTypeV1()
                    ? CreationPolicy.NonShared
                    : (CreationPolicy)importAttribute.RequiredCreationPolicy;

                Type contractType = importAttribute.ContractType ?? GetTypeIdentityFromImportingType(importingType, importMany: false);
                var constraints = PartCreationPolicyConstraint.GetRequiredCreationPolicyConstraints(requiredCreationPolicy)
                    .Union(this.GetMetadataViewConstraints(importingType, importMany: false))
                    .Union(GetExportTypeIdentityConstraints(contractType));
                importDefinition = new ImportDefinition(
                    string.IsNullOrEmpty(importAttribute.ContractName) ? GetContractName(contractType) : importAttribute.ContractName,
                    importAttribute.AllowDefault ? ImportCardinality.OneOrZero : ImportCardinality.ExactlyOne,
                    GetImportMetadataForGenericTypeImport(contractType),
                    constraints);
                return true;
            }
            else if (importManyAttribute != null)
            {
                if (importManyAttribute.Source != ImportSource.Any)
                {
                    throw new NotSupportedException(Strings.CustomImportSourceNotSupported);
                }

                var requiredCreationPolicy = GetElementTypeFromMany(importingType).IsExportFactoryTypeV1()
                    ? CreationPolicy.NonShared
                    : (CreationPolicy)importManyAttribute.RequiredCreationPolicy;

                Type contractType = importManyAttribute.ContractType ?? GetTypeIdentityFromImportingType(importingType, importMany: true);
                var constraints = PartCreationPolicyConstraint.GetRequiredCreationPolicyConstraints(requiredCreationPolicy)
                    .Union(this.GetMetadataViewConstraints(importingType, importMany: true))
                    .Union(GetExportTypeIdentityConstraints(contractType));
                importDefinition = new ImportDefinition(
                    string.IsNullOrEmpty(importManyAttribute.ContractName) ? GetContractName(contractType) : importManyAttribute.ContractName,
                    ImportCardinality.ZeroOrMore,
                    GetImportMetadataForGenericTypeImport(contractType),
                    constraints);
                return true;
            }
            else
            {
                importDefinition = null;
                return false;
            }
        }

        private ImportDefinitionBinding CreateImport(ParameterInfo parameter)
        {
            Assumes.True(this.TryCreateImportDefinition(parameter.ParameterType, parameter, out ImportDefinition importDefinition));
            return new ImportDefinitionBinding(
                importDefinition,
                TypeRef.Get(parameter.Member.DeclaringType, this.Resolver),
                ParameterRef.Get(parameter, this.Resolver),
                TypeRef.Get(parameter.ParameterType, this.Resolver),
                TypeRef.Get(GetImportingSiteTypeWithoutCollection(importDefinition, parameter.ParameterType), this.Resolver));
        }

        private static IReadOnlyDictionary<string, object> GetExportMetadata(MemberInfo member)
        {
            Requires.NotNull(member, nameof(member));

            var result = ImmutableDictionary.CreateBuilder<string, object>();
            foreach (var attribute in member.GetAttributes<Attribute>())
            {
                var exportMetadataAttribute = attribute as ExportMetadataAttribute;
                if (exportMetadataAttribute != null)
                {
                    if (exportMetadataAttribute.IsMultiple)
                    {
                        result[exportMetadataAttribute.Name] = AddElement(result.GetValueOrDefault(exportMetadataAttribute.Name) as Array, exportMetadataAttribute.Value, null);
                    }
                    else
                    {
                        result.Add(exportMetadataAttribute.Name, exportMetadataAttribute.Value);
                    }
                }
                else
                {
                    Type attrType = attribute.GetType();

                    // Perf optimization, relies on short circuit evaluation, often a property attribute is an ExportAttribute
                    if (attrType != typeof(ExportAttribute) && attrType.IsAttributeDefined<MetadataAttributeAttribute>(true))
                    {
                        var usage = attrType.GetFirstAttribute<AttributeUsageAttribute>(true);
                        var properties = attribute.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);

                        foreach (var property in properties.Where(p => p.DeclaringType != typeof(Attribute) && p.DeclaringType != typeof(ExportAttribute)))
                        {
                            if (usage != null && usage.AllowMultiple)
                            {
                                result[property.Name] = AddElement(result.GetValueOrDefault(property.Name) as Array, property.GetValue(attribute), ReflectionHelpers.GetMemberType(property));
                            }
                            else
                            {
                                if (result.ContainsKey(property.Name))
                                {
                                    string memberName = member.MemberType.HasFlag(MemberTypes.TypeInfo) || member.MemberType.HasFlag(MemberTypes.NestedType)
                                        ? ((Type)member).FullName
                                        : $"{member.DeclaringType.FullName}.{member.Name}";

                                    throw new NotSupportedException(string.Format(CultureInfo.CurrentCulture, Strings.DiscoveredIdenticalPropertiesInMetadataAttributesForPart, memberName, property.Name));
                                }

                                result.Add(property.Name, property.GetValue(attribute));
                            }
                        }
                    }
                }
            }

            return result.ToImmutable();
        }
    }
}

#endif
