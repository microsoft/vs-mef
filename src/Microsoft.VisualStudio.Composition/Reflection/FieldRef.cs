﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.Composition.Reflection
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using MessagePack;
    using System.Reflection;
    using MessagePack;

    [DebuggerDisplay("{" + nameof(DebuggerDisplay) + ",nq}")]
    //    [MessagePackObject(true)]
    [MessagePackFormatter(typeof(MemberRefFormatter<FieldRef>))]

    public class FieldRef : MemberRef, IEquatable<FieldRef>
    {
        /// <summary>
        /// Gets the string to display in the debugger watch window for this value.
        /// </summary>
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
      //  [Key(0)]
        private string DebuggerDisplay => $"{this.DeclaringType.FullName}.{this.Name}";

        public FieldRef(TypeRef declaringType, TypeRef fieldTypeRef, int metadataToken, string name, bool isStatic)
            : base(declaringType, metadataToken, isStatic)
        {
            Requires.NotNullOrEmpty(name, nameof(name));
            this.Name = name;
            this.FieldTypeRef = fieldTypeRef;
        }

        public FieldRef(FieldInfo field, Resolver resolver)
            : base(field, resolver)
        {
            this.Name = field.Name;
            this.FieldTypeRef = TypeRef.Get(field.FieldType, resolver);
        }

       // [Key(6)]
        public FieldInfo FieldInfo => (FieldInfo)this.MemberInfo;

        //[Key(7)]
        public TypeRef FieldTypeRef { get; }

        //[Key(8)]
        public override string Name { get; }

        internal override void GetInputAssemblies(ISet<AssemblyName> assemblies) => this.DeclaringType?.GetInputAssemblies(assemblies);

        protected override bool EqualsByTypeLocalMetadata(MemberRef other)
        {
            var otherField = (FieldRef)other;
            return this.Name == otherField.Name;
        }

        public bool Equals(FieldRef? fieldRef) => this.Equals((MemberRef?)fieldRef);

        public override int GetHashCode()
        {
            return this.DeclaringType.GetHashCode() + this.Name.GetHashCode();
        }

        protected override MemberInfo Resolve() => ResolverExtensions.Resolve(this);
    }
}
