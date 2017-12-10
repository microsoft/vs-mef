// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.VisualStudio.Composition
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Globalization;
    using System.Linq;
    using System.Reflection;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.VisualStudio.Composition.Reflection;

    public class ExportDefinitionBinding : IEquatable<ExportDefinitionBinding>
    {
        public ExportDefinitionBinding(ExportDefinition exportDefinition, ComposablePartDefinition partDefinition, MemberRef exportingMemberRef)
        {
            Requires.NotNull(exportDefinition, nameof(exportDefinition));
            Requires.NotNull(partDefinition, nameof(partDefinition));

            this.ExportDefinition = exportDefinition;
            this.PartDefinition = partDefinition;
            this.ExportingMemberRef = exportingMemberRef;
        }

        public ExportDefinition ExportDefinition { get; private set; }

        // TODO: remove this member, perhaps in favor of just a property of type TypeRef,
        // so that ComposablePartDefinition can contain a collection of ExportDefinitionBinding
        // instead of just ExportDefinition in a dictionary.
        // This would make it parallel to ImportDefinitionBinding.
        public ComposablePartDefinition PartDefinition { get; private set; }

        /// <summary>
        /// Gets the member with the ExportAttribute applied. <c>null</c> when the export is on the type itself.
        /// </summary>
        public MemberInfo ExportingMember
        {
            get { return this.ExportingMemberRef.MemberInfo; }
        }

        /// <summary>
        /// Gets the member with the ExportAttribute applied. The return value's <see cref="MemberRef.IsEmpty"/>
        /// is <c>true</c> when the export is on the type itself.
        /// </summary>
        public MemberRef ExportingMemberRef { get; private set; }

        /// <summary>
        /// Gets a value indicating whether the exporting member is static.
        /// </summary>
        public bool IsStaticExport
        {
            get { return this.ExportingMember.IsStatic(); }
        }

        public TypeRef ExportedValueTypeRef
        {
            get { return TypeRef.Get(this.ExportedValueType, this.PartDefinition.TypeRef.Resolver); }
        }

        public Type ExportedValueType
        {
            get { return ReflectionHelpers.GetExportedValueType(this.PartDefinition.Type, this.ExportingMember); }
        }

        internal ExportDefinitionBinding CloseGenericExport(Type[] genericTypeArguments)
        {
            Requires.NotNull(genericTypeArguments, nameof(genericTypeArguments));

            string exportTypeIdentity = string.Format(
                CultureInfo.InvariantCulture,
                (string)this.ExportDefinition.Metadata[CompositionConstants.ExportTypeIdentityMetadataName],
                genericTypeArguments.Select(ContractNameServices.GetTypeIdentity).ToArray());
            var updatedMetadata = ImmutableDictionary.CreateRange(this.ExportDefinition.Metadata)
                .SetItem(CompositionConstants.ExportTypeIdentityMetadataName, exportTypeIdentity);
            return new ExportDefinitionBinding(
                new ExportDefinition(this.ExportDefinition.ContractName, updatedMetadata),
                this.PartDefinition,
                this.ExportingMemberRef);
        }

        public override bool Equals(object obj)
        {
            return this.Equals(obj as ExportDefinitionBinding);
        }

        public override int GetHashCode()
        {
            int hashCode = this.PartDefinition.TypeRef.GetHashCode();
            if (!this.ExportingMemberRef.IsEmpty)
            {
                hashCode += this.ExportingMemberRef.GetHashCode();
            }

            return hashCode;
        }

        public bool Equals(ExportDefinitionBinding other)
        {
            bool result = this.PartDefinition.TypeRef.Equals(other.PartDefinition.TypeRef)
                && this.ExportDefinition.Equals(other.ExportDefinition)
                && this.ExportingMemberRef.Equals(other.ExportingMemberRef);
            return result;
        }
    }
}
