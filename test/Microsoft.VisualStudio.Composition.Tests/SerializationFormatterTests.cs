// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.Composition.Tests;

using System;
using System.Collections.Immutable;
using System.Composition;
using System.Globalization;
using System.Reflection;
using MessagePack;
using MessagePack.Resolvers;
using Microsoft.VisualStudio.Composition.Reflection;
using Xunit;

public class SerializationFormatterTests
{
    [Fact]
    public void AssemblyNameSerializeTest()
    {
        AssemblyNameSerialize.Instance.TestSerialization();
    }

    [Fact]
    public void MetaDataDictionaryFormatterSerializeTest()
    {
        MetaDataDictionaryFormatterSerialize.Instance.TestSerialization();
    }

    [Fact]
    public void MetaDataObjectSerializeTest()
    {
        MetaDataObjectSerialize.Instance.TestSerialization();
    }

    [Fact]
    public void MemberRefSerializeTest()
    {
        MemberRefSerialize.Instance.TestSerialization();
    }

    [Fact]
    public void TypeRefSerializeTest()
    {
        TypeRefSerialize.Instance.TestSerialization();
    }

    [Fact]
    public void ComposableCatalogSerializeTest()
    {
        ComposableCatalogSerialize.Instance.TestSerialization();
    }

    [Fact]
    public void ComposablePartDefinitionSerializeTest()
    {
        ComposablePartDefinitionSerialize.Instance.TestSerialization();
    }

    [Fact]
    public void ImportDefinitionBindingSerializeTest()
    {
        ImportDefinitionBindingSerialize.Instance.TestSerialization();
    }

    [Fact]
    public void ImportMetadataViewConstraintSerializeTest()
    {
        ImportMetadataViewConstraintSerialize.Instance.TestSerialization();
    }

    [Fact]
    public void PartCreationPolicyConstraintSerializeTest()
    {
        PartCreationPolicyConstraintSerialize.Instance.TestSerialization();
    }

    [Fact]
    public void RuntimeCompositionSerializeTest()
    {
        RuntimeCompositionSerialize.Instance.TestSerialization();
    }

    private abstract class TypeSerializerTest<TObjectType>
    {
        private MessagePackReader SerializeAndGetReader(TObjectType objectToValidate, out MessagePackSerializerContext context)
        {
            context = new MessagePackSerializerContext(StandardResolverAllowPrivate.Instance, Resolver.DefaultInstance);
            var bytes = MessagePackSerializer.Serialize(objectToValidate, context);

            return new MessagePackReader(bytes);
        }

        private void ReadArray(ref MessagePackReader messagePackReader, MessagePackSerializerOptions context)
        {
            int headerCount = messagePackReader.ReadArrayHeader();
            context.Security.DepthStep(ref messagePackReader);
            try
            {
                for (int i = 0; i < headerCount; i++)
                {
                    this.Read(ref messagePackReader, context);
                }
            }
            finally
            {
                messagePackReader.Depth--;
            }
        }

        private void ReadMapArray(ref MessagePackReader messagePackReader, MessagePackSerializerOptions context)
        {
            int headerCount = messagePackReader.ReadMapHeader();
            context.Security.DepthStep(ref messagePackReader);
            try
            {
                for (int i = 0; i < headerCount * 2; i++)
                {
                    this.Read(ref messagePackReader, context);
                }
            }
            finally
            {
                messagePackReader.Depth--;
            }
        }

        private void Read(ref MessagePackReader reader, MessagePackSerializerOptions context)
        {
            switch (reader.NextMessagePackType)
            {
                case MessagePackType.Array:
                    this.ReadArray(ref reader, context);
                    break;
                case MessagePackType.Map:
                    this.ReadMapArray(ref reader, context);
                    break;
                default:
                    reader.Skip();
                    break;
            }
        }

        public void TestSerialization()
        {
            TObjectType[] objectsToValidate = this.CreateObjectToSerialize();

            foreach (TObjectType objectToValidate in objectsToValidate)
            {
                var reader = this.SerializeAndGetReader(objectToValidate, out MessagePackSerializerContext context);
                this.Read(ref reader, context);

                bool isMessagePackReaderAtEnd = reader.End;

                // Ensure that the reader is at the end of the stream. If it is not, then the serialization failed.
                Assert.True(isMessagePackReaderAtEnd);
            }
        }

