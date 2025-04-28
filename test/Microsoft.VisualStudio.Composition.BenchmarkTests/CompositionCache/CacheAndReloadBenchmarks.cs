// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.Composition.BenchmarkTests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Security.Cryptography;
    using System.Text;
    using System.Threading.Tasks;
    using BenchmarkDotNet.Attributes;
    using BenchmarkDotNet.Running;
    using Microsoft.VisualStudio.Composition.Tests;
    using Microsoft.VSDiagnostics;
    using Xunit.Abstractions;
    using static System.Runtime.InteropServices.JavaScript.JSType;
    using static Microsoft.VisualStudio.Composition.Tests.AssemblyReferencingTests;

    [CPUUsageDiagnoser]
    public class CacheAndReloadBenchmarks
    {
        private CacheAndReloadBenchmarkHost cacheBenchmarkHost = new CacheAndReloadBenchmarkHost();

        [Benchmark]
        public Task CacheAndReloadAsync()
        {
            return this.cacheBenchmarkHost.CacheAndReloadAsync();
        }
    }
}
