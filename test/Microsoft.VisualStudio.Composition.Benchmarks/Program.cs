// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using BenchmarkDotNet.Running;
using Microsoft.VisualStudio.Composition.Benchmarks;

BenchmarkSwitcher.FromAssembly(typeof(CompositionBenchmarks).Assembly).Run(args);
