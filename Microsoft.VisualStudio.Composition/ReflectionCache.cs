namespace Microsoft.VisualStudio.Composition
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Linq;
    using System.Reflection;
    using System.Text;
    using System.Threading.Tasks;
    using Validation;

    internal class ReflectionCache
    {
        private readonly Dictionary<Assembly, ImmutableArray<Attribute>> assemblyAttributes = new Dictionary<Assembly, ImmutableArray<Attribute>>();

        private readonly Dictionary<MemberInfo, ImmutableArray<Attribute>> memberAttributes = new Dictionary<MemberInfo, ImmutableArray<Attribute>>();

        private readonly Dictionary<ParameterInfo, ImmutableArray<Attribute>> parameterAttributes = new Dictionary<ParameterInfo, ImmutableArray<Attribute>>();

        private readonly Dictionary<MemberInfo, Type> memberType = new Dictionary<MemberInfo, Type>();

        internal ImmutableArray<Attribute> GetCustomAttributes(Assembly assembly)
        {
            Requires.NotNull(assembly, "assembly");

            ImmutableArray<Attribute> result;
            lock (this.assemblyAttributes)
            {
                this.assemblyAttributes.TryGetValue(assembly, out result);
            }

            if (result.IsDefault)
            {
                result = assembly.GetCustomAttributes().ToImmutableArray();
                lock (this.assemblyAttributes)
                {
                    this.assemblyAttributes[assembly] = result;
                }
            }

            return result;
        }

        internal ImmutableArray<Attribute> GetCustomAttributes(MemberInfo member)
        {
            Requires.NotNull(member, "member");

            ImmutableArray<Attribute> result;
            lock (this.memberAttributes)
            {
                this.memberAttributes.TryGetValue(member, out result);
            }

            if (result.IsDefault)
            {
                result = member.GetCustomAttributes(false).ToImmutableArray();
                lock (this.memberAttributes)
                {
                    this.memberAttributes[member] = result;
                }
            }

            return result;
        }

        internal ImmutableArray<Attribute> GetCustomAttributes(ParameterInfo parameter)
        {
            Requires.NotNull(parameter, "parameter");

            ImmutableArray<Attribute> result;
            lock (this.memberAttributes)
            {
                this.parameterAttributes.TryGetValue(parameter, out result);
            }

            if (result.IsDefault)
            {
                result = parameter.GetCustomAttributes(false).ToImmutableArray();
                lock (this.memberAttributes)
                {
                    this.parameterAttributes[parameter] = result;
                }
            }

            return result;
        }

        internal Type GetMemberType(MemberInfo fieldOrProperty)
        {
            Requires.NotNull(fieldOrProperty, "fieldOrProperty");

            Type result;
            lock (this.memberType)
            {
                this.memberType.TryGetValue(fieldOrProperty, out result);
            }

            if (result == null)
            {
                var property = fieldOrProperty as PropertyInfo;
                if (property != null)
                {
                    result = property.PropertyType;
                }
                else
                {
                    var field = fieldOrProperty as FieldInfo;
                    if (field != null)
                    {
                        result = field.FieldType;
                    }
                }

                if (result == null)
                {
                    throw new ArgumentException("Unexpected member type.");
                }

                lock (this.memberType)
                {
                    this.memberType[fieldOrProperty] = result;
                }
            }

            return result;
        }
    }
}
