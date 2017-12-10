// Copyright (c) Microsoft. All rights reserved.

using System.Runtime.CompilerServices;

// This is an important part of testing. We need to know that our ability to handle
// internal types can span assemblies when, for example, an internal type in one assembly
// references an internal type in another assembly.
[assembly: InternalsVisibleTo("Microsoft.VisualStudio.Composition.Tests, PublicKey=" + ThisAssembly.PublicKey)]
