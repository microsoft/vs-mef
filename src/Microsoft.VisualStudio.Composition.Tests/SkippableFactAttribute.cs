namespace Microsoft.VisualStudio.Composition.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Xunit;
    using Xunit.Sdk;

    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public class SkippableFactAttribute : FactAttribute
    {
        protected override IEnumerable<ITestCommand> EnumerateTestCommands(IMethodInfo method)
        {
            foreach (ITestCommand test in base.EnumerateTestCommands(method))
            {
                yield return new SkippableFactCommand(method, test);
            }
        }

        /// <summary>
        /// The exception to throw to register a skipped test.
        /// </summary>
        public class SkipException : Exception
        {
            public SkipException(string reason) : base(reason) { }
        }

        private class SkippableFactCommand : TestCommand
        {
            private readonly ITestCommand command;
            private readonly IMethodInfo method;

            internal SkippableFactCommand(IMethodInfo methodInfo, ITestCommand command)
                : base(methodInfo, command.DisplayName, command.Timeout)
            {
                Requires.NotNull(methodInfo, "methodInfo");
                Requires.NotNull(command, "command");

                this.method = methodInfo;
                this.command = command;
            }

            public override MethodResult Execute(object testClass)
            {
                try
                {
                    return this.command.Execute(testClass);
                }
                catch (SkipException ex)
                {
                    return new SkipResult(this.method, this.DisplayName, ex.Message);
                }
            }
        }
    }
}