        protected abstract TObjectType[] CreateObjectToSerialize();
    }

    private class MetaDataObjectSerialize : TypeSerializerTest<object?>
    {
        public static readonly MetaDataObjectSerialize Instance = new();

        public MetaDataObjectSerialize()
        {
        }

        protected override object?[] CreateObjectToSerialize()
        {
            return new[]
            {
                new object[] { 1, 2, 3, TypeRef.Get(typeof(int), TestUtilities.Resolver) },
                new object[] { "Test1", "Test2", 1 },
            };
        }
    }

    private class MetaDataDictionaryFormatterSerialize : TypeSerializerTest<IReadOnlyDictionary<string, object?>>
    {
        public static readonly MetaDataDictionaryFormatterSerialize Instance = new();

        public MetaDataDictionaryFormatterSerialize()
        {
        }

        protected override IReadOnlyDictionary<string, object?>[] CreateObjectToSerialize()
        {
            var testObject1 = new Dictionary<string, object?>()
            {
                { "arrayKey", new int[] { 1, 2, 3 } },
                { "boolKey", true },
                { "longKey", 123456789L },
                { "ulongKey", 123456789UL },
                { "intKey", 123 },
                { "uintKey", 123U },
                { "shortKey", (short)123 },
                { "ushortKey", (ushort)123 },
                { "byteKey", (byte)123 },
                { "sbyteKey", (sbyte)123 },
                { "floatKey", 123.45f },
                { "doubleKey", 123.45 },
                { "charKey", 'A' },
                { "stringKey", "stringValue" },
                { "guidKey", Guid.NewGuid() },
                { "creationPolicyKey", CreationPolicy.Shared },
                { "typeRefKey", TypeRef.Get(typeof(object), TestUtilities.Resolver) },
                { "typeKey", typeof(string) },
            };

            return new[] { testObject1 };
        }
    }

    private class AssemblyNameSerialize : TypeSerializerTest<AssemblyName>
    {
        public static readonly AssemblyNameSerialize Instance = new();

        private AssemblyNameSerialize()
        {
        }

        protected override AssemblyName[] CreateObjectToSerialize()
        {
            byte[] publicKey = this.GetPublicKeyFromExecutingAssembly();
            AssemblyName testObject = this.CreateAssemblyName("AssemblyA", new Version(1, 0), CultureInfo.CurrentCulture, @"C:\some\path\AssemblyA.dll", publicKey: publicKey);

            return new[] { testObject };
        }

        private AssemblyName CreateAssemblyName(string name, Version version, CultureInfo cultureInfo, string codeBase, byte[]? publicKey = null, byte[]? publicKeyToken = null)
        {
            AssemblyName assemblyName = new AssemblyName();
            assemblyName.Name = name;
            assemblyName.Version = version;
            assemblyName.CultureInfo = cultureInfo;
            assemblyName.CodeBase = codeBase;

            if (publicKey != null)
            {
                assemblyName.SetPublicKey(publicKey);
            }
            else if (publicKeyToken != null)
            {
                assemblyName.SetPublicKeyToken(publicKeyToken);
            }

            return assemblyName;
        }

        private byte[] GetPublicKeyFromExecutingAssembly()
        {
            byte[] publicKey = typeof(ByValueEqualityTests).GetTypeInfo().Assembly.GetName().GetPublicKey()!;
            Assert.NotNull(publicKey);
            return publicKey;
        }
    }

    private class MemberRefSerialize : TypeSerializerTest<MemberRef>
    {
        public static readonly MemberRefSerialize Instance = new();

        public MemberRefSerialize()
        {
        }

