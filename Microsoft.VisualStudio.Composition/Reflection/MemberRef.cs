namespace Microsoft.VisualStudio.Composition.Reflection
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

    public struct MemberRef : IEquatable<MemberRef>
    {
        public MemberRef(ConstructorRef constructor)
            : this()
        {
            this.Constructor = constructor;
        }

        public MemberRef(FieldRef field)
            : this()
        {
            this.Field = field;
        }

        public MemberRef(PropertyRef property)
            : this()
        {
            this.Property = property;
        }

        public MemberRef(MethodRef method)
            : this()
        {
            this.Method = method;
        }

        public ConstructorRef Constructor { get; private set; }

        public FieldRef Field { get; private set; }

        public PropertyRef Property { get; private set; }

        public MethodRef Method { get; private set; }

        public bool IsEmpty
        {
            get { return this.Constructor.IsEmpty && this.Field.IsEmpty && this.Property.IsEmpty && this.Method.IsEmpty; }
        }

        public bool IsConstructor
        {
            get { return !this.Constructor.IsEmpty; }
        }

        public bool IsField
        {
            get { return !this.Field.IsEmpty; }
        }

        public bool IsProperty
        {
            get { return !this.Property.IsEmpty; }
        }

        public bool IsMethod
        {
            get { return !this.Method.IsEmpty; }
        }

        public bool Equals(MemberRef other)
        {
            return this.Constructor.Equals(other.Constructor)
                && this.Field.Equals(other.Field)
                && this.Property.Equals(other.Property)
                && this.Method.Equals(other.Method);
        }

        public override int GetHashCode()
        {
            return
                this.IsField ? this.Field.GetHashCode() :
                this.IsProperty ? this.Property.GetHashCode() :
                this.IsMethod ? this.Method.GetHashCode() :
                this.IsConstructor ? this.Constructor.GetHashCode() :
                0;
        }

        public override bool Equals(object obj)
        {
            return obj is MemberRef && this.Equals((MemberRef)obj);
        }
    }
}
