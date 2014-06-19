namespace Microsoft.VisualStudio.Composition
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using System.Text;
    using System.Threading.Tasks;
    using Validation;

    public class Import : IEquatable<Import>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Import"/> class
        /// to represent an importing member.
        /// </summary>
        public Import(ImportDefinition importDefinition, Type composablePartType, MemberInfo importingMember)
        {
            Requires.NotNull(importDefinition, "importDefinition");
            Requires.NotNull(composablePartType, "composablePartType");
            Requires.NotNull(importingMember, "importingMember");

            this.ImportDefinition = importDefinition;
            this.ComposablePartType = composablePartType;
            this.ImportingMember = importingMember;
           
            var importingType = ReflectionHelpers.GetMemberType(importingMember);
            this.IsLazy = importingType.IsAnyLazyType();
            this.IsLazyConcreteType = importingType.IsConcreteLazyType();
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Import"/> class
        /// to represent a parameter in an importing constructor.
        /// </summary>
        public Import(ImportDefinition importDefinition, Type composablePartType, ParameterInfo importingConstructorParameter)
        {
            Requires.NotNull(importDefinition, "importDefinition");
            Requires.NotNull(composablePartType, "composablePartType");
            Requires.NotNull(importingConstructorParameter, "importingConstructorParameter");

            this.ImportDefinition = importDefinition;
            this.ComposablePartType = composablePartType;
            this.ImportingParameter = importingConstructorParameter;

            var importingType = importingConstructorParameter.ParameterType;
            this.IsLazy = importingType.IsAnyLazyType();
            this.IsLazyConcreteType = importingType.IsConcreteLazyType();
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Import"/> class
        /// to represent an imperative query into the container (no importing part).
        /// </summary>
        public Import(ImportDefinition importDefinition)
        {
            Requires.NotNull(importDefinition, "importDefinition");

            this.ImportDefinition = importDefinition;
         
            this.IsLazy = true;
            this.IsLazyConcreteType = false;
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

        public Type ImportingMemberOrParameterType
        {
            get
            {
                if (this.ImportingParameter != null)
                {
                    return this.ImportingParameter.ParameterType;
                }

                if (this.ImportingMember != null)
                {
                    return ReflectionHelpers.GetMemberType(this.ImportingMember);
                }

                return null;
            }
        }

        public bool IsLazy { get; private set; }

        public bool IsLazyConcreteType { get; private set; }

        public Type MetadataType
        {
            get
            {
                if ((this.IsLazy || this.IsExportFactory) && this.ImportingMemberOrParameterType != null)
                {
                    var args = this.ImportingMemberOrParameterType.GetTypeInfo().GenericTypeArguments;
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
            get { return this.ImportingMemberOrParameterType.IsExportFactoryTypeV1() || this.ImportingMemberOrParameterType.IsExportFactoryTypeV2(); }
        }

        public Type ExportFactoryType
        {
            get { return this.IsExportFactory ? this.ImportingMemberOrParameterType : null; }
        }

        public override int GetHashCode()
        {
            return this.ImportDefinition.GetHashCode();
        }

        public override bool Equals(object obj)
        {
            return this.Equals(obj as Import);
        }

        public bool Equals(Import other)
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
