// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.VisualStudio.Composition.Reflection
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Diagnostics;
    using System.Linq;
    using System.Reflection;
    using System.Runtime.InteropServices;
    using System.Text;
    using System.Threading.Tasks;

    [DebuggerDisplay("{" + nameof(DebuggerDisplay) + ",nq}")]
    [StructLayout(LayoutKind.Auto)] // Workaround multi-core JIT deadlock (DevDiv.1043199)
    public struct MethodRef : IEquatable<MethodRef>
    {
        /// <summary>
        /// Gets the string to display in the debugger watch window for this value.
        /// </summary>
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        internal string DebuggerDisplay => this.IsEmpty ? "(empty)" : $"{this.DeclaringType.FullName}.{this.Name}({string.Join(", ", this.ParameterTypes.Select(p => p.FullName))})";

        /// <summary>
        /// The metadata token for this member if read from a persisted assembly.
        /// We do not store metadata tokens for members in dynamic assemblies because they can change till the Type is closed.
        /// </summary>
        private readonly int? metadataToken;

        /// <summary>
        /// The <see cref="MemberInfo"/> that this value was instantiated with,
        /// or cached later when a metadata token was resolved.
        /// </summary>
        private MethodBase methodBase;

        public MethodRef(TypeRef declaringType, int metadataToken, string name, ImmutableArray<TypeRef> parameterTypes, ImmutableArray<TypeRef> genericMethodArguments)
               : this()
        {
            Requires.NotNullOrEmpty(name, nameof(name));

            this.DeclaringType = declaringType;
            this.metadataToken = metadataToken;
            this.ParameterTypes = parameterTypes;
            this.Name = name;
            this.GenericMethodArguments = genericMethodArguments;
        }

#if DESKTOP
        [Obsolete]
        public MethodRef(TypeRef declaringType, int metadataToken, ImmutableArray<TypeRef> genericMethodArguments)
            : this(
                  declaringType,
                  metadataToken,
                  declaringType.Resolve().Assembly.ManifestModule.ResolveMethod(metadataToken).Name,
                  declaringType.Resolve().Assembly.ManifestModule.ResolveMethod(metadataToken).GetParameterTypes(declaringType.Resolver),
                  genericMethodArguments)
        {
        }
#endif

        public MethodRef(MethodInfo method, Resolver resolver)
            : this((MethodBase)method, resolver)
        {
        }

        public MethodRef(MethodBase method, Resolver resolver)
            : this(method, resolver, Requires.NotNull(method, nameof(method)).GetParameterTypes(resolver))
        {
        }

        public MethodRef(MethodBase method, Resolver resolver, ImmutableArray<TypeRef> parameterTypes)
            : this()
        {
            Requires.NotNull(method, nameof(method));
            Requires.NotNull(resolver, nameof(resolver));

            this.DeclaringType = TypeRef.Get(method.DeclaringType, resolver);
            this.ParameterTypes = parameterTypes;
            this.Name = method.Name;
            this.GenericMethodArguments = method.GetGenericTypeArguments(resolver);
            this.methodBase = method;
        }

        public MethodRef(ConstructorRef constructor)
            : this(constructor.DeclaringType, constructor.MetadataToken, ConstructorInfo.ConstructorName, constructor.ParameterTypes, ImmutableArray<TypeRef>.Empty)
        {
            this.methodBase = constructor.ConstructorInfoNoResolve;
        }

        public TypeRef DeclaringType { get; private set; }

        public int MetadataToken
        {
            get
            {
                if (this.metadataToken.HasValue)
                {
                    return this.metadataToken.Value;
                }

#if DESKTOP
                // Avoid calling MemberInfo.MetadataToken on MethodBuilders because they throw exceptions
                if (this.methodBase is System.Reflection.Emit.MethodBuilder mb)
                {
                    return mb.GetToken().Token;
                }
#endif

                return this.methodBase?.MetadataToken ?? 0;
            }
        }

        public MethodBase MethodBase => this.methodBase ?? (this.methodBase = this.Resolve2());

        public string Name { get; private set; }

        public ImmutableArray<TypeRef> ParameterTypes { get; private set; }

        public ImmutableArray<TypeRef> GenericMethodArguments { get; private set; }

        public bool IsEmpty
        {
            get { return this.DeclaringType == null; }
        }

        internal Resolver Resolver => this.DeclaringType?.Resolver;

        public static MethodRef Get(MethodInfo method, Resolver resolver) => Get((MethodBase)method, resolver);

        public static MethodRef Get(MethodBase method, Resolver resolver) => method != null ? new MethodRef(method, resolver) : default(MethodRef);

        public bool Equals(MethodRef other)
        {
            if (this.IsEmpty ^ other.IsEmpty)
            {
                return false;
            }

            if (this.IsEmpty)
            {
                return true;
            }

            if (this.methodBase != null && other.methodBase != null)
            {
                if (this.methodBase == other.methodBase)
                {
                    return true;
                }
            }

            if (!EqualityComparer<TypeRef>.Default.Equals(this.DeclaringType, other.DeclaringType))
            {
                return false;
            }

            if (this.metadataToken.HasValue && other.metadataToken.HasValue && this.DeclaringType.AssemblyId.Equals(other.DeclaringType.AssemblyId))
            {
                if (this.metadataToken.Value != other.metadataToken.Value)
                {
                    return false;
                }
            }
            else
            {
                if (this.Name != other.Name || !this.ParameterTypes.EqualsByValue(other.ParameterTypes))
                {
                    return false;
                }
            }

            return this.GenericMethodArguments.EqualsByValue(other.GenericMethodArguments);
        }

        public override int GetHashCode()
        {
            return this.DeclaringType.GetHashCode() + this.Name.GetHashCode();
        }

        public override bool Equals(object obj)
        {
            return obj is MethodRef method && this.Equals(method);
        }
    }
}
