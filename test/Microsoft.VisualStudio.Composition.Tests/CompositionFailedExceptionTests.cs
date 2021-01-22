// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

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
        [Fact]
        public void ExceptionIsSerializable()
        {
            var discovery = TestUtilities.V2Discovery;
            var catalog = TestUtilities.EmptyCatalog.AddParts(new[] { discovery.CreatePart(typeof(Tree))! });
            var configuration = CompositionConfiguration.Create(catalog);

            CompositionFailedException? exception = null;
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
            Assert.Equal(exception!.Message, actual.Message);

            Assert.Equal(exception.ErrorsAsString, actual.ErrorsAsString);

            // At present, we do not implement serialization of the Errors collection.
            Assert.Null(actual.Errors);
            ////Assert.False(actual.Errors!.IsEmpty);
            ////Assert.Equal(1, actual.Errors.Peek().Count);
            ////Assert.Equal(exception.Errors!.Peek().Single().Message, actual.Errors.Peek().Single().Message);
        }

        [Export]
        public class Tree
        {
            [Import("Fruit")] // not satisfied
            public object Fruit { get; set; } = null!;
        }
    }
}
