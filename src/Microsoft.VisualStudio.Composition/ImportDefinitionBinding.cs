namespace Microsoft.VisualStudio.Composition
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.VisualStudio.Composition.Reflection;

    public class ImportDefinitionBinding : IEquatable<ImportDefinitionBinding>
    {
        private bool? isLazy;

        private Type importingSiteTypeWithoutCollection;

        private TypeRef importingSiteTypeRef;

        /// <summary>
        /// Initializes a new instance of the <see cref="ImportDefinitionBinding"/> class
        /// to represent an importing member.
        /// </summary>
        public ImportDefinitionBinding(ImportDefinition importDefinition, TypeRef composablePartType, MemberRef importingMember)
        {
            Requires.NotNull(importDefinition, "importDefinition");
            Requires.NotNull(composablePartType, "composablePartType");

            this.ImportDefinition = importDefinition;
            this.ComposablePartTypeRef = composablePartType;
            this.ImportingMemberRef = importingMember;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ImportDefinitionBinding"/> class
        /// to represent a parameter in an importing constructor.
        /// </summary>
        public ImportDefinitionBinding(ImportDefinition importDefinition, TypeRef composablePartType, ParameterRef importingConstructorParameter)
        {
            Requires.NotNull(importDefinition, "importDefinition");
            Requires.NotNull(composablePartType, "composablePartType");

            this.ImportDefinition = importDefinition;
            this.ComposablePartTypeRef = composablePartType;
            this.ImportingParameterRef = importingConstructorParameter;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ImportDefinitionBinding"/> class
        /// to represent an imperative query into the container (no importing part).
        /// </summary>
        public ImportDefinitionBinding(ImportDefinition importDefinition, TypeRef contractType)
        {
            Requires.NotNull(importDefinition, "importDefinition");
            Requires.NotNull(contractType, "contractType");

            this.ImportDefinition = importDefinition;
            this.importingSiteTypeRef = TypeRef.Get(typeof(IEnumerable<>).MakeGenericType(typeof(Lazy<>).MakeGenericType(contractType.Resolve())));
        }

        /// <summary>
        /// Gets the definition for this import.
        /// </summary>
        public ImportDefinition ImportDefinition { get; private set; }

        /// <summary>
        /// Gets the member this import is found on. Null for importing constructors.
        /// </summary>
        public MemberInfo ImportingMember
        {
            get { return this.ImportingMemberRef.Resolve(); }
        }

        /// <summary>
        /// Gets the member this import is found on. Null for importing constructors.
        /// </summary>
        public MemberRef ImportingMemberRef { get; private set; }

        public ParameterInfo ImportingParameter
        {
            get { return this.ImportingParameterRef.Resolve(); }
        }

        public ParameterRef ImportingParameterRef { get; private set; }

        public Type ComposablePartType
        {
            get { return this.ComposablePartTypeRef.Resolve(); }
        }

        public TypeRef ComposablePartTypeRef { get; private set; }

        /// <summary>
        /// Gets the actual type of the variable or member that will be assigned the result.
        /// This includes any Lazy, ExportFactory or collection wrappers.
        /// </summary>
        /// <value>Never null.</value>
        public Type ImportingSiteType
        {
            get { return this.ImportingSiteTypeRef.Resolve(); }
        }

        /// <summary>
        /// Gets the actual type of the variable or member that will be assigned the result.
        /// This includes any Lazy, ExportFactory or collection wrappers.
        /// </summary>
        /// <value>Never null.</value>
        public TypeRef ImportingSiteTypeRef
        {
            get
            {
                if (this.importingSiteTypeRef == null)
                {
                    if (!this.ImportingMemberRef.IsEmpty)
                    {
                        this.importingSiteTypeRef = TypeRef.Get(ReflectionHelpers.GetMemberType(this.ImportingMemberRef.Resolve()));
                    }
                    else if (!this.ImportingParameterRef.IsEmpty)
                    {
                        this.importingSiteTypeRef = TypeRef.Get(this.ImportingParameterRef.Resolve().ParameterType);
                    }
                    else
                    {
                        throw Assumes.NotReachable();
                    }
                }

                return this.importingSiteTypeRef;
            }
        }

        public Type ImportingSiteTypeWithoutCollection
        {
            get
            {
                if (this.importingSiteTypeWithoutCollection == null)
                {
                    this.importingSiteTypeWithoutCollection = this.ImportDefinition.Cardinality == ImportCardinality.ZeroOrMore
                        ? PartDiscovery.GetElementTypeFromMany(this.ImportingSiteType)
                        : this.ImportingSiteType;
                }

                return this.importingSiteTypeWithoutCollection;
            }
        }

        /// <summary>
        /// Gets the type of the member, with the ImportMany collection and Lazy/ExportFactory stripped off, when present.
        /// </summary>
        public Type ImportingSiteElementType
        {
            get
            {
                return PartDiscovery.GetTypeIdentityFromImportingType(this.ImportingSiteType, this.ImportDefinition.Cardinality == ImportCardinality.ZeroOrMore);
            }
        }

        public bool IsLazy
        {
            get
            {
                if (!this.isLazy.HasValue)
                {
                    this.isLazy = this.ImportingSiteTypeWithoutCollection.IsAnyLazyType();
                }

                return this.isLazy.Value;
            }
        }

        public Type MetadataType
        {
            get
            {
                if (this.IsLazy || this.IsExportFactory)
                {
                    var args = this.ImportingSiteTypeWithoutCollection.GetTypeInfo().GenericTypeArguments;
                    if (args.Length == 2)
                    {
                        return args[1];
                    }
                }

                return null;
            }
        }

        public bool IsExportFactory
        {
            get { return this.ImportingSiteTypeWithoutCollection.IsExportFactoryType(); }
        }

        public Type ExportFactoryType
        {
            get { return this.IsExportFactory ? this.ImportingSiteTypeWithoutCollection : null; }
        }

        public override int GetHashCode()
        {
            return this.ImportDefinition.GetHashCode();
        }

        public override bool Equals(object obj)
        {
            return this.Equals(obj as ImportDefinitionBinding);
        }

        public bool Equals(ImportDefinitionBinding other)
        {
            if (other == null)
            {
                return false;
            }

            bool result = this.ImportDefinition.Equals(other.ImportDefinition)
                && EqualityComparer<TypeRef>.Default.Equals(this.ComposablePartTypeRef, other.ComposablePartTypeRef)
                && EqualityComparer<MemberRef>.Default.Equals(this.ImportingMemberRef, other.ImportingMemberRef)
                && EqualityComparer<ParameterRef>.Default.Equals(this.ImportingParameterRef, other.ImportingParameterRef);

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

        internal void GetInputAssemblies(ISet<AssemblyName> assemblies)
        {
            Requires.NotNull(assemblies, "assemblies");

            this.ImportDefinition.GetInputAssemblies(assemblies);
            this.ComposablePartTypeRef.GetInputAssemblies(assemblies);
            this.ImportingMemberRef.GetInputAssemblies(assemblies);
            this.ImportingParameterRef.GetInputAssemblies(assemblies);
            this.ImportingSiteTypeRef.GetInputAssemblies(assemblies);
            this.ComposablePartTypeRef.GetInputAssemblies(assemblies);
        }
    }
}
