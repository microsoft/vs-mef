﻿// Copyright (c) Microsoft Corporation. All rights reserved.
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

    [MessagePackObject(true)]
    public class ImportDefinitionBinding : IEquatable<ImportDefinitionBinding>
    {
        [IgnoreMember]
        private bool? isLazy;

        [IgnoreMember]

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

       // [Key(0)]
        public ImportDefinition ImportDefinition { get; private set; }

        /// <summary>
        /// Gets the member this import is found on. Null for importing constructors.
        /// </summary>
        //[Key(1)] we need to  use the custome serilizer to seralize this property keeping ignore for now - Ankit TODO ths is onverting a MemberInfo object to a string and back is not straightforward because MemberInfo is an abstract class representing various types of members like fields, properties, methods, etc., and there's no single string representation that encapsulates all of its properties.
        [IgnoreMember] 
        public MemberInfo? ImportingMember => this.ImportingMemberRef?.MemberInfo;

        /// <summary>
        /// Gets the member this import is found on. Null for importing constructors.
        /// </summary>
      //  [Key(2)]
        public MemberRef? ImportingMemberRef { get; private set; }

        //[Key(3)]
        [IgnoreMember] // no public constructor and can be derived from ImportingParameterRef
        public ParameterInfo? ImportingParameter => this.ImportingParameterRef?.ParameterInfo;

      //  [Key(4)]
        public ParameterRef? ImportingParameterRef { get; private set; }

     //   [Key(5)]
        public Type ComposablePartType => this.ComposablePartTypeRef.ResolvedType;

     //   [Key(6)]
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

     //   [Key(9)]
        public TypeRef ImportingSiteTypeWithoutCollectionRef { get; }

     //   [Key(10)]
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

    //    [Key(13)]
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

    //    [Key(14)]
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
