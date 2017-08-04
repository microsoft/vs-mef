// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.VisualStudio.Composition.Tests.Reflection
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Linq;
    using System.Reflection;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.VisualStudio.Composition.Reflection;
    using Xunit;

    public class MethodRefTests
    {
        [Fact]
        public void CtorInfo_FromInstance()
        {
            ConstructorInfo ctor = this.GetType().GetTypeInfo().GetConstructor(Type.EmptyTypes);
            var methodRef = new MethodRef(ctor, Resolver.DefaultInstance);
            Assert.Same(ctor, methodRef.MethodBase);
        }

        [Fact]
        public void CtorInfo_FromMetadataToken()
        {
            ConstructorInfo ctor = this.GetType().GetTypeInfo().GetConstructor(Type.EmptyTypes);
            var methodRef = new MethodRef(TypeRef.Get(this.GetType(), Resolver.DefaultInstance), ctor.MetadataToken, ConstructorInfo.ConstructorName, ImmutableArray<TypeRef>.Empty, ImmutableArray<TypeRef>.Empty);
            Assert.Same(ctor, methodRef.MethodBase);
        }

        [Fact]
        public void MethodInfo_FromInstance()
        {
            MethodInfo methodInfo = this.GetType().GetTypeInfo().GetMethod(nameof(this.MethodInfo_FromInstance));
            var methodRef = new MethodRef(methodInfo, Resolver.DefaultInstance);
            Assert.Same(methodInfo, methodRef.MethodBase);
        }

        [Fact]
        public void MethodInfo_FromMetadataToken()
        {
            MethodInfo methodInfo = this.GetType().GetTypeInfo().GetMethod(nameof(this.MethodInfo_FromInstance));
            var methodRef = new MethodRef(TypeRef.Get(this.GetType(), Resolver.DefaultInstance), methodInfo.MetadataToken, methodInfo.Name, ImmutableArray<TypeRef>.Empty, ImmutableArray<TypeRef>.Empty);
            Assert.Same(methodInfo, methodRef.MethodBase);
        }
    }
}
