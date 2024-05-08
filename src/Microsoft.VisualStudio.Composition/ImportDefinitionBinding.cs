// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.Composition
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using MessagePack;
    using System.Reflection;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.VisualStudio.Composition.Reflection;
    using MessagePack.Formatters;


    public class ImportDefinitionBindingFormatter : IMessagePackFormatter<ImportDefinitionBinding>
    {
        public void Serialize(ref MessagePackWriter writer, ImportDefinitionBinding value, MessagePackSerializerOptions options)
        {

            if (value.ImportingMemberRef == null)
            {
                options.Resolver.GetFormatterWithVerify<bool>().Serialize(ref writer, false, options);

                options.Resolver.GetFormatterWithVerify<ParameterRef?>().Serialize(ref writer, value.ImportingParameterRef, options);
            }
            else
            {
                options.Resolver.GetFormatterWithVerify<bool>().Serialize(ref writer, true, options);

                options.Resolver.GetFormatterWithVerify<MemberRef?>().Serialize(ref writer, value.ImportingMemberRef, options);

            }


            options.Resolver.GetFormatterWithVerify<Type>().Serialize(ref writer, value.ComposablePartType, options);
            options.Resolver.GetFormatterWithVerify<TypeRef>().Serialize(ref writer, value.ComposablePartTypeRef, options);


            options.Resolver.GetFormatterWithVerify<Type?>().Serialize(ref writer, value.ExportFactoryType, options);
            options.Resolver.GetFormatterWithVerify<ImportDefinition>().Serialize(ref writer, value.ImportDefinition, options);
           // options.Resolver.GetFormatterWithVerify<MemberInfo?>().Serialize(ref writer, value.ImportingMember, options);
            //options.Resolver.GetFormatterWithVerify<ParameterInfo?>().Serialize(ref writer, value.ImportingParameter, options);
            options.Resolver.GetFormatterWithVerify<Type>().Serialize(ref writer, value.ImportingSiteType, options);
            options.Resolver.GetFormatterWithVerify<TypeRef>().Serialize(ref writer, value.ImportingSiteTypeRef, options);
            options.Resolver.GetFormatterWithVerify<Type>().Serialize(ref writer, value.ImportingSiteTypeWithoutCollection, options);
            options.Resolver.GetFormatterWithVerify<TypeRef>().Serialize(ref writer, value.ImportingSiteTypeWithoutCollectionRef, options);
            options.Resolver.GetFormatterWithVerify<Type>().Serialize(ref writer, value.ImportingSiteElementType, options);
            options.Resolver.GetFormatterWithVerify<TypeRef>().Serialize(ref writer, value.ImportingSiteElementTypeRef, options);
            options.Resolver.GetFormatterWithVerify<bool>().Serialize(ref writer, value.IsExportFactory, options);
            options.Resolver.GetFormatterWithVerify<bool>().Serialize(ref writer, value.IsLazy, options);
            options.Resolver.GetFormatterWithVerify<Type>().Serialize(ref writer, value.MetadataType, options);


          
        }

        public ImportDefinitionBinding Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            var isMember = options.Resolver.GetFormatterWithVerify<bool>().Deserialize(ref reader, options);
            ParameterRef? importingParameterRef = null;
            MemberRef? importingMemberRef = null;
            if (!isMember)
            {
                importingParameterRef = options.Resolver.GetFormatterWithVerify<ParameterRef?>().Deserialize(ref reader, options);
            }
            else
            {
                importingMemberRef = options.Resolver.GetFormatterWithVerify<MemberRef?>().Deserialize(ref reader, options);
            }


            var composablePartType = options.Resolver.GetFormatterWithVerify<Type>().Deserialize(ref reader, options);
            var composablePartTypeRef = options.Resolver.GetFormatterWithVerify<TypeRef>().Deserialize(ref reader, options);

            var exportFactoryType = options.Resolver.GetFormatterWithVerify<Type?>().Deserialize(ref reader, options);
            var importDefinition = options.Resolver.GetFormatterWithVerify<ImportDefinition>().Deserialize(ref reader, options);
            //var importingMember = options.Resolver.GetFormatterWithVerify<MemberInfo?>().Deserialize(ref reader, options);
            //var importingParameter = options.Resolver.GetFormatterWithVerify<ParameterInfo?>().Deserialize(ref reader, options);

            var importingSiteType = options.Resolver.GetFormatterWithVerify<Type>().Deserialize(ref reader, options);
            var importingSiteTypeRef = options.Resolver.GetFormatterWithVerify<TypeRef>().Deserialize(ref reader, options);
            var importingSiteTypeWithoutCollection = options.Resolver.GetFormatterWithVerify<Type>().Deserialize(ref reader, options);
            var importingSiteTypeWithoutCollectionRef = options.Resolver.GetFormatterWithVerify<TypeRef>().Deserialize(ref reader, options);
            var importingSiteElementType = options.Resolver.GetFormatterWithVerify<Type>().Deserialize(ref reader, options);
            var importingSiteElementTypeRef = options.Resolver.GetFormatterWithVerify<TypeRef>().Deserialize(ref reader, options);
            var isExportFactory = options.Resolver.GetFormatterWithVerify<bool>().Deserialize(ref reader, options);
            var isLazy = options.Resolver.GetFormatterWithVerify<bool>().Deserialize(ref reader, options);
            var metadataType = options.Resolver.GetFormatterWithVerify<Type>().Deserialize(ref reader, options);

            if (isMember)
            {
                // return new ImportDefinitionBinding(importDefinition, importingSiteElementTypeRef, importingMemberRef, importingSiteTypeRef, importingSiteTypeWithoutCollectionRef);
                return new ImportDefinitionBinding(importDefinition, composablePartTypeRef, importingMemberRef, importingSiteTypeRef, importingSiteTypeWithoutCollectionRef);
            }
            else
            {
                //   return new ImportDefinitionBinding(importDefinition, importingSiteElementTypeRef, importingParameterRef, importingSiteTypeRef, importingSiteTypeWithoutCollectionRef);
                return new ImportDefinitionBinding(importDefinition, composablePartTypeRef, importingParameterRef, importingSiteTypeRef, importingSiteTypeWithoutCollectionRef);
            }


        }
    }


    //[MessagePackObject(false)]
    [MessagePackFormatter(typeof(ImportDefinitionBindingFormatter))]

    public class ImportDefinitionBinding : IEquatable<ImportDefinitionBinding>
    {
      //  [IgnoreMember]
        private bool? isLazy;

      //  [IgnoreMember]

        private TypeRef? importingSiteElementTypeRef;

        /// <summary>
        /// Initializes a new instance of the <see cref="ImportDefinitionBinding"/> class
        /// to represent an importing member.
        /// </summary>
        public ImportDefinitionBinding(
            ImportDefinition importDefinition,
            TypeRef composablePartTypeRef,
            MemberRef importingMemberRef,
            TypeRef importingSiteTypeRef,
            TypeRef importingSiteTypeWithoutCollectionRef)
        {
            Requires.NotNull(importDefinition, nameof(importDefinition));
            Requires.NotNull(composablePartTypeRef, nameof(composablePartTypeRef));
            Requires.NotNull(importingMemberRef, nameof(importingMemberRef));
            Requires.NotNull(importingSiteTypeRef, nameof(importingSiteTypeRef));
            Requires.NotNull(importingSiteTypeWithoutCollectionRef, nameof(importingSiteTypeWithoutCollectionRef));

            this.ImportDefinition = importDefinition;
            this.ComposablePartTypeRef = composablePartTypeRef;
            this.ImportingMemberRef = importingMemberRef;
            this.ImportingSiteTypeRef = importingSiteTypeRef;
            this.ImportingSiteTypeWithoutCollectionRef = importingSiteTypeWithoutCollectionRef;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ImportDefinitionBinding"/> class
        /// to represent a parameter in an importing constructor.
        /// </summary>
        public ImportDefinitionBinding(
            ImportDefinition importDefinition,
            TypeRef composablePartTypeRef,
            ParameterRef importingParameterRef,
            TypeRef importingSiteTypeRef,
            TypeRef importingSiteTypeWithoutCollectionRef)
        {
            Requires.NotNull(importDefinition, nameof(importDefinition));
            Requires.NotNull(composablePartTypeRef, nameof(composablePartTypeRef));
            Requires.NotNull(importingParameterRef, nameof(importingParameterRef));
            Requires.NotNull(importingSiteTypeRef, nameof(importingSiteTypeRef));
            Requires.NotNull(importingSiteTypeWithoutCollectionRef, nameof(importingSiteTypeWithoutCollectionRef));

            this.ImportDefinition = importDefinition;
            this.ComposablePartTypeRef = composablePartTypeRef;
            this.ImportingParameterRef = importingParameterRef;
            this.ImportingSiteTypeRef = importingSiteTypeRef;
            this.ImportingSiteTypeWithoutCollectionRef = importingSiteTypeWithoutCollectionRef;
        }

        /// <summary>
        /// Gets the definition for this import.
        /// </summary>

     //   [Key(0)]
        public ImportDefinition ImportDefinition { get; private set; }

        /// <summary>
        /// Gets the member this import is found on. Null for importing constructors.
        /// </summary>
        //[Key(1)] we need to  use the custome serilizer to seralize this property keeping ignore for now - Ankit TODO ths is onverting a MemberInfo object to a string and back is not straightforward because MemberInfo is an abstract class representing various types of members like fields, properties, methods, etc., and there's no single string representation that encapsulates all of its properties.
      //  [IgnoreMember] 
        public MemberInfo? ImportingMember => this.ImportingMemberRef?.MemberInfo;

        /// <summary>
        /// Gets the member this import is found on. Null for importing constructors.
        /// </summary>
       // [Key(2)]
        public MemberRef? ImportingMemberRef { get; private set; }

        //[Key(3)]
        [IgnoreMember] // no public constructor and can be derived from ImportingParameterRef
        public ParameterInfo? ImportingParameter => this.ImportingParameterRef?.ParameterInfo;

       // [Key(4)]
        public ParameterRef? ImportingParameterRef { get; private set; }

      //  [Key(5)]
        public Type ComposablePartType => this.ComposablePartTypeRef.ResolvedType;

       // [Key(6)]
        public TypeRef ComposablePartTypeRef { get; private set; }

        /// <summary>
        /// Gets the actual type of the variable or member that will be assigned the result.
        /// This includes any Lazy, ExportFactory or collection wrappers.
        /// </summary>
        /// <value>Never null.</value>
      //  [Key(7)]
        public Type ImportingSiteType => this.ImportingSiteTypeRef.Resolve();

        /// <summary>
        /// Gets the actual type of the variable or member that will be assigned the result.
        /// This includes any Lazy, ExportFactory or collection wrappers.
        /// </summary>
        /// <value>Never null.</value>
      //  [Key(8)]
        public TypeRef ImportingSiteTypeRef { get; }

       // [Key(9)]
        public TypeRef ImportingSiteTypeWithoutCollectionRef { get; }

      //  [Key(10)]
        public Type ImportingSiteTypeWithoutCollection => this.ImportingSiteTypeWithoutCollectionRef.ResolvedType;

        /// <summary>
        /// Gets the type of the member, with the ImportMany collection and Lazy/ExportFactory stripped off, when present.
        /// </summary>
     //   [Key(11)]
        public TypeRef ImportingSiteElementTypeRef
        {
            get
            {
                if (this.importingSiteElementTypeRef == null)
                {
                    this.importingSiteElementTypeRef = PartDiscovery.GetTypeIdentityFromImportingTypeRef(this.ImportingSiteTypeRef, this.ImportDefinition.Cardinality == ImportCardinality.ZeroOrMore);
                }

                return this.importingSiteElementTypeRef;
            }
        }

        /// <summary>
        /// Gets the type of the member, with the ImportMany collection and Lazy/ExportFactory stripped off, when present.
        /// </summary>
     //   [Key(12)]
        public Type? ImportingSiteElementType => this.ImportingSiteElementTypeRef?.Resolve();

     //   [Key(13)]
        public bool IsLazy
        {
            get
            {
                if (!this.isLazy.HasValue)
                {
                    this.isLazy = this.ImportingSiteTypeWithoutCollectionRef.IsAnyLazyType();
                }

                return this.isLazy.Value;
            }
        }

     //   [Key(14)]
        public Type? MetadataType
        {
            get
            {
                if (this.IsLazy || this.IsExportFactory)
                {
                    var args = this.ImportingSiteTypeWithoutCollectionRef.GenericTypeArguments;
                    if (args.Length == 2)
                    {
                        return args[1].ResolvedType;
                    }
                }

                return null;
            }
        }

     //   [Key(15)]
        public bool IsExportFactory
        {
            get { return this.ImportingSiteTypeWithoutCollectionRef.IsExportFactoryType(); }
        }

     //   [Key(16)]
        public Type? ExportFactoryType
        {
            get { return this.IsExportFactory ? this.ImportingSiteTypeWithoutCollection : null; }
        }

        public override int GetHashCode()
        {
            return this.ImportDefinition.GetHashCode();
        }

        public override bool Equals(object? obj)
        {
            return this.Equals(obj as ImportDefinitionBinding);
        }

        public bool Equals(ImportDefinitionBinding? other)
        {
            if (other == null)
            {
                return false;
            }

            bool result = this.ImportDefinition.Equals(other.ImportDefinition)
                && EqualityComparer<TypeRef>.Default.Equals(this.ComposablePartTypeRef, other.ComposablePartTypeRef)
                && EqualityComparer<MemberRef?>.Default.Equals(this.ImportingMemberRef, other.ImportingMemberRef)
                && EqualityComparer<ParameterRef?>.Default.Equals(this.ImportingParameterRef, other.ImportingParameterRef);

            return result;
        }

        public void ToString(TextWriter writer)
        {
            var indentingWriter = IndentingTextWriter.Get(writer);

            indentingWriter.WriteLine("ImportDefinition:");
            using (indentingWriter.Indent())
            {
                this.ImportDefinition.ToString(writer);
            }

            indentingWriter.WriteLine("ComposablePartType: {0}", this.ComposablePartType.FullName);
            indentingWriter.WriteLine("ImportingMember: {0}", this.ImportingMember);
            indentingWriter.WriteLine("ParameterInfo: {0}", this.ImportingParameter);
            indentingWriter.WriteLine("ImportingSiteType: {0}", this.ImportingSiteType);
        }

        internal void GetInputAssemblies(ISet<AssemblyName> assemblies, Func<Assembly, AssemblyName> nameRetriever)
        {
            Requires.NotNull(assemblies, nameof(assemblies));
            Requires.NotNull(nameRetriever, nameof(nameRetriever));

            this.ImportDefinition.GetInputAssemblies(assemblies, nameRetriever);
            this.ComposablePartTypeRef.GetInputAssemblies(assemblies);
            this.ImportingMemberRef?.GetInputAssemblies(assemblies);
            this.ImportingParameterRef?.GetInputAssemblies(assemblies);
            this.ImportingSiteTypeRef.GetInputAssemblies(assemblies);
            this.ComposablePartTypeRef.GetInputAssemblies(assemblies);
        }
    }
}
