// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#pragma warning disable SA1202 // Elements should be ordered by access - because field initializer depend on each other

internal static class ReferencesHelper
{
    private static readonly string NuGetConfigPath = FindNuGetConfigPath();

    internal static ReferenceAssemblies DefaultReferences = ReferenceAssemblies.NetFramework.Net472.Default
        .WithNuGetConfigFilePath(NuGetConfigPath)
        .WithPackages(ImmutableArray.Create(
            new PackageIdentity("System.Composition.AttributedModel", "6.0.0"),
            new PackageIdentity("System.ComponentModel.Composition", "6.0.0")));

    private static string FindNuGetConfigPath()
    {
        string? path = AppContext.BaseDirectory;
        while (path is not null)
        {
            string candidate = Path.Combine(path, "nuget.config");
            if (File.Exists(candidate))
            {
                return candidate;
            }

            path = Path.GetDirectoryName(path);
        }

        throw new InvalidOperationException("Could not find NuGet.config by searching up from " + AppContext.BaseDirectory);
    }
}
