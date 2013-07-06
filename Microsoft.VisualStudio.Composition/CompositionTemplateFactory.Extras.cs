namespace Microsoft.VisualStudio.Composition
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
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
            var exports = satisfyingExport.Value;
            string fullTypeNameWithPerhapsLazy = GetTypeName(importDefinition.CoercedValueType);
            if (importDefinition.IsLazy)
            {
                fullTypeNameWithPerhapsLazy = "Lazy<" + fullTypeNameWithPerhapsLazy + ">";
            }

            string left = "result." + importingMember.Name;
            string right = null;
            if (importDefinition.Cardinality == ImportCardinality.ZeroOrMore)
            {
                right = "new List<" + fullTypeNameWithPerhapsLazy + "> {";
                this.PushIndent("\t");
                foreach (var export in exports)
                {
                    right += Environment.NewLine + this.CurrentIndent;
                    if (importDefinition.IsLazy) { right += "new " + fullTypeNameWithPerhapsLazy + "(() => "; }
                    right += "this." + GetPartFactoryMethodName(export.PartDefinition, importDefinition.Contract.Type.GetGenericArguments().Select(GetTypeName).ToArray()) + "()";
                    if (importDefinition.IsLazy) { right += ")"; }
                    right += ",";
                }

                this.PopIndent();
                right += Environment.NewLine + this.CurrentIndent + "}";
                if (importDefinition.IsLazy)
                {
                }
            }
            else if (exports.Any())
            {
                right = "this." + GetPartFactoryMethodName(exports.Single().PartDefinition, importDefinition.Contract.Type.GetGenericArguments().Select(GetTypeName).ToArray()) + "()";
                if (importDefinition.IsLazy)
                {
                    right = "new " + fullTypeNameWithPerhapsLazy + "(() => " + right + ")";
                }
            }

            if (right != null)
            {
                this.Write(this.CurrentIndent);
                this.WriteLine("{0} = {1};", left, right);
            }
        }

        private void EmitInstantiatePart(ComposablePart part)
        {
            this.Write(this.CurrentIndent);
            this.WriteLine("var result = new {0}();", GetTypeName(part.Definition.Type));
            foreach (var satisfyingExport in part.SatisfyingExports)
            {
                this.EmitImportSatisfyingAssignment(satisfyingExport);
            }

            this.Write(this.CurrentIndent);
            this.WriteLine("return result;");
        }

        private static string GetTypeName(Type type)
        {
            return GetTypeName(type, genericTypeDefinition: false);
        }

        private static string GetTypeName(Type type, bool genericTypeDefinition)
        {
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
                string[] typeArguments = type.GetGenericArguments().Select(t => t.Name).ToArray();
                result += ReplaceBackTickWithTypeArgs(type.DeclaringType == null ? type.FullName : type.Name, typeArguments);
            }

            return result;
        }

        private static string FilterTypeNameForGenericTypeDefinition(Type type, bool fullName)
        {
            Requires.NotNull(type, "type");

            string name = fullName ? type.FullName : type.Name;
            if (type.IsGenericType)
            {
                name = name.Substring(0, type.Name.IndexOf('`'));
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
            if (originalName.IndexOf('`') >= 0)
            {
                name = originalName.Substring(0, name.IndexOf('`'));
                name += "<";
                int typeArgumentsCount = int.Parse(originalName.Substring(originalName.IndexOf('`') + 1), CultureInfo.InvariantCulture);
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
    }
}
