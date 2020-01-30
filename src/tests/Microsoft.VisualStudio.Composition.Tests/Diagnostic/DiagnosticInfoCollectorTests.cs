// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.VisualStudio.Composition.Tests.Diagnostic
{
    using Microsoft.VisualStudio.Composition.Diagnostic;
    using Xunit;

    public class DiagnosticInfoCollectorTests
    {
        [Fact]
        public void CollectAllInformation()
        {
            var diagnosticInfoCollector = DiagnosticInfoCollector.CreateInstance();

            diagnosticInfoCollector.Collect("Information1");

            var informationCollected = diagnosticInfoCollector.GetAllInformation();

            Assert.True(informationCollected != null && informationCollected.Contains("Information1"));
        }

        [Fact]
        public void InforamtionShouldHaveNewLineSymbol()
        {
            var diagnosticInfoCollector = DiagnosticInfoCollector.CreateInstance();

            diagnosticInfoCollector.Collect("Information1");
            diagnosticInfoCollector.Collect("Information2");

            var informationCollected = diagnosticInfoCollector.GetAllInformation();

            Assert.True(informationCollected != null && informationCollected.Contains("\n"));
        }
    }
}