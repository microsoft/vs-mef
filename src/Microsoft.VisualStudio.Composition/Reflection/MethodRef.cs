// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.Composition.Reflection
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Diagnostics;
    using System.Diagnostics.CodeAnalysis;
    using System.Linq;
    using System.Reflection;
    using System.Runtime.InteropServices;
    using System.Text;
    using System.Threading.Tasks;

    [DebuggerDisplay("{" + nameof(DebuggerDisplay) + ",nq}")]
    public class MethodRef : MemberRef, IEquatable<MethodRef>
    {
        /// <summary>
        /// Gets the string to display in the debugger watch window for this value.
        /// </summary>
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        internal virtual string DebuggerDisplay => $"{this.DeclaringType.FullName}.{this.Name}({string.Join(", ", this.ParameterTypes.Select(p => p.FullName))})";

        public MethodRef(TypeRef declaringType, int metadataToken, string name, bool isStatic, ImmutableArray<TypeRef> parameterTypes, ImmutableArray<TypeRef> genericMethodArguments)
            : base(declaringType, metadataToken, isStatic)
        {
            Requires.NotNullOrEmpty(name, nameof(name));
            if (parameterTypes.IsDefault)
            {
                throw new ArgumentNullException(nameof(parameterTypes));
            }

            if (genericMethodArguments.IsDefault)
            {
                throw new ArgumentNullException(nameof(parameterTypes));
            }

            this.ParameterTypes = parameterTypes;
            this.Name = name;
            this.GenericMethodArguments = genericMethodArguments;
        }

        public MethodRef(MethodBase method, Resolver resolver)
            : this(method, resolver, Requires.NotNull(method, nameof(method)).GetParameterTypes(resolver))
        {
        }

        public MethodRef(MethodBase method, Resolver resolver, ImmutableArray<TypeRef> parameterTypes)
            : base(method, resolver)
        {
            Requires.NotNull(method, nameof(method));
            Requires.NotNull(resolver, nameof(resolver));

            this.ParameterTypes = parameterTypes;
            this.Name = method.Name;
            this.GenericMethodArguments = method.GetGenericTypeArguments(resolver);
        }

        protected MethodRef(ConstructorInfo constructor, Resolver resolver)
            : base(constructor, resolver)
        {
            this.ParameterTypes = constructor.GetParameterTypes(resolver);
            this.Name = ConstructorInfo.ConstructorName;
            this.GenericMethodArguments = ImmutableArray<TypeRef>.Empty;
        }

        public MethodBase MethodBase => (MethodBase)this.MemberInfo;

        public MethodBase? MethodBaseNoResolve => (MethodBase?)this.MemberInfoNoResolve;

        protected override MemberInfo Resolve() => ResolverExtensions.Resolve(this);

        public override string Name { get; }

        public ImmutableArray<TypeRef> ParameterTypes { get; }

        public ImmutableArray<TypeRef> GenericMethodArguments { get; }

        [return: NotNullIfNotNull("method")]
        public static MethodRef? Get(MethodBase? method, Resolver resolver) => method != null ? new MethodRef(method, resolver) : null;

        internal override void GetInputAssemblies(ISet<AssemblyName> assemblies)
        {
            Requires.NotNull(assemblies, nameof(assemblies));

            assemblies.Add(this.DeclaringType.AssemblyName);
            foreach (var typeArg in this.GenericMethodArguments)
            {
                typeArg.GetInputAssemblies(assemblies);
            }
        }

        protected override bool EqualsByTypeLocalMetadata(MemberRef other)
        {
            var otherMethod = (MethodRef)other;

            return this.Name == otherMethod.Name
                && this.ParameterTypes.EqualsByValue(otherMethod.ParameterTypes)
                && this.GenericMethodArguments.EqualsByValue(otherMethod.GenericMethodArguments);
        }

        public override int GetHashCode() => this.DeclaringType.GetHashCode() + this.Name.GetHashCode();

        public bool Equals(MethodRef? other) => this.Equals((MemberRef?)other);
    }
}