        protected override MemberRef[] CreateObjectToSerialize()
        {
            return new MemberRef[]
            {
                new FieldRef(TypeRef.Get(typeof(string), TestUtilities.Resolver), TypeRef.Get(typeof(string), TestUtilities.Resolver), 1, "Field1", true),
                new PropertyRef(TypeRef.Get(typeof(string), TestUtilities.Resolver), TypeRef.Get(typeof(string), TestUtilities.Resolver), 1, 1, 1, "Property1", true),
                new MethodRef(TypeRef.Get(typeof(string), TestUtilities.Resolver), 1, "Method1", true, ImmutableArray.Create(new[] { TypeRef.Get(typeof(string), TestUtilities.Resolver) }), ImmutableArray.Create(new[] { TypeRef.Get(typeof(string), TestUtilities.Resolver) })),
            };
        }
    }

    private class TypeRefSerialize : TypeSerializerTest<TypeRef>
    {
        public static readonly TypeRefSerialize Instance = new();

        public TypeRefSerialize()
        {
        }

        protected override TypeRef[] CreateObjectToSerialize()
        {
            return new TypeRef[]
            {
                TypeRef.Get(typeof(string), TestUtilities.Resolver),
                TypeRef.Get(typeof(int), TestUtilities.Resolver),
                TypeRef.Get(typeof(TypeRef), TestUtilities.Resolver),
            };
        }
    }

    private class ComposableCatalogSerialize : TypeSerializerTest<ComposableCatalog>
    {
        public static readonly ComposableCatalogSerialize Instance = new();

        public ComposableCatalogSerialize()
        {
        }

        protected override ComposableCatalog[] CreateObjectToSerialize()
        {
            var partDiscoverer = new AttributedPartDiscovery(Resolver.DefaultInstance, isNonPublicSupported: true);
            var testExportPart = partDiscoverer.CreatePart(typeof(TestExport))!;
            var testMEFPartWithStaticFactoryMethodPart = partDiscoverer.CreatePart(typeof(TestMEFPartWithStaticFactoryMethod))!;
            var testMEFPartWithStaticFactoryMethodRef = MethodRef.Get(typeof(TestMEFPartWithStaticFactoryMethod).GetTypeInfo().DeclaredMethods.Single(m => m.Name == nameof(TestMEFPartWithStaticFactoryMethod.Create)), Resolver.DefaultInstance);
            testMEFPartWithStaticFactoryMethodPart = new ComposablePartDefinition(
                testMEFPartWithStaticFactoryMethodPart.TypeRef,
                testMEFPartWithStaticFactoryMethodPart.Metadata,
                testMEFPartWithStaticFactoryMethodPart.ExportedTypes,
                testMEFPartWithStaticFactoryMethodPart.ExportingMembers,
                testMEFPartWithStaticFactoryMethodPart.ImportingMembers,
                testMEFPartWithStaticFactoryMethodPart.SharingBoundary,
                testMEFPartWithStaticFactoryMethodPart.OnImportsSatisfiedMethodRefs,
                testMEFPartWithStaticFactoryMethodRef,
                testMEFPartWithStaticFactoryMethodPart.ImportingConstructorImports?.Take(1).ToList(),
                testMEFPartWithStaticFactoryMethodPart.CreationPolicy,
                testMEFPartWithStaticFactoryMethodPart.IsSharingBoundaryInferred);

            var catalog = ComposableCatalog.Create(Resolver.DefaultInstance)
                .AddParts(new[] { testExportPart, testMEFPartWithStaticFactoryMethodPart });

            return new[] { catalog };
        }

        [Export, Shared]
        private class TestExport
        {
        }

        [Export]
        private class TestMEFPartWithStaticFactoryMethod
        {
            [ImportingConstructor] // This is so we can 'transfer' it to the static factory method in the test.
            private TestMEFPartWithStaticFactoryMethod(TestExport someOtherExport, bool anotherRandomValue)
            {
                this.SomeOtherExport = someOtherExport;
                this.AnotherRandomValue = anotherRandomValue;
            }

            public TestExport SomeOtherExport { get; }

            public bool AnotherRandomValue { get; }

            public static TestMEFPartWithStaticFactoryMethod Create(TestExport someOtherExport)
            {
                return new TestMEFPartWithStaticFactoryMethod(someOtherExport, true);
            }
        }
    }

    private class ComposablePartDefinitionSerialize : TypeSerializerTest<ComposablePartDefinition>
    {
        public static readonly ComposablePartDefinitionSerialize Instance = new();

