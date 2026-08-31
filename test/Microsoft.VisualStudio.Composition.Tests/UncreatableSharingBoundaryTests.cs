// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.Composition.Tests;

using System;
using System.Collections.Generic;
using System.Composition;
using System.Linq;
using Xunit;

/// <summary>
/// Verifies that parts in sharing boundaries that cannot be created are rejected during composition.
/// </summary>
/// <remarks>
/// These scenarios reproduce <see href="https://github.com/microsoft/vs-mef/issues/132">issue #132</see>.
/// </remarks>
public class UncreatableSharingBoundaryTests
{
    [MefFact(
        CompositionEngines.V3EmulatingV2 | CompositionEngines.V3AllowConfigurationWithErrors,
        typeof(UniqueMessageHandler),
        typeof(GlobalMessageHandler),
        typeof(ScopedMessageHandler),
        typeof(Helper1),
        typeof(Helper2),
        InvalidConfiguration = true)]
    public void ImportManyWithUncreatableSharingBoundaryRejectsScopedExport(IContainer container)
    {
        var v3Container = Assert.IsType<TestUtilities.V3ContainerWrapper>(container);
        var rootCause = Assert.Single(v3Container.Configuration.CompositionErrors.Peek());
        Assert.Equal(typeof(ScopedMessageHandler), Assert.Single(rootCause.Parts).Definition.Type);

        var helper = container.GetExportedValue<Helper2>().LazyHelper.Value;
        Assert.Equal(2, helper.Handlers.Count);
        Assert.Contains(helper.Handlers, handler => handler.Value is UniqueMessageHandler);
        Assert.Contains(helper.Handlers, handler => handler.Value is GlobalMessageHandler);
    }

    [MefFact(
        CompositionEngines.V3EmulatingV2 | CompositionEngines.V3AllowConfigurationWithErrors,
        typeof(UncreatableParent),
        typeof(UncreatableChild),
        InvalidConfiguration = true)]
    public void SharingBoundaryFactoryOnUncreatablePartDoesNotMakeChildBoundaryCreatable(IContainer container)
    {
        var v3Container = Assert.IsType<TestUtilities.V3ContainerWrapper>(container);
        var rejectedPartTypes = v3Container.Configuration.CompositionErrors.Peek()
            .Select(error => Assert.Single(error.Parts).Definition.Type)
            .ToHashSet();

        Assert.Equal(2, rejectedPartTypes.Count);
        Assert.Contains(typeof(UncreatableParent), rejectedPartTypes);
        Assert.Contains(typeof(UncreatableChild), rejectedPartTypes);
    }

    public interface IMessageHandler { }

    [Export(typeof(IMessageHandler))]
    public class UniqueMessageHandler : IMessageHandler { }

    [Export(typeof(IMessageHandler)), Shared]
    public class GlobalMessageHandler : IMessageHandler { }

    [Export(typeof(IMessageHandler)), Shared("somewhere")]
    public class ScopedMessageHandler : IMessageHandler { }

    [Export]
    public class Helper1
    {
        [ImportMany]
        public ICollection<Lazy<IMessageHandler>> Handlers { get; set; } = null!;
    }

    [Export]
    public class Helper2
    {
        [Import]
        public Lazy<Helper1> LazyHelper { get; set; } = null!;
    }

    [Export, Shared("parent")]
    public class UncreatableParent
    {
        [Import, SharingBoundary("child")]
        public ExportFactory<UncreatableChild> ChildFactory { get; set; } = null!;
    }

    [Export, Shared("child")]
    public class UncreatableChild { }
}
