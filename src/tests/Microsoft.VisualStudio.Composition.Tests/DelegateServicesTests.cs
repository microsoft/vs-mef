// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.VisualStudio.Composition.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Xunit;

    public class DelegateServicesTests
    {
        [Fact]
        public void FromValue()
        {
            string expectedValue = "hi";
            Func<string> func = DelegateServices.FromValue(expectedValue);
            Assert.Same(expectedValue, func());
        }

        [Fact]
        public void As()
        {
            Func<object> fo = () => 5;
            Func<int> fi = fo.As<int>();
            int result = fi();
            Assert.Equal(5, result);
        }
    }
}
