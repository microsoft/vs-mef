namespace Microsoft.VisualStudio.Composition
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using System.Text;
    using System.Threading.Tasks;
    using Validation;

    public class Import : IEquatable<Import>
    {
        public Import(ComposablePartDefinition partDefinition, ImportDefinition importDefinition, MemberInfo importingMember)
        {
            Requires.NotNull(partDefinition, "partDefinition");
            Requires.NotNull(importDefinition, "importDefinition");
            Requires.NotNull(importingMember, "importingMember");

            this.PartDefinition = partDefinition;
            this.ImportDefinition = importDefinition;
            this.ImportingMember = importingMember;
        }

        public ImportDefinition ImportDefinition { get; private set; }

        public ComposablePartDefinition PartDefinition { get; private set; }

        public MemberInfo ImportingMember { get; private set; }

        public override int GetHashCode()
        {
            return this.ImportDefinition.GetHashCode() + this.PartDefinition.GetHashCode();
        }

        public override bool Equals(object obj)
        {
            return this.Equals(obj as Import);
        }

        public bool Equals(Import other)
        {
            return this.ImportDefinition.Equals(other.ImportDefinition)
                && this.PartDefinition.Equals(other.PartDefinition)
                && this.ImportingMember.Equals(other.ImportingMember);
        }
    }
}