        public ComposablePartDefinitionSerialize()
        {
        }

        protected override ComposablePartDefinition[] CreateObjectToSerialize()
        {
            var partDiscoverer = new AttributedPartDiscovery(Resolver.DefaultInstance, isNonPublicSupported: true);
            var testExportPart = partDiscoverer.CreatePart(typeof(TestExport))!;
            var testMEFPartWithStaticFactoryMethodPart = partDiscoverer.CreatePart(typeof(TestMEFPartWithStaticFactoryMethod))!;
            var testMEFPartWithStaticFactoryMethodRef = MethodRef.Get(typeof(TestMEFPartWithStaticFactoryMethod).GetTypeInfo().DeclaredMethods.Single(m => m.Name == nameof(TestMEFPartWithStaticFactoryMethod.Create)), Resolver.DefaultInstance);
            testMEFPartWithStaticFactoryMethodPart = new ComposablePartDefinition(
                testMEFPartWithStaticFactoryMethodPart.TypeRef,
                testMEFPartWithStaticFactoryMethodPart.Metadata,
                testMEFPartWithStaticFactoryMethodPart.ExportedTypes,
                testMEFPartWithStaticFactoryMethodPart.ExportingMembers,
                testMEFPartWithStaticFactoryMethodPart.ImportingMembers,
                testMEFPartWithStaticFactoryMethodPart.SharingBoundary,
                testMEFPartWithStaticFactoryMethodPart.OnImportsSatisfiedMethodRefs,
                testMEFPartWithStaticFactoryMethodRef,
                testMEFPartWithStaticFactoryMethodPart.ImportingConstructorImports?.Take(1).ToList(),
                testMEFPartWithStaticFactoryMethodPart.CreationPolicy,
                testMEFPartWithStaticFactoryMethodPart.IsSharingBoundaryInferred);

            return new[] { testMEFPartWithStaticFactoryMethodPart };
        }

        [Export, Shared]
        private class TestExport
        {
        }

        [Export]
        private class TestMEFPartWithStaticFactoryMethod
        {
            [ImportingConstructor] // This is so we can 'transfer' it to the static factory method in the test.
            private TestMEFPartWithStaticFactoryMethod(TestExport someOtherExport, bool anotherRandomValue)
            {
                this.SomeOtherExport = someOtherExport;
                this.AnotherRandomValue = anotherRandomValue;
            }

            public TestExport SomeOtherExport { get; }

            public bool AnotherRandomValue { get; }

            public static TestMEFPartWithStaticFactoryMethod Create(TestExport someOtherExport)
            {
                return new TestMEFPartWithStaticFactoryMethod(someOtherExport, true);
            }
        }
    }

    private class ImportDefinitionBindingSerialize : TypeSerializerTest<ImportDefinitionBinding>
    {
        public static readonly ImportDefinitionBindingSerialize Instance = new();

        public ImportDefinitionBindingSerialize()
        {
        }

