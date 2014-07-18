namespace Microsoft.VisualStudio.Composition.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.VisualStudio.Composition.AssemblyDiscoveryTests;
    using Xunit;
    using System.Composition;
    using MefV1 = System.ComponentModel.Composition;

    public abstract class AttributedPartDiscoveryTestBase
    {
        protected abstract PartDiscovery DiscoveryService { get; }

        [Fact]
        public void NonSharedPartProduction()
        {
            ComposablePartDefinition result = this.DiscoveryService.CreatePart(typeof(NonSharedPart));
            Assert.NotNull(result);
            Assert.Equal(1, result.ExportedTypes.Count);
            Assert.Equal(0, result.ImportingMembers.Count);
            Assert.False(result.IsShared);
        }

        [Fact]
        public void SharedPartProduction()
        {
            ComposablePartDefinition result = this.DiscoveryService.CreatePart(typeof(SharedPart));
            Assert.NotNull(result);
            Assert.Equal(1, result.ExportedTypes.Count);
            Assert.Equal(0, result.ImportingMembers.Count);
            Assert.True(result.IsShared);
        }

        [Fact]
        public async Task AssemblyDiscoveryFindsTopLevelParts()
        {
            var result = await this.DiscoveryService.CreatePartsAsync(typeof(NonDiscoverablePart).Assembly);
            Assert.True(result.Parts.Any(p => p.Type.IsEquivalentTo(typeof(DiscoverablePart1))));
            Assert.True(result.Parts.Any(p => p.Type.IsEquivalentTo(typeof(DiscoverablePart2))));
        }

        [Fact]
        public async Task AssemblyDiscoveryOmitsNonDiscoverableParts()
        {
            var result = await this.DiscoveryService.CreatePartsAsync(typeof(NonDiscoverablePart).Assembly);
            Assert.False(result.Parts.Any(p => p.Type.IsEquivalentTo(typeof(NonPart))));
            Assert.False(result.Parts.Any(p => p.Type.IsEquivalentTo(typeof(NonDiscoverablePart))));
        }

        [Fact]
        public async Task AssemblyDiscoveryFindsNestedParts()
        {
            var result = await this.DiscoveryService.CreatePartsAsync(typeof(NonDiscoverablePart).Assembly);
            Assert.True(result.Parts.Any(p => p.Type.IsEquivalentTo(typeof(OuterClass.NestedPart))));
        }

        [Fact]
        public async Task AssemblyGetTypesError()
        {
            var assembly = new SketchyAssembly();
            var result = await this.DiscoveryService.CreatePartsAsync(assembly);
        }

        [Export]
        [MefV1.Export, MefV1.PartCreationPolicy(MefV1.CreationPolicy.NonShared)]
        public class NonSharedPart { }

        [Export, Shared]
        [MefV1.Export]
        public class SharedPart { }

        #region Indexer overloading tests

        [Fact]
        public void IndexerInDerivedAndBase()
        {
            var part = this.DiscoveryService.CreatePart(typeof(DerivedTypeWithIndexer));
        }

        public class BaseTypeWithIndexer
        {
            public virtual string this[int index]
            {
                get { throw new NotImplementedException(); }
                set { throw new NotImplementedException(); }
            }

            public virtual string this[string index]
            {
                get { throw new NotImplementedException(); }
                set { throw new NotImplementedException(); }
            }
        }

        [Export]
        public class DerivedTypeWithIndexer : BaseTypeWithIndexer
        {
            public override string this[int index]
            {
                get { throw new NotImplementedException(); }
                set { throw new NotImplementedException(); }
            }
        }

        #endregion

        
        [AttributeUsage(AttributeTargets.All)]
        private class SketchyAttribute : Attribute
        {
            public SketchyAttribute() { throw new ArgumentException(); }
        }

        [Sketchy]
        private class SketchyType: Type
        {

            public override IEnumerable<CustomAttributeData> CustomAttributes
            {
                get
                {
                    throw new ArgumentException();
                }
            }

            public override Assembly Assembly
            {
                get { throw new NotImplementedException(); }
            }

            public override string AssemblyQualifiedName
            {
                get { throw new NotImplementedException(); }
            }

            public override Type BaseType
            {
                get { throw new NotImplementedException(); }
            }

            public override string FullName
            {
                get { throw new NotImplementedException(); }
            }

            public override Guid GUID
            {
                get { throw new NotImplementedException(); }
            }

            protected override TypeAttributes GetAttributeFlagsImpl()
            {
                throw new NotImplementedException();
            }

            protected override ConstructorInfo GetConstructorImpl(BindingFlags bindingAttr, Binder binder, CallingConventions callConvention, Type[] types, ParameterModifier[] modifiers)
            {
                throw new NotImplementedException();
            }

            public override ConstructorInfo[] GetConstructors(BindingFlags bindingAttr)
            {
                throw new NotImplementedException();
            }

            public override Type GetElementType()
            {
                throw new NotImplementedException();
            }

            public override EventInfo GetEvent(string name, BindingFlags bindingAttr)
            {
                throw new NotImplementedException();
            }

            public override EventInfo[] GetEvents(BindingFlags bindingAttr)
            {
                throw new NotImplementedException();
            }

            public override FieldInfo GetField(string name, BindingFlags bindingAttr)
            {
                throw new NotImplementedException();
            }

            public override FieldInfo[] GetFields(BindingFlags bindingAttr)
            {
                throw new NotImplementedException();
            }

            public override Type GetInterface(string name, bool ignoreCase)
            {
                throw new NotImplementedException();
            }

            public override Type[] GetInterfaces()
            {
                throw new NotImplementedException();
            }

            public override MemberInfo[] GetMembers(BindingFlags bindingAttr)
            {
                throw new NotImplementedException();
            }

            protected override MethodInfo GetMethodImpl(string name, BindingFlags bindingAttr, Binder binder, CallingConventions callConvention, Type[] types, ParameterModifier[] modifiers)
            {
                throw new NotImplementedException();
            }

            public override MethodInfo[] GetMethods(BindingFlags bindingAttr)
            {
                throw new NotImplementedException();
            }

            public override Type GetNestedType(string name, BindingFlags bindingAttr)
            {
                throw new NotImplementedException();
            }

            public override Type[] GetNestedTypes(BindingFlags bindingAttr)
            {
                throw new NotImplementedException();
            }

            public override PropertyInfo[] GetProperties(BindingFlags bindingAttr)
            {
                throw new NotImplementedException();
            }

            protected override PropertyInfo GetPropertyImpl(string name, BindingFlags bindingAttr, Binder binder, Type returnType, Type[] types, ParameterModifier[] modifiers)
            {
                throw new NotImplementedException();
            }

            protected override bool HasElementTypeImpl()
            {
                throw new NotImplementedException();
            }

            public override object InvokeMember(string name, BindingFlags invokeAttr, Binder binder, object target, object[] args, ParameterModifier[] modifiers, System.Globalization.CultureInfo culture, string[] namedParameters)
            {
                throw new NotImplementedException();
            }

            protected override bool IsArrayImpl()
            {
                throw new NotImplementedException();
            }

            protected override bool IsByRefImpl()
            {
                throw new NotImplementedException();
            }

            protected override bool IsCOMObjectImpl()
            {
                throw new NotImplementedException();
            }

            protected override bool IsPointerImpl()
            {
                throw new NotImplementedException();
            }

            protected override bool IsPrimitiveImpl()
            {
                throw new NotImplementedException();
            }

            public override Module Module
            {
                get { throw new NotImplementedException(); }
            }

            public override string Namespace
            {
                get { throw new NotImplementedException(); }
            }

            public override Type UnderlyingSystemType
            {
                get { throw new NotImplementedException(); }
            }

            public override object[] GetCustomAttributes(Type attributeType, bool inherit)
            {
                throw new NotImplementedException();
            }

            public override object[] GetCustomAttributes(bool inherit)
            {
                throw new NotImplementedException();
            }

            public override bool IsDefined(Type attributeType, bool inherit)
            {
                throw new NotImplementedException();
            }

            public override string Name
            {
                get { throw new NotImplementedException(); }
            }
            public override IList<CustomAttributeData> GetCustomAttributesData()
            {
                throw new ArgumentException();
            }
        }

        private class SketchyAssembly : Assembly
        {
            public override System.Type[] GetTypes()
            {
                return new Type[] { typeof(SketchyType) };
            }

            public override Type[] GetExportedTypes()
            {
                return this.GetTypes();
            }
        }
    }
}
