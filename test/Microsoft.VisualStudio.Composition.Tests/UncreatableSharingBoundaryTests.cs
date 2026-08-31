// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.Composition.Tests;

using System;
using System.Collections.Generic;
using System.Composition;
using System.Linq;
using Xunit;
using MefV1 = System.ComponentModel.Composition;

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

    [MefFact(
        CompositionEngines.V3EmulatingV2 | CompositionEngines.V3AllowConfigurationWithErrors,
        typeof(RootWithUnsatisfiedBoundaryFactory),
        typeof(OrphanPart),
        InvalidConfiguration = true)]
    public void UnsatisfiedExportFactoryDoesNotMakeSharingBoundaryCreatable(IContainer container)
    {
        var v3Container = Assert.IsType<TestUtilities.V3ContainerWrapper>(container);
        var rootCause = Assert.Single(v3Container.Configuration.CompositionErrors.Peek());
        Assert.Equal(typeof(OrphanPart), Assert.Single(rootCause.Parts).Definition.Type);

        Assert.Null(container.GetExportedValue<RootWithUnsatisfiedBoundaryFactory>().Factory);
    }

    [MefFact(
        CompositionEngines.V3EmulatingV1AndV2AtOnce | CompositionEngines.V3AllowConfigurationWithErrors,
        typeof(InferredOrphanPart),
        typeof(OrphanPart),
        InvalidConfiguration = true)]
    public void InferredUncreatableSharingBoundaryRejectsPart(IContainer container)
    {
        var v3Container = Assert.IsType<TestUtilities.V3ContainerWrapper>(container);
        var rejectedPartTypes = v3Container.Configuration.CompositionErrors.Peek()
            .Select(error => Assert.Single(error.Parts).Definition.Type)
            .ToHashSet();

        Assert.Equal(2, rejectedPartTypes.Count);
        Assert.Contains(typeof(InferredOrphanPart), rejectedPartTypes);
        Assert.Contains(typeof(OrphanPart), rejectedPartTypes);
    }

    [MefFact(
        CompositionEngines.V3EmulatingV2 | CompositionEngines.V3AllowConfigurationWithErrors,
        typeof(InvalidFactoryOwner),
        typeof(ScopedFromInvalidFactory),
        InvalidConfiguration = true)]
    public void InvalidPartDoesNotMakeSharingBoundaryCreatable(IContainer container)
    {
        var v3Container = Assert.IsType<TestUtilities.V3ContainerWrapper>(container);
        var rejectedPartTypes = v3Container.Configuration.CompositionErrors.Peek()
            .Select(error => Assert.Single(error.Parts).Definition.Type)
            .ToHashSet();

        Assert.Equal(2, rejectedPartTypes.Count);
        Assert.Contains(typeof(InvalidFactoryOwner), rejectedPartTypes);
        Assert.Contains(typeof(ScopedFromInvalidFactory), rejectedPartTypes);
    }

    [MefFact(
        CompositionEngines.V3EmulatingV2 | CompositionEngines.V3AllowConfigurationWithErrors,
        typeof(InvalidDependency),
        typeof(FactoryOwnerWithInvalidDependency),
        typeof(ScopedFromTransitivelyInvalidFactory),
        InvalidConfiguration = true)]
    public void TransitivelyInvalidPartDoesNotMakeSharingBoundaryCreatable(IContainer container)
    {
        var v3Container = Assert.IsType<TestUtilities.V3ContainerWrapper>(container);
        var rootRejectedPartTypes = v3Container.Configuration.CompositionErrors.Peek()
            .Select(error => Assert.Single(error.Parts).Definition.Type)
            .ToHashSet();
        Assert.Equal(2, rootRejectedPartTypes.Count);
        Assert.Contains(typeof(InvalidDependency), rootRejectedPartTypes);
        Assert.Contains(typeof(ScopedFromTransitivelyInvalidFactory), rootRejectedPartTypes);

        var secondOrderRejections = v3Container.Configuration.CompositionErrors.Pop().Peek();
        Assert.Equal(2, secondOrderRejections.Count);
        Assert.All(secondOrderRejections, rejection => Assert.Equal(typeof(FactoryOwnerWithInvalidDependency), Assert.Single(rejection.Parts).Definition.Type));
    }

    [MefFact(
        CompositionEngines.V3EmulatingV2 | CompositionEngines.V3AllowConfigurationWithErrors,
        typeof(UniqueMessageHandler),
        typeof(ScopedMessageHandler),
        typeof(RootWithImportManyAndValidFactory),
        typeof(ValidScopedPart),
        InvalidConfiguration = true)]
    public void ImportManyPruningDoesNotInvalidateFactoryOwner(IContainer container)
    {
        var root = container.GetExportedValue<RootWithImportManyAndValidFactory>();
        Assert.IsType<UniqueMessageHandler>(Assert.Single(root.Handlers));
        Assert.IsType<ValidScopedPart>(root.Factory.CreateExport().Value);
    }

    [MefFact(
        CompositionEngines.V3EmulatingV2 | CompositionEngines.V3AllowConfigurationWithErrors,
        typeof(RootWithSiblingFactories),
        typeof(SiblingScopeA),
        typeof(SiblingScopeB),
        typeof(PartRequiringSiblingScopes),
        typeof(UncreatableNestedScope),
        InvalidConfiguration = true)]
    public void SiblingSharingBoundariesDoNotCombineForReachability(IContainer container)
    {
        var v3Container = Assert.IsType<TestUtilities.V3ContainerWrapper>(container);
        var rootCause = Assert.Single(v3Container.Configuration.CompositionErrors.Peek());
        Assert.Equal(typeof(UncreatableNestedScope), Assert.Single(rootCause.Parts).Definition.Type);

        var root = container.GetExportedValue<RootWithSiblingFactories>();
        Assert.IsType<SiblingScopeA>(root.FactoryA.CreateExport().Value);
        Assert.IsType<SiblingScopeB>(root.FactoryB.CreateExport().Value);
    }

    [MefFact(
        CompositionEngines.V3EmulatingV2 | CompositionEngines.V3AllowConfigurationWithErrors,
        typeof(RootWithSiblingServiceFactories),
        typeof(SiblingServiceA),
        typeof(SiblingServiceB),
        typeof(ImportManyOwnerWithNestedFactory),
        typeof(NestedFromIncompatibleImportMany),
        InvalidConfiguration = true)]
    public void ImportManyAcrossSiblingScopesDoesNotEnableNestedFactory(IContainer container)
    {
        var v3Container = Assert.IsType<TestUtilities.V3ContainerWrapper>(container);
        var rootCause = Assert.Single(v3Container.Configuration.CompositionErrors.Peek());
        Assert.Equal(typeof(NestedFromIncompatibleImportMany), Assert.Single(rootCause.Parts).Definition.Type);
    }

    [MefFact(
        CompositionEngines.V3EmulatingV2 | CompositionEngines.V3AllowConfigurationWithErrors,
        typeof(RootWithInvalidSiblingFactoryTarget),
        typeof(ExistingScopeC),
        typeof(TargetForScopeA),
        typeof(UnrelatedScopeA),
        InvalidConfiguration = true)]
    public void FactoryTargetMustBeInstantiableInProspectiveScope(IContainer container)
    {
        var v3Container = Assert.IsType<TestUtilities.V3ContainerWrapper>(container);
        var rootCause = Assert.Single(v3Container.Configuration.CompositionErrors.Peek());
        Assert.Equal(typeof(UnrelatedScopeA), Assert.Single(rootCause.Parts).Definition.Type);

        var root = container.GetExportedValue<RootWithInvalidSiblingFactoryTarget>();
        Assert.IsType<ExistingScopeC>(root.FactoryC.CreateExport().Value);
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

    public interface IMissing { }

    [Export]
    public class RootWithUnsatisfiedBoundaryFactory
    {
        [Import(AllowDefault = true), SharingBoundary("orphan")]
        public ExportFactory<IMissing>? Factory { get; set; }
    }

    [Export, Shared("orphan")]
    public class OrphanPart { }

    [MefV1.Export, MefV1.PartCreationPolicy(MefV1.CreationPolicy.Shared)]
    public class InferredOrphanPart
    {
        [MefV1.ImportMany]
        public ICollection<OrphanPart> Parts { get; set; } = null!;
    }

    [Export]
    public class InvalidFactoryOwner
    {
        [Import]
        public IMissing Missing { get; set; } = null!;

        [Import, SharingBoundary("madeByInvalid")]
        public ExportFactory<ScopedFromInvalidFactory> Factory { get; set; } = null!;
    }

    [Export, Shared("madeByInvalid")]
    public class ScopedFromInvalidFactory { }

    [Export]
    public class InvalidDependency
    {
        [Import]
        public IMissing Missing { get; set; } = null!;
    }

    [Export]
    public class FactoryOwnerWithInvalidDependency
    {
        [Import]
        public InvalidDependency InvalidDependency { get; set; } = null!;

        [Import, SharingBoundary("madeByTransitivelyInvalid")]
        public ExportFactory<ScopedFromTransitivelyInvalidFactory> Factory { get; set; } = null!;
    }

    [Export, Shared("madeByTransitivelyInvalid")]
    public class ScopedFromTransitivelyInvalidFactory { }

    [Export, Shared]
    public class RootWithImportManyAndValidFactory
    {
        [ImportMany]
        public ICollection<IMessageHandler> Handlers { get; set; } = null!;

        [Import, SharingBoundary("valid")]
        public ExportFactory<ValidScopedPart> Factory { get; set; } = null!;
    }

    [Export, Shared("valid")]
    public class ValidScopedPart { }

    [Export, Shared]
    public class RootWithSiblingFactories
    {
        [Import, SharingBoundary("A")]
        public ExportFactory<SiblingScopeA> FactoryA { get; set; } = null!;

        [Import, SharingBoundary("B")]
        public ExportFactory<SiblingScopeB> FactoryB { get; set; } = null!;
    }

    [Export, Shared("A")]
    public class SiblingScopeA { }

    [Export, Shared("B")]
    public class SiblingScopeB { }

    [Export]
    public class PartRequiringSiblingScopes
    {
        [Import]
        public SiblingScopeA ScopeA { get; set; } = null!;

        [Import]
        public SiblingScopeB ScopeB { get; set; } = null!;

        [Import, SharingBoundary("C")]
        public ExportFactory<UncreatableNestedScope> FactoryC { get; set; } = null!;
    }

    [Export, Shared("C")]
    public class UncreatableNestedScope { }

    public interface ISiblingService { }

    [Export, Export(typeof(ISiblingService)), Shared("serviceA")]
    public class SiblingServiceA : ISiblingService { }

    [Export, Export(typeof(ISiblingService)), Shared("serviceB")]
    public class SiblingServiceB : ISiblingService { }

    [Export, Shared]
    public class RootWithSiblingServiceFactories
    {
        [Import, SharingBoundary("serviceA")]
        public ExportFactory<SiblingServiceA> FactoryA { get; set; } = null!;

        [Import, SharingBoundary("serviceB")]
        public ExportFactory<SiblingServiceB> FactoryB { get; set; } = null!;
    }

    [Export]
    public class ImportManyOwnerWithNestedFactory
    {
        [ImportMany]
        public ICollection<ISiblingService> Services { get; set; } = null!;

        [Import, SharingBoundary("nestedFromImportMany")]
        public ExportFactory<NestedFromIncompatibleImportMany> Factory { get; set; } = null!;
    }

    [Export, Shared("nestedFromImportMany")]
    public class NestedFromIncompatibleImportMany { }

    [Export, Shared]
    public class RootWithInvalidSiblingFactoryTarget
    {
        [Import, SharingBoundary("scopeC")]
        public ExportFactory<ExistingScopeC> FactoryC { get; set; } = null!;

        [Import, SharingBoundary("scopeA")]
        public ExportFactory<TargetForScopeA> FactoryA { get; set; } = null!;
    }

    [Export, Shared("scopeC")]
    public class ExistingScopeC { }

    [Export]
    public class TargetForScopeA
    {
        [Import]
        public ExistingScopeC ScopeC { get; set; } = null!;
    }

    [Export, Shared("scopeA")]
    public class UnrelatedScopeA { }
}
