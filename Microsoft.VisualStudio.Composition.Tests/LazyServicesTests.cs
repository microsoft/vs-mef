namespace Microsoft.VisualStudio.Composition.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Xunit;

    public class LazyServicesTests
    {
        [Fact]
        public void FromValueOfObject()
        {
            object expectedValue = new object();
            Lazy<object> lazy = LazyServices.FromValue(expectedValue);
            Assert.Same(expectedValue, lazy.Value);
        }

        [Fact]
        public void FromValueOfObjectWithMetadata()
        {
            int metadata = 5;
            object expectedValue = new object();
            Lazy<object, int> lazy = LazyServices.FromValue(expectedValue, metadata);
            Assert.Equal(metadata, lazy.Metadata);
            Assert.Same(expectedValue, lazy.Value);
        }

        [Fact]
        public void FromValueOfT()
        {
            string expectedValue = "hi";
            Lazy<string> lazy = LazyServices.FromValue(expectedValue);
            Assert.Same(expectedValue, lazy.Value);
        }

        [Fact]
        public void FromValueOfTWithMetadata()
        {
            int metadata = 5;
            string expectedValue = "hi";
            Lazy<string, int> lazy = LazyServices.FromValue(expectedValue, metadata);
            Assert.Equal(metadata, lazy.Metadata);
            Assert.Same(expectedValue, lazy.Value);
        }

        [Fact]
        public void FromFactory()
        {
            Lazy<string> lazy = LazyServices.FromFactory(v => v + "ha!", "Ha");
            Assert.Equal("Haha!", lazy.Value);
        }

        [Fact]
        public void FromFactoryWithMetadata()
        {
            int metadata = 5;
            Lazy<string, int> lazy = LazyServices.FromFactory(v => v + "ha!", "Ha", metadata);
            Assert.Equal(metadata, lazy.Metadata);
            Assert.Equal("Haha!", lazy.Value);
        }

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
            Func<Func<object>, object, object> factory = LazyServices.CreateStronglyTypedLazyFactory(typeof(string), null);
            bool executed = false;
            var lazy = (Lazy<string>)factory(() => { executed = true; return "hi"; }, 5);
            Assert.IsType(typeof(Lazy<string>), lazy);
            Assert.False(executed);
            Assert.Equal("hi", lazy.Value);
            Assert.True(executed);
        }

        [Fact]
        public void StrongTypeLazyWrapperFactoryWithMetadata()
        {
            Func<Func<object>, object, object> factory = LazyServices.CreateStronglyTypedLazyFactory(typeof(string), typeof(int));
            bool executed = false;
            var lazy = (Lazy<string, int>)factory(() => { executed = true; return "hi"; }, 5);
            Assert.False(executed);
            Assert.Equal(5, lazy.Metadata);
            Assert.False(executed);
            Assert.Equal("hi", lazy.Value);
            Assert.True(executed);
        }
    }
}
