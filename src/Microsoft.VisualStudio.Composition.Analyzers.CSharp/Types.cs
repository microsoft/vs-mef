// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.Composition.Analyzers.CSharp;

internal static class Types
{
    internal static class ImportAttribute
    {
        internal const string Name = "ImportAttribute";
        internal const string FullName = $"System.ComponentModel.Composition.{Name}";
    }

    internal static class ImportManyAttribute
    {
        internal const string Name = "ImportManyAttribute";
        internal const string FullName = $"System.ComponentModel.Composition.{Name}";
    }

    internal static class ImportAttributeV2
    {
        internal const string Name = "ImportAttribute";
        internal const string FullName = $"System.Composition.{Name}";
    }

    internal static class ImportingConstructorAttribute
    {
        internal const string Name = "ImportingConstructorAttribute";
        internal const string FullName = $"System.ComponentModel.Composition.{Name}";
    }

    internal static class ImportingConstructorAttributeV2
    {
        internal const string Name = "ImportingConstructorAttribute";
        internal const string FullName = $"System.Composition.{Name}";
    }

    internal static class MetadataViewImplementationAttribute
    {
        internal const string Name = "MetadataViewImplementationAttribute";
        internal const string FullName = $"System.ComponentModel.Composition.{Name}";
        internal const string QualifiedName = $"global::{FullName}";
    }

    internal static class MetadataViewAttribute
    {
        internal const string Name = "MetadataViewAttribute";
        internal const string FullName = $"Microsoft.VisualStudio.Composition.{Name}";
        internal const string QualifiedName = $"global::{FullName}";
    }

    internal static class MetadataView
    {
        internal const string Name = "MetadataView";
        internal const string FullName = $"Microsoft.VisualStudio.Composition.{Name}";
        internal const string QualifiedName = $"global::{FullName}";
    }

    internal static class Lazy
    {
        internal const string Name = "Lazy`2";
        internal const string FullName = "System.Lazy`2";
    }

    internal static class ExportFactory
    {
        internal const string Name = "ExportFactory`2";
    }

    internal static class GeneratedCodeAttribute
    {
        internal const string Name = "GeneratedCodeAttribute";
        internal const string FullName = $"System.CodeDom.Compiler.{Name}";
        internal const string QualifiedName = $"global::{FullName}";
    }

    internal static class ObsoleteAttribute
    {
        internal const string Name = "ObsoleteAttribute";
        internal const string FullName = $"System.{Name}";
        internal const string QualifiedName = $"global::{FullName}";
    }

    internal static class DefaultValueAttribute
    {
        internal const string Name = "DefaultValueAttribute";
        internal const string FullName = $"System.ComponentModel.{Name}";
        internal const string QualifiedName = $"global::{FullName}";
    }
}
