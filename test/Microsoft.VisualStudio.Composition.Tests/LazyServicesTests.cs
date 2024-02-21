// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.Composition.Tests
{
    using System;
    using System.Reflection;
    using Xunit;

    public class LazyServicesTests
    {
        [Fact]
        public void IsAnyLazyType()
        {
            Assert.True(typeof(Lazy<string>).IsAnyLazyType());
            Assert.True(typeof(Lazy<string, int>).IsAnyLazyType());
            Assert.True(typeof(Lazy<>).IsAnyLazyType());
            Assert.False(typeof(string).IsAnyLazyType());
        }

        [Fact]
        public void StrongTypeLazyWrapperFactory()
        {
            Func<AssemblyName, Func<object>, object, object> factory = LazyServices.CreateStronglyTypedLazyFactory(typeof(string), null);
            bool executed = false;
            AssemblyName expectedName = this.GetType().Assembly.GetName();
            var lazy = (Lazy<string>)factory(
                expectedName,
                () =>
                {
                    executed = true;
                    return "hi";
                },
                5);
            Assert.IsAssignableFrom<Lazy<string>>(lazy);
            Assert.False(executed);
            Assert.Equal("hi", lazy.Value);
            Assert.True(executed);
        }

        [Fact]
        public void StrongTypeLazyWrapperFactoryWithMetadata()
        {
            Func<AssemblyName, Func<object>, object, object> factory = LazyServices.CreateStronglyTypedLazyFactory(typeof(string), typeof(int));
            bool executed = false;
            AssemblyName expectedName = this.GetType().Assembly.GetName();
            var lazy = (Lazy<string, int>)factory(
                expectedName,
                () =>
                {
                    executed = true;
                    return "hi";
                },
                5);
            Assert.False(executed);
            Assert.Equal(5, lazy.Metadata);
            Assert.False(executed);
            Assert.Equal("hi", lazy.Value);
            Assert.True(executed);
        }
    }
}
