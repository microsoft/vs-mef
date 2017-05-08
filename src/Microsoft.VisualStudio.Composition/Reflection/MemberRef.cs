// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.VisualStudio.Composition.Reflection
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using System.Runtime.InteropServices;
    using System.Text;
    using System.Threading.Tasks;

    [StructLayout(LayoutKind.Auto)] // Workaround multi-core JIT deadlock (DevDiv.1043199)
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

        public MemberRef(TypeRef type)
            : this()
        {
            this.Type = type;
        }

        public MemberRef(MemberInfo member, Resolver resolver)
            : this()
        {
            Requires.NotNull(member, nameof(member));

            switch (member.MemberType)
            {
                case MemberTypes.Constructor:
                    this.Constructor = new ConstructorRef((ConstructorInfo)member, resolver);
                    break;
                case MemberTypes.Field:
                    this.Field = new FieldRef((FieldInfo)member, resolver);
                    break;
                case MemberTypes.Method:
                    this.Method = new MethodRef((MethodInfo)member, resolver);
                    break;
                case MemberTypes.Property:
                    this.Property = new PropertyRef((PropertyInfo)member, resolver);
                    break;
                default:
                    if (member is TypeInfo typeInfo)
                    {
                        this.Type = TypeRef.Get(typeInfo.AsType(), resolver);
                    }
                    else
                    {
                        throw new NotSupportedException();
                    }

                    break;
            }
        }

        public ConstructorRef Constructor { get; private set; }

        public FieldRef Field { get; private set; }

        public PropertyRef Property { get; private set; }

        public MethodRef Method { get; private set; }

        public TypeRef Type { get; private set; }

        public TypeRef DeclaringType
        {
            get
            {
                if (this.IsProperty)
                {
                    return this.Property.DeclaringType;
                }
                else if (this.IsField)
                {
                    return this.Field.DeclaringType;
                }
                else if (this.IsConstructor)
                {
                    return this.Constructor.DeclaringType;
                }
                else if (this.IsMethod)
                {
                    return this.Method.DeclaringType;
                }
                else if (this.IsType)
                {
                    throw new NotSupportedException();
                }
                else
                {
                    return null;
                }
            }
        }

        public bool IsEmpty
        {
            get { return this.Constructor.IsEmpty && this.Field.IsEmpty && this.Property.IsEmpty && this.Method.IsEmpty && this.Type == null; }
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

        public bool IsType
        {
            get { return this.Type != null; }
        }

        internal Resolver Resolver => this.DeclaringType?.Resolver;

        public static MemberRef Get(MemberInfo member, Resolver resolver)
        {
            return member != null ? new MemberRef(member, resolver) : default(MemberRef);
        }

        public bool Equals(MemberRef other)
        {
            return this.Constructor.Equals(other.Constructor)
                && this.Field.Equals(other.Field)
                && this.Property.Equals(other.Property)
                && this.Method.Equals(other.Method)
                && EqualityComparer<TypeRef>.Default.Equals(this.Type, other.Type);
        }

        public override int GetHashCode()
        {
            return
                this.IsField ? this.Field.GetHashCode() :
                this.IsProperty ? this.Property.GetHashCode() :
                this.IsMethod ? this.Method.GetHashCode() :
                this.IsConstructor ? this.Constructor.GetHashCode() :
                this.IsType ? this.Type.GetHashCode() :
                0;
        }

        public override bool Equals(object obj)
        {
            return obj is MemberRef && this.Equals((MemberRef)obj);
        }
    }
}
