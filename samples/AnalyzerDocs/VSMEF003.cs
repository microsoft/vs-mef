// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable disable

using System.Composition;

namespace Samples.AnalyzerDocs.VSMEF003
{
#pragma warning disable VSMEF003
    namespace Defective
    {
        #region Defective
        interface ICalculator
        {
            int Add(int a, int b);
        }

        [Export(typeof(ICalculator))] // ❌ Violates VSMEF003
        public class TextProcessor
        {
            public string ProcessText(string input) => input.ToUpper();
        }
        #endregion
    }
#pragma warning restore VSMEF003

    namespace Fixed
    {
        #region Fix
        interface ICalculator
        {
            int Add(int a, int b);
        }

        [Export(typeof(ICalculator))] // ✅ OK
        public class Calculator1 : ICalculator
        {
            public int Add(int a, int b) => a + b;
        }

        [Export] // ✅ OK - exports Calculator type
        public class Calculator2
        {
            public int Add(int a, int b) => a + b;
        }
        #endregion
    }
}
