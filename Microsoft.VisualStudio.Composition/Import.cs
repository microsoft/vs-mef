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
        /// <summary>
        /// Initializes a new instance of the <see cref="Import"/> class
        /// to represent an importing member.
        /// </summary>
        public Import(ComposablePartDefinition partDefinition, ImportDefinition importDefinition, MemberInfo importingMember)
        {
            Requires.NotNull(partDefinition, "partDefinition");
            Requires.NotNull(importDefinition, "importDefinition");
            Requires.NotNull(importingMember, "importingMember");

            this.PartDefinition = partDefinition;
            this.ImportDefinition = importDefinition;
            this.ImportingMember = importingMember;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Import"/> class
        /// to represent a parameter in an importing constructor.
        /// </summary>
        public Import(ComposablePartDefinition partDefinition, ImportDefinition importDefinition)
        {
            Requires.NotNull(partDefinition, "partDefinition");
            Requires.NotNull(importDefinition, "importDefinition");

            this.PartDefinition = partDefinition;
            this.ImportDefinition = importDefinition;
        }

        public ImportDefinition ImportDefinition { get; private set; }

        public ComposablePartDefinition PartDefinition { get; private set; }

        /// <summary>
        /// Gets the members this import is found on. Null for importing constructors.
        /// </summary>
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
            if (other == null)
            {
                return false;
            }

            return this.ImportDefinition.Equals(other.ImportDefinition)
                && this.PartDefinition.Equals(other.PartDefinition)
                && EqualityComparer<MemberInfo>.Default.Equals(this.ImportingMember, other.ImportingMember);
        }
    }
}
