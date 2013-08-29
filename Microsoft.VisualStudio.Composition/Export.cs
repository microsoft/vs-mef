namespace Microsoft.VisualStudio.Composition
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using System.Text;
    using System.Threading.Tasks;
    using Validation;

    public class Export
    {
        public Export(ExportDefinition exportDefinition, ComposablePartDefinition partDefinition, MemberInfo exportingMember)
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
            get { return this.ExportingMember.IsStaticExport(); }
        }

        public Type ExportedValueType
        {
            get
            {
                if (this.ExportingMember == null)
                {
                    return this.PartDefinition.Type;
                }

                switch (this.ExportingMember.MemberType)
                {
                    case MemberTypes.Field:
                        return ((FieldInfo)this.ExportingMember).FieldType;
                    case MemberTypes.Property:
                        return ((PropertyInfo)this.ExportingMember).PropertyType;
                    case MemberTypes.Method:
                        return GetContractTypeForDelegate((MethodInfo)this.ExportingMember);
                    default:
                        throw new NotSupportedException();
                }
            }
        }

        internal static Type GetContractTypeForDelegate(MethodInfo method)
        {
            Type genericTypeDefinition;
            int parametersCount = method.GetParameters().Length;
            var typeArguments = method.GetParameters().Select(p => p.ParameterType).ToList();
            var voidResult = method.ReturnType.IsEquivalentTo(typeof(void));
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
    }
}
