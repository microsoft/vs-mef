// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.Composition.Tests
{
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Reflection;
    using Microsoft.VisualStudio.Composition.Reflection;
    using Xunit;

    public class ComposablePartDefinitionTests
    {
        [Fact]
        public void EqualsConsidersImportingConstructorRef()
        {
            TypeRef typeRef = TypeRef.Get(typeof(int), TestUtilities.Resolver);

            ComposablePartDefinition partDef1 =
                new ComposablePartDefinition(
                    typeRef,
                    ImmutableDictionary<string, object?>.Empty,
                    ImmutableList.Create<ExportDefinition>(),
                    ImmutableDictionary.Create<MemberRef, IReadOnlyCollection<ExportDefinition>>(),
                    ImmutableList.Create<ImportDefinitionBinding>(),
                    null,
                    default(MethodRef),
                    new MethodRef(typeRef, metadataToken: 1000, name: ConstructorInfo.ConstructorName, isStatic: true, parameterTypes: ImmutableArray<TypeRef>.Empty, genericMethodArguments: ImmutableArray<TypeRef>.Empty),
                    ImmutableList.Create<ImportDefinitionBinding>(),
                    CreationPolicy.Any,
                    true);

            ComposablePartDefinition partDef2 =
                new ComposablePartDefinition(
                    typeRef,
                    ImmutableDictionary<string, object?>.Empty,
                    ImmutableList.Create<ExportDefinition>(),
                    ImmutableDictionary.Create<MemberRef, IReadOnlyCollection<ExportDefinition>>(),
                    ImmutableList.Create<ImportDefinitionBinding>(),
                    null,
                    default(MethodRef),
                    new MethodRef(typeRef, metadataToken: 1001, name: ConstructorInfo.ConstructorName, isStatic: true, parameterTypes: ImmutableArray<TypeRef>.Empty, genericMethodArguments: ImmutableArray<TypeRef>.Empty),
                    ImmutableList.Create<ImportDefinitionBinding>(),
                    CreationPolicy.Any,
                    true);

            Assert.NotEqual(partDef1, partDef2);
        }
    }
}
