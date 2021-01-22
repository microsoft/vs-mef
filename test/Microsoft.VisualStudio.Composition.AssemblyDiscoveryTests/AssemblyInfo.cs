// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

// This is an important part of testing. We need to know that our ability to handle
// internal types can span assemblies when, for example, an internal type in one assembly
// references an internal type in another assembly.
[assembly: InternalsVisibleTo("Microsoft.VisualStudio.Composition.Tests, PublicKey=" + ThisAssembly.PublicKey)]
