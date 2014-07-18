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
    using Validation;

    public class ExportDefinitionBinding
    {
        public ExportDefinitionBinding(ExportDefinition exportDefinition, ComposablePartDefinition partDefinition, MemberInfo exportingMember)
        {
            Requires.NotNull(exportDefinition, "exportDefinition");
            Requires.NotNull(partDefinition, "partDefinition");

            this.ExportDefinition = exportDefinition;
            this.PartDefinition = partDefinition;
            this.ExportingMember = exportingMember;
        }

        public ExportDefinition ExportDefinition { get; private set; }

        public ComposablePartDefinition PartDefinition { get; private set; }

        /// <summary>
        /// Gets the member with the ExportAttribute applied. <c>null</c> when the export is on the type itself.
        /// </summary>
        public MemberInfo ExportingMember { get; private set; }

        /// <summary>
        /// Gets a value indicating whether the exporting member is static.
        /// </summary>
        public bool IsStaticExport
        {
            get { return this.ExportingMember.IsStatic(); }
        }

        public Type ExportedValueType
        {
            get
            {
                if (this.ExportingMember == null)
                {
                    return this.PartDefinition.Type;
                }

                var exportingField = this.ExportingMember as FieldInfo;
                if (exportingField != null)
                {
                    return exportingField.FieldType;
                }

                var exportingProperty = this.ExportingMember as PropertyInfo;
                if (exportingProperty != null)
                {
                    return exportingProperty.PropertyType;
                }

                var exportingMethod = this.ExportingMember as MethodInfo;
                if (exportingMethod != null)
                {
                    return GetContractTypeForDelegate(exportingMethod);
                }

                throw new NotSupportedException();
            }
        }

        internal static Type GetContractTypeForDelegate(MethodInfo method)
        {
            Type genericTypeDefinition;
            int parametersCount = method.GetParameters().Length;
            var typeArguments = method.GetParameters().Select(p => p.ParameterType).ToList();
            var voidResult = method.ReturnType.Equals(typeof(void));
            if (voidResult)
            {
                if (typeArguments.Count == 0)
                {
                    return typeof(Action);
                }

                genericTypeDefinition = Type.GetType("System.Action`" + typeArguments.Count);
            }
            else
            {
                typeArguments.Add(method.ReturnType);
                genericTypeDefinition = Type.GetType("System.Func`" + typeArguments.Count);
            }

            return genericTypeDefinition.MakeGenericType(typeArguments.ToArray());
        }

        internal ExportDefinitionBinding CloseGenericExport(Type[] genericTypeArguments)
        {
            Requires.NotNull(genericTypeArguments, "genericTypeArguments");

            string exportTypeIdentity = string.Format(
                CultureInfo.InvariantCulture,
                (string)this.ExportDefinition.Metadata[CompositionConstants.ExportTypeIdentityMetadataName],
                genericTypeArguments.Select(ContractNameServices.GetTypeIdentity).ToArray());
            var updatedMetadata = ImmutableDictionary.CreateRange(this.ExportDefinition.Metadata)
                .SetItem(CompositionConstants.ExportTypeIdentityMetadataName, exportTypeIdentity);
            return new ExportDefinitionBinding(
                new ExportDefinition(this.ExportDefinition.ContractName, updatedMetadata),
                this.PartDefinition,
                this.ExportingMember);
        }
    }
}
