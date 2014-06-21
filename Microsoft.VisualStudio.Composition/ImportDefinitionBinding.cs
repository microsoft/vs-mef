namespace Microsoft.VisualStudio.Composition
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using System.Text;
    using System.Threading.Tasks;
    using Validation;

    public class ImportDefinitionBinding : IEquatable<ImportDefinitionBinding>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ImportDefinitionBinding"/> class
        /// to represent an importing member.
        /// </summary>
        public ImportDefinitionBinding(ImportDefinition importDefinition, Type composablePartType, MemberInfo importingMember)
        {
            Requires.NotNull(importDefinition, "importDefinition");
            Requires.NotNull(composablePartType, "composablePartType");
            Requires.NotNull(importingMember, "importingMember");

            this.ImportDefinition = importDefinition;
            this.ComposablePartType = composablePartType;
            this.ImportingMember = importingMember;
            this.ImportingSiteType = ReflectionHelpers.GetMemberType(importingMember);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ImportDefinitionBinding"/> class
        /// to represent a parameter in an importing constructor.
        /// </summary>
        public ImportDefinitionBinding(ImportDefinition importDefinition, Type composablePartType, ParameterInfo importingConstructorParameter)
        {
            Requires.NotNull(importDefinition, "importDefinition");
            Requires.NotNull(composablePartType, "composablePartType");
            Requires.NotNull(importingConstructorParameter, "importingConstructorParameter");

            this.ImportDefinition = importDefinition;
            this.ComposablePartType = composablePartType;
            this.ImportingParameter = importingConstructorParameter;
            this.ImportingSiteType = importingConstructorParameter.ParameterType;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ImportDefinitionBinding"/> class
        /// to represent an imperative query into the container (no importing part).
        /// </summary>
        public ImportDefinitionBinding(ImportDefinition importDefinition)
        {
            Requires.NotNull(importDefinition, "importDefinition");

            this.ImportDefinition = importDefinition;
            this.ImportingSiteType = typeof(IEnumerable<>).MakeGenericType(typeof(ILazy<>).MakeGenericType(importDefinition.Contract.Type));
        }

        /// <summary>
        /// Gets the definition for this import.
        /// </summary>
        public ImportDefinition ImportDefinition { get; private set; }

        /// <summary>
        /// Gets the members this import is found on. Null for importing constructors.
        /// </summary>
        public MemberInfo ImportingMember { get; private set; }

        public ParameterInfo ImportingParameter { get; private set; }

        public Type ComposablePartType { get; private set; }

        /// <summary>
        /// Gets the actual type of the variable or member that will be assigned the result.
        /// This includes any Lazy, ExportFactory or collection wrappers.
        /// </summary>
        /// <value>Never null.</value>
        public Type ImportingSiteType { get; private set; }

        public Type ImportingSiteTypeWithoutCollection
        {
            get
            {
                return this.ImportDefinition.Cardinality == ImportCardinality.ZeroOrMore
                    ? PartDiscovery.GetElementTypeFromMany(this.ImportingSiteType)
                    : this.ImportingSiteType;
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
            get { return this.ImportingSiteTypeWithoutCollection.IsAnyLazyType(); }
        }

        public bool IsLazyConcreteType
        {
            get { return this.ImportingSiteTypeWithoutCollection.IsConcreteLazyType(); }
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

            return this.ImportDefinition.Equals(other.ImportDefinition)
                && EqualityComparer<Type>.Default.Equals(this.ComposablePartType, other.ComposablePartType)
                && EqualityComparer<MemberInfo>.Default.Equals(this.ImportingMember, other.ImportingMember)
                && EqualityComparer<ParameterInfo>.Default.Equals(this.ImportingParameter, other.ImportingParameter);
        }
    }
}
