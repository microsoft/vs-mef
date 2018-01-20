// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.VisualStudio.Composition.Reflection
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Reflection;

    [DebuggerDisplay("{" + nameof(DebuggerDisplay) + ",nq}")]
    public class FieldRef : MemberRef, IEquatable<FieldRef>
    {
        /// <summary>
        /// Gets the string to display in the debugger watch window for this value.
        /// </summary>
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private string DebuggerDisplay => $"{this.DeclaringType.FullName}.{this.Name}";

        public FieldRef(TypeRef declaringType, int metadataToken, string name)
            : base(declaringType, metadataToken)
        {
            Requires.NotNullOrEmpty(name, nameof(name));
            this.Name = name;
        }

        public FieldRef(FieldInfo field, Resolver resolver)
            : base(field, resolver)
        {
            this.Name = field.Name;
        }

        public FieldInfo FieldInfo => (FieldInfo)this.MemberInfo;

        public string Name { get; private set; }

        internal override void GetInputAssemblies(ISet<AssemblyName> assemblies) => this.DeclaringType?.GetInputAssemblies(assemblies);

        protected override bool EqualsByTypeLocalMetadata(MemberRef other)
        {
            var otherField = (FieldRef)other;
            return this.Name == otherField.Name;
        }

        public bool Equals(FieldRef fieldRef) => this.Equals((MemberRef)fieldRef);

        public override int GetHashCode()
        {
            return this.DeclaringType.GetHashCode() + this.Name.GetHashCode();
        }

        protected override MemberInfo Resolve() => ResolverExtensions.Resolve(this);
    }
}
