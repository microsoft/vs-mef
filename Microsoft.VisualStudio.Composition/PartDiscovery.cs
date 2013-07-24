namespace Microsoft.VisualStudio.Composition
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using System.Text;
    using System.Threading.Tasks;
    using Validation;

    public abstract class PartDiscovery
    {
        /// <summary>
        /// Reflects on a type and returns metadata on its role as a MEF part, if applicable.
        /// </summary>
        /// <param name="partType">The type to reflect over.</param>
        /// <returns>A new instance of <see cref="ComposablePartDefinition"/> if <paramref name="partType"/>
        /// represents a MEF part; otherwise <c>null</c>.</returns>
        public abstract ComposablePartDefinition CreatePart(Type partType);

        /// <summary>
        /// Reflects over an assembly and produces MEF parts for every applicable type.
        /// </summary>
        /// <param name="assembly">The assembly to search for MEF parts.</param>
        /// <returns>A sequence of generated parts.</returns>
        public abstract IReadOnlyCollection<ComposablePartDefinition> CreateParts(Assembly assembly);

        protected internal static Type GetElementFromImportingMemberType(Type type, bool importMany)
        {
            Requires.NotNull(type, "type");

            if (importMany)
            {
                type = GetElementTypeFromMany(type);
            }

            if (type.IsAnyLazyType() || type.IsExportFactoryTypeV1() || type.IsExportFactoryTypeV2())
            {
                return type.GetGenericArguments()[0];
            }

            return type;
        }

        protected internal static Type GetElementTypeFromMany(Type type)
        {
            Requires.NotNull(type, "type");

            if (type.HasElementType)
            {
                type = type.GetElementType(); // T[] -> T
            }
            else
            {
                type = type.GetGenericArguments()[0]; // IEnumerable<T> -> T
            }

            return type;
        }

        protected static ConstructorInfo GetImportingConstructor(Type type, Type importingConstructorAttributeType, bool publicOnly)
        {
            Requires.NotNull(type, "type");
            Requires.NotNull(importingConstructorAttributeType, "importingConstructorAttributeType");

            var flags = BindingFlags.Instance|BindingFlags.Public ;
            if (!publicOnly)
            {
                flags |= BindingFlags.NonPublic;
            }

            var ctors = type.GetConstructors(flags);
            var taggedCtor = ctors.SingleOrDefault(ctor => ctor.GetCustomAttribute(importingConstructorAttributeType) != null);
            var defaultCtor = ctors.SingleOrDefault(ctor => ctor.GetParameters().Length == 0);
            var importingCtor = taggedCtor ?? defaultCtor;
            Verify.Operation(importingCtor != null, "No importing constructor found.");
            return importingCtor;
        }

        internal static bool IsImportManyCollectionTypeCreateable(ImportDefinition importDefinition)
        {
            Requires.NotNull(importDefinition, "importDefinition");

            var collectionType = importDefinition.MemberType;
            var elementType = importDefinition.MemberWithoutManyWrapper;
            var icollectionOfT = typeof(ICollection<>).MakeGenericType(elementType);
            var ienumerableOfT = typeof(IEnumerable<>).MakeGenericType(elementType);
            var ilistOfT = typeof(IList<>).MakeGenericType(elementType);

            if (collectionType.IsArray || collectionType.IsEquivalentTo(ienumerableOfT) || collectionType.IsEquivalentTo(ilistOfT) || collectionType.IsEquivalentTo(icollectionOfT))
            {
                return true;
            }

            Verify.Operation(icollectionOfT.IsAssignableFrom(collectionType), "Collection type must derive from ICollection<T>");

            var defaultCtor = collectionType.GetConstructor(new Type[0]);
            if (defaultCtor != null && defaultCtor.IsPublic)
            {
                return true;
            }

            return false;
        }
    }
}
