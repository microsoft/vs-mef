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
        public void FromValueOfT()
        {
            string expectedValue = "hi";
            Func<string> func = DelegateServices.FromValue(expectedValue);
            Assert.Same(expectedValue, func());
        }

        [Fact]
        public void FromValue()
        {
            object expectedValue = new object();
            Func<object> func = DelegateServices.FromValue(expectedValue);
            Assert.Same(expectedValue, func());
        }

        private static int executed = 0;

        [Fact]
        public void PresupplyArgument()
        {
            executed = 0;
            Func<string, int> getLength = v => { executed++; return v.Length; };
            Func<int> getLengthOfFive = getLength.PresupplyArgument("five");
            Assert.Equal(0, executed);
            Assert.Equal(4, getLengthOfFive());
            Assert.Equal(1, executed);
            Assert.Equal(4, getLengthOfFive());
            Assert.Equal(2, executed);
        }

        [Fact]
        public void PresupplyArgumentOnMethodWithClosure()
        {
            int local = 0;
            Func<string, int> getLength = v => { local++; return v.Length; };

            Assert.Throws<ArgumentException>(() => getLength.PresupplyArgument("hi"));
        }
    }
}
