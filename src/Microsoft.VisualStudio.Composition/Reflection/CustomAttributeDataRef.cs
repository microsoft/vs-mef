namespace Microsoft.VisualStudio.Composition.Reflection
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Linq;
    using System.Reflection;
    using System.Text;
    using System.Threading.Tasks;

    public struct CustomAttributeDataRef
    {
        public CustomAttributeDataRef(CustomAttributeData data)
            : this()
        {
            Requires.NotNull(data, nameof(data));

            this.Constructor = new ConstructorRef(data.Constructor);
            this.PositionalArguments = data.ConstructorArguments.Select(a => a.Value).ToImmutableArray();
            this.NamedArguments = ImmutableDictionary.CreateRange(
                data.NamedArguments.Select(
                    a => new KeyValuePair<MemberRef, object>(MemberRef.Get(a.MemberInfo), a.TypedValue.Value)));
        }

        public CustomAttributeDataRef(ConstructorRef constructor, IReadOnlyList<object> positionalArguments, IReadOnlyDictionary<MemberRef, object> namedArguments)
            : this()
        {
            this.Constructor = constructor;
            this.PositionalArguments = positionalArguments;
            this.NamedArguments = namedArguments;
        }

        /// <summary>
        /// Gets the constructor invoked by the attribute.
        /// </summary>
        public ConstructorRef Constructor { get; private set; }

        /// <summary>
        /// Gets the arguments passed directly to the constructor.
        /// </summary>
        /// <remarks>
        /// All these values are restricted to the set of types described by
        /// the C# language specification 17.1.3.
        /// </remarks>
        public IReadOnlyList<object> PositionalArguments { get; private set; }

        /// <summary>
        /// Gets the named property or field setters that are invoked after the constructor
        /// as part of the attribute invocation syntax allowed in the CLR.
        /// </summary>
        /// <remarks>
        /// All these values are restricted to the set of types described by
        /// the C# language specification 17.1.3.
        /// </remarks>
        public IReadOnlyDictionary<MemberRef, object> NamedArguments { get; private set; }

        public bool IsEmpty
        {
            get { return this.Constructor.IsEmpty; }
        }

        public Attribute Instantiate()
        {
            Verify.Operation(!this.IsEmpty, Strings.InstanceEmpty);
            Attribute attribute = (Attribute)this.Constructor.Resolve().Invoke(this.PositionalArguments.ToArray());
            foreach (var namedArgument in this.NamedArguments)
            {
                if (namedArgument.Key.IsField)
                {
                    namedArgument.Key.Field.Resolve().SetValue(attribute, namedArgument.Value);
                }
                else
                {
                    namedArgument.Key.Property.Resolve().SetValue(attribute, namedArgument.Value);
                }
            }

            return attribute;
        }
    }
}
