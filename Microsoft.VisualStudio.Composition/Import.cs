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

        /// <summary>
        /// Initializes a new instance of the <see cref="Import"/> class
        /// to represent an imperative query into the container (no importing part).
        /// </summary>
        public Import(ImportDefinition importDefinition)
        {
            Requires.NotNull(importDefinition, "importDefinition");

            this.ImportDefinition = importDefinition;
        }

        /// <summary>
        /// Gets the definition for this import.
        /// </summary>
        public ImportDefinition ImportDefinition { get; private set; }

        /// <summary>
        /// Gets the part definition on which this import is found.
        /// </summary>
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
