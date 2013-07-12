namespace Microsoft.VisualStudio.Composition
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Globalization;
    using System.Linq;
    using System.Reflection;
    using System.Text;
    using System.Threading.Tasks;
    using Validation;

    partial class CompositionTemplateFactory
    {
        public CompositionConfiguration Configuration { get; set; }

        private void EmitImportSatisfyingAssignment(KeyValuePair<Import, IReadOnlyList<Export>> satisfyingExport)
        {
            var importingMember = satisfyingExport.Key.ImportingMember;
            var importDefinition = satisfyingExport.Key.ImportDefinition;
            var importingPartDefinition = satisfyingExport.Key.PartDefinition;
            var exports = satisfyingExport.Value;
            string fullTypeNameWithPerhapsLazy = GetTypeName(importDefinition.LazyType ?? importDefinition.CoercedValueType);

            string left = "result." + importingMember.Name;
            var right = new StringBuilder();
            if (importDefinition.Cardinality == ImportCardinality.ZeroOrMore)
            {
                right.AppendFormat("new List<{0}> {{", fullTypeNameWithPerhapsLazy);
                this.PushIndent("    ");
                foreach (var export in exports)
                {
                    string memberModifier = export.ExportingMember == null ? string.Empty : "." + export.ExportingMember.Name;
                    right.AppendLine();
                    right.Append(this.CurrentIndent);
                    if (importDefinition.IsLazyConcreteType)
                    {
                        if (importDefinition.Contract.Type.IsEquivalentTo(export.PartDefinition.Type))
                        {
                            right.AppendFormat("({0})", fullTypeNameWithPerhapsLazy);
                        }
                        else
                        {
                            right.AppendFormat("new {0}(() => ", fullTypeNameWithPerhapsLazy);
                        }
                    }

                    right.AppendFormat(
                        "this.{0}(provisionalDisposableObjects, provisionalSharedObjects)",
                        GetPartFactoryMethodName(export.PartDefinition, importDefinition.Contract.Type.GetGenericArguments().Select(GetTypeName).ToArray()));
                    if (importDefinition.IsLazy)
                    {
                        if (importDefinition.IsLazyConcreteType && !importDefinition.Contract.Type.IsEquivalentTo(export.PartDefinition.Type))
                        {
                            right.AppendFormat(".Value{0}, true)", memberModifier);
                        }
                    }
                    else
                    {
                        right.Append(".Value");
                        right.Append(memberModifier);
                    }

                    right.Append(",");
                }

                this.PopIndent();
                right.Append(Environment.NewLine);
                right.Append(this.CurrentIndent);
                right.Append("}");
            }
            else if (exports.Any())
            {
                var export = exports.Single();
                string memberModifier = export.ExportingMember == null ? string.Empty : "." + export.ExportingMember.Name;
                if (export.PartDefinition == importingPartDefinition && importingPartDefinition.IsShared)
                {
                    right.Append("result");
                }
                else
                {
                    if (importDefinition.IsLazyConcreteType)
                    {
                        if (importDefinition.MetadataType == null && importDefinition.Contract.Type.IsEquivalentTo(export.PartDefinition.Type))
                        {
                            right.AppendFormat("({0})", fullTypeNameWithPerhapsLazy);
                        }
                        else
                        {
                            right.AppendFormat("new {0}(() => ", fullTypeNameWithPerhapsLazy);
                        }
                    }

                    this.Write(this.CurrentIndent);
                    this.WriteLine("var {0} = {1};", importingMember.Name, "this." + GetPartFactoryMethodName(export.PartDefinition, importDefinition.Contract.Type.GetGenericArguments().Select(GetTypeName).ToArray()) + "(provisionalDisposableObjects, provisionalSharedObjects)");
                    right.Append(importingMember.Name);
                    if (importDefinition.IsLazy)
                    {
                        if (importDefinition.MetadataType != null)
                        {
                            right.AppendFormat(".Value{0}, ", memberModifier);
                            if (importDefinition.MetadataType != typeof(IDictionary<string, object>))
                            {
                                right.AppendFormat("new {0}(", GetClassNameForMetadataView(importDefinition.MetadataType));
                            }

                            right.Append(GetExportMetadata(export));
                            if (importDefinition.MetadataType != typeof(IDictionary<string, object>))
                            {
                                right.Append(")");
                            }

                            right.Append(", true)");
                        }
                        else if (importDefinition.IsLazyConcreteType && !importDefinition.Contract.Type.IsEquivalentTo(export.PartDefinition.Type))
                        {
                            right.AppendFormat(".Value{0}, true)", memberModifier);
                        }
                    }
                    else
                    {
                        right.AppendFormat(".Value{0}", memberModifier);
                    }
                }
            }

            if (right.Length > 0)
            {
                this.Write(this.CurrentIndent);
                this.Write(left);
                this.Write(" = ");
                this.Write(right.ToString());
                this.WriteLine(";");
            }
        }

        private string GetExportMetadata(Export export)
        {
            var builder = new StringBuilder();
            builder.Append("new Dictionary<string, object> {");
            foreach (var metadatum in export.ExportDefinition.Metadata)
            {
                builder.AppendFormat(" {{ \"{0}\", \"{1}\" }}, ", metadatum.Key, (string)metadatum.Value);
            }
            builder.Append("}");
            return builder.ToString();
        }

        private void EmitInstantiatePart(ComposablePart part)
        {
            this.Write(this.CurrentIndent);
            this.WriteLine("var result = new {0}();", GetTypeName(part.Definition.Type));
            this.Write(this.CurrentIndent);
            this.WriteLine("provisionalSharedObjects.Add(typeof({0}), result);", GetTypeName(part.Definition.Type));

            foreach (var satisfyingExport in part.SatisfyingExports)
            {
                this.EmitImportSatisfyingAssignment(satisfyingExport);
            }

            if (part.Definition.OnImportsSatisfied != null)
            {
                if (part.Definition.OnImportsSatisfied.DeclaringType.IsInterface)
                {
                    this.Write(this.CurrentIndent);
                    this.WriteLine("{0} onImportsSatisfiedInterface = result;", part.Definition.OnImportsSatisfied.DeclaringType.FullName);
                    this.Write(this.CurrentIndent);
                    this.WriteLine("onImportsSatisfiedInterface.{0}();", part.Definition.OnImportsSatisfied.Name);
                }
                else
                {
                    this.Write(this.CurrentIndent);
                    this.WriteLine("result.{0}();", part.Definition.OnImportsSatisfied.Name);
                }
            }

            this.Write(this.CurrentIndent);
            this.WriteLine("return result;");
        }

        private HashSet<Type> GetMetadataViewInterfaces()
        {
            var set = new HashSet<Type>();

            set.UnionWith(
                from part in this.Configuration.Parts
                from importAndExports in part.SatisfyingExports
                where importAndExports.Value.Count > 0
                let metadataType = importAndExports.Key.ImportDefinition.MetadataType
                where metadataType != null && metadataType.IsInterface && metadataType != typeof(IDictionary<string, object>)
                select metadataType);

            return set;
        }

        private static string GetTypeName(Type type)
        {
            return GetTypeName(type, genericTypeDefinition: false);
        }

        private static string GetTypeName(Type type, bool genericTypeDefinition)
        {
            if (type.IsGenericParameter)
            {
                return type.Name;
            }

            string result = string.Empty;
            if (type.DeclaringType != null)
            {
                result = GetTypeName(type.DeclaringType, genericTypeDefinition) + ".";
            }

            if (genericTypeDefinition)
            {
                result += FilterTypeNameForGenericTypeDefinition(type, type.DeclaringType == null);
            }
            else
            {
                string[] typeArguments = type.GetGenericArguments().Select(GetTypeName).ToArray();
                result += ReplaceBackTickWithTypeArgs(type.DeclaringType == null ? type.FullName : type.Name, typeArguments);
            }

            return result;
        }

        private static string GetClassNameForMetadataView(Type metadataView)
        {
            Requires.NotNull(metadataView, "metadataView");

            if (metadataView.IsInterface)
            {
                return "ClassFor" + metadataView.Name;
            }

            return GetTypeName(metadataView);
        }

        private static string GetValueOrDefaultForMetadataView(PropertyInfo property, string sourceVarName)
        {
            var defaultValueAttribute = property.GetCustomAttribute<DefaultValueAttribute>();
            if (defaultValueAttribute != null)
            {
                return String.Format(
                    CultureInfo.InvariantCulture,
                    @"({0})({1}.ContainsKey(""{2}"") ? {1}[""{2}""] : ""{3}"")",
                    GetTypeName(property.PropertyType),
                    sourceVarName,
                    property.Name,
                    defaultValueAttribute.Value);
            }
            else
            {
                return String.Format(
                    CultureInfo.InvariantCulture,
                    @"({0}){1}[""{2}""]",
                    GetTypeName(property.PropertyType),
                    sourceVarName,
                    property.Name);
            }
        }

        private static string FilterTypeNameForGenericTypeDefinition(Type type, bool fullName)
        {
            Requires.NotNull(type, "type");

            string name = fullName ? type.FullName : type.Name;
            if (type.IsGenericType)
            {
                name = name.Substring(0, name.IndexOf('`'));
                name += "<";
                name += new String(',', type.GetGenericArguments().Length - 1);
                name += ">";
            }

            return name;
        }

        private static void Test<T>() { }

        private static string GetPartFactoryMethodNameNoTypeArgs(ComposablePartDefinition part)
        {
            string name = GetPartFactoryMethodName(part);
            int indexOfTypeArgs = name.IndexOf('<');
            if (indexOfTypeArgs >= 0)
            {
                return name.Substring(0, indexOfTypeArgs);
            }

            return name;
        }

        private static string GetPartFactoryMethodName(ComposablePartDefinition part, params string[] typeArguments)
        {
            if (typeArguments == null || typeArguments.Length == 0)
            {
                typeArguments = part.Type.GetGenericArguments().Select(t => t.Name).ToArray();
            }

            string name = "GetOrCreate" + ReplaceBackTickWithTypeArgs(part.Type.Name, typeArguments);
            return name;
        }

        private static string ReplaceBackTickWithTypeArgs(string originalName, params string[] typeArguments)
        {
            Requires.NotNullOrEmpty(originalName, "originalName");

            string name = originalName;
            int backTickIndex = originalName.IndexOf('`');
            if (backTickIndex >= 0)
            {
                name = originalName.Substring(0, name.IndexOf('`'));
                name += "<";
                int typeArgIndex = originalName.IndexOf('[', backTickIndex + 1);
                string typeArgumentsCountString = originalName.Substring(backTickIndex + 1);
                if (typeArgIndex >= 0)
                {
                    typeArgumentsCountString = typeArgumentsCountString.Substring(0, typeArgIndex - backTickIndex - 1);
                }

                int typeArgumentsCount = int.Parse(typeArgumentsCountString, CultureInfo.InvariantCulture);
                if (typeArguments == null || typeArguments.Length == 0)
                {
                    if (typeArgumentsCount == 1)
                    {
                        name += "T";
                    }
                    else
                    {
                        for (int i = 1; i <= typeArgumentsCount; i++)
                        {
                            name += "T" + i;
                            if (i < typeArgumentsCount)
                            {
                                name += ",";
                            }
                        }
                    }
                }
                else
                {
                    Requires.Argument(typeArguments.Length == typeArgumentsCount, "typeArguments", "Wrong length.");
                    name += string.Join(",", typeArguments);
                }

                name += ">";
            }

            return name;
        }

        private static string GetPartOrMemberLazy(string partLocalVariableName, MemberInfo member, ExportDefinition exportDefinition)
        {
            Requires.NotNullOrEmpty(partLocalVariableName, "partLocalVariableName");
            Requires.NotNull(exportDefinition, "exportDefinition");

            if (member == null)
            {
                return partLocalVariableName;
            }

            switch (member.MemberType)
            {
                case MemberTypes.Method:
                    return string.Format(
                        CultureInfo.InvariantCulture,
                        "new LazyPart<{0}>(() => new {0}({1}.Value.{2}))",
                        GetTypeName(exportDefinition.Contract.Type),
                        partLocalVariableName,
                        member.Name);
                case MemberTypes.Field:
                case MemberTypes.Property:
                    return string.Format(
                        CultureInfo.InvariantCulture,
                        "new LazyPart<{0}>(() => {1}.Value.{2})",
                        GetTypeName(exportDefinition.Contract.Type),
                        partLocalVariableName,
                        member.Name);
                default:
                    throw new NotSupportedException();
            }
        }
    }
}
