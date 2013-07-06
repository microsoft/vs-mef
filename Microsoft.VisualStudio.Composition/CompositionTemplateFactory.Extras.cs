namespace Microsoft.VisualStudio.Composition
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

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
                    right += "this.GetOrCreate" + export.PartDefinition.Id + "()";
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
                right = "this.GetOrCreate" + exports.Single().PartDefinition.Id + "()";
                if (importDefinition.IsLazy)
                {
                    right = "new " + fullTypeNameWithPerhapsLazy + "(() => " + right + ")";
                }

                this.Write(this.CurrentIndent);
                this.WriteLine("{0} = {1};", left, right);
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
            if (type.DeclaringType != null)
            {
                return GetTypeName(type.DeclaringType) + "." + type.Name;
            }
            else
            {
                return type.FullName;
            }
        }
    }
}