        protected override ImportDefinitionBinding[] CreateObjectToSerialize()
        {
            var partDiscoverer = new AttributedPartDiscovery(Resolver.DefaultInstance, isNonPublicSupported: true);
            var testExportPart = partDiscoverer.CreatePart(typeof(TestExport))!;
            var testMEFPartWithStaticFactoryMethodPart = partDiscoverer.CreatePart(typeof(TestMEFPartWithStaticFactoryMethod))!;
            var testMEFPartWithStaticFactoryMethodRef = MethodRef.Get(typeof(TestMEFPartWithStaticFactoryMethod).GetTypeInfo().DeclaredMethods.Single(m => m.Name == nameof(TestMEFPartWithStaticFactoryMethod.Create)), Resolver.DefaultInstance);
            testMEFPartWithStaticFactoryMethodPart = new ComposablePartDefinition(
                testMEFPartWithStaticFactoryMethodPart.TypeRef,
                testMEFPartWithStaticFactoryMethodPart.Metadata,
                testMEFPartWithStaticFactoryMethodPart.ExportedTypes,
                testMEFPartWithStaticFactoryMethodPart.ExportingMembers,
                testMEFPartWithStaticFactoryMethodPart.ImportingMembers,
                testMEFPartWithStaticFactoryMethodPart.SharingBoundary,
                testMEFPartWithStaticFactoryMethodPart.OnImportsSatisfiedMethodRefs,
                testMEFPartWithStaticFactoryMethodRef,
                testMEFPartWithStaticFactoryMethodPart.ImportingConstructorImports?.Take(1).ToList(),
                testMEFPartWithStaticFactoryMethodPart.CreationPolicy,
                testMEFPartWithStaticFactoryMethodPart.IsSharingBoundaryInferred);

            var catalog = ComposableCatalog.Create(Resolver.DefaultInstance)
                .AddParts(new[] { testExportPart, testMEFPartWithStaticFactoryMethodPart });

            var importDefinition = new ImportDefinition(
                typeof(TestExport).FullName!,
                ImportCardinality.ExactlyOne,
                ImmutableDictionary<string, object?>.Empty,
                ImmutableList<IImportSatisfiabilityConstraint>.Empty);

            var memberRef = new FieldRef(TypeRef.Get(typeof(string), TestUtilities.Resolver), TypeRef.Get(typeof(string), TestUtilities.Resolver), 1, "Field1", true);

            var binding = new ImportDefinitionBinding(importDefinition, TypeRef.Get(typeof(string), TestUtilities.Resolver), memberRef, TypeRef.Get(typeof(string), TestUtilities.Resolver), TypeRef.Get(typeof(string), TestUtilities.Resolver));

            return new[] { binding };
        }

        [Export, Shared]
        private class TestExport
        {
        }

        [Export]
        private class TestMEFPartWithStaticFactoryMethod
        {
            [ImportingConstructor] // This is so we can 'transfer' it to the static factory method in the test.
            private TestMEFPartWithStaticFactoryMethod(TestExport someOtherExport, bool anotherRandomValue)
            {
                this.SomeOtherExport = someOtherExport;
                this.AnotherRandomValue = anotherRandomValue;
            }

            public TestExport SomeOtherExport { get; }

            public bool AnotherRandomValue { get; }

            public static TestMEFPartWithStaticFactoryMethod Create(TestExport someOtherExport)
            {
                return new TestMEFPartWithStaticFactoryMethod(someOtherExport, true);
            }
        }
    }

    private class ImportMetadataViewConstraintSerialize : TypeSerializerTest<ImportMetadataViewConstraint>
    {
        public static readonly ImportMetadataViewConstraintSerialize Instance = new();

        public ImportMetadataViewConstraintSerialize()
        {
        }

        protected override ImportMetadataViewConstraint[] CreateObjectToSerialize()
        {
            var importMetadataViewConstraint = ImportMetadataViewConstraint.GetConstraint(TypeRef.Get(typeof(string), TestUtilities.Resolver), TestUtilities.Resolver);

            return new[] { importMetadataViewConstraint };
        }
    }

    private class PartCreationPolicyConstraintSerialize : TypeSerializerTest<PartCreationPolicyConstraint>
    {
        public static readonly PartCreationPolicyConstraintSerialize Instance = new();

        public PartCreationPolicyConstraintSerialize()
        {
        }

        protected override PartCreationPolicyConstraint[] CreateObjectToSerialize()
        {
            var partCreationPolicyConstraint = PartCreationPolicyConstraint.GetRequiredCreationPolicyConstraint(CreationPolicy.NonShared)!;

            return new[] { partCreationPolicyConstraint };
        }
    }

    private class RuntimeCompositionSerialize : TypeSerializerTest<RuntimeComposition>
    {
        public static readonly RuntimeCompositionSerialize Instance = new();

        public RuntimeCompositionSerialize()
        {
        }

        protected override RuntimeComposition[] CreateObjectToSerialize()
        {
            var catalog = TestUtilities.EmptyCatalog.AddParts(new[] { TestUtilities.V2Discovery.CreatePart(typeof(SomeExport))! });
            var configuration = CompositionConfiguration.Create(catalog);
            var runtimeComposition = RuntimeComposition.CreateRuntimeComposition(configuration);

            return new[] { runtimeComposition };
        }

        [Export]
        public class SomeExport { }
    }
}
