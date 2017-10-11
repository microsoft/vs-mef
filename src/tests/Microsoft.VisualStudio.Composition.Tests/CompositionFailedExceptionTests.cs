// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.VisualStudio.Composition.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Composition;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Xunit;

    public class CompositionFailedExceptionTests
    {
#if DESKTOP
        [Fact(Skip = "Not yet implemented.")]
        public void ExceptionIsSerializable()
        {
            var discovery = TestUtilities.V2Discovery;
            var catalog = TestUtilities.EmptyCatalog.AddParts(new[] { discovery.CreatePart(typeof(Tree)) });
            var configuration = CompositionConfiguration.Create(catalog);

            CompositionFailedException exception = null;
            try
            {
                configuration.ThrowOnErrors();
                Assert.True(false, "Expected exception not thrown.");
            }
            catch (CompositionFailedException ex)
            {
                exception = ex;
            }

            var formatter = new System.Runtime.Serialization.Formatters.Binary.BinaryFormatter();
            var ms = new MemoryStream();
            formatter.Serialize(ms, exception);

            ms.Position = 0;
            var actual = (CompositionFailedException)formatter.Deserialize(ms);
            Assert.Equal(exception.Message, actual.Message);
            Assert.NotNull(actual.Errors);
            Assert.False(actual.Errors.IsEmpty);
            Assert.Equal(1, actual.Errors.Peek().Count);
            Assert.Equal(exception.Errors.Peek().Single().Message, actual.Errors.Peek().Single().Message);
        }
#endif

        [Export]
        public class Tree
        {
            [Import("Fruit")] // not satisfied
            public object Fruit { get; set; }
        }
    }
}
