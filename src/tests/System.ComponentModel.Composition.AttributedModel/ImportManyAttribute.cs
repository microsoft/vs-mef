// Copyright (c) Microsoft. All rights reserved.

#if NET40 || NET45

using System.ComponentModel.Composition;
using System.Runtime.CompilerServices;

[assembly: TypeForwardedTo(typeof(ImportManyAttribute))]

#else

namespace System.ComponentModel.Composition
{
    using System;

    /// <summary>
    ///     Specifies that a property, field, or parameter imports a particular set of exports.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter, AllowMultiple = false, Inherited = false)]
    public class ImportManyAttribute : Attribute
    {
        /// <summary>
        ///     Initializes a new instance of the <see cref="ImportManyAttribute"/> class, importing the
        ///     set of exports with the default contract name.
        /// </summary>
        public ImportManyAttribute()
              : this((string)null)
        {
        }

        /// <summary>
        ///     Initializes a new instance of the <see cref="ImportManyAttribute"/> class, importing the
        ///     set of exports with the contract name derived from the specified type.
        /// </summary>
        /// <param name="contractType">
        ///     A <see cref="Type"/> of which to derive the contract name of the exports to import, or
        ///     <see langword="null"/> to use the default contract name.
        /// </param>
        public ImportManyAttribute(Type contractType)
            : this((string)null, contractType)
        {
        }

        /// <summary>
        ///     Initializes a new instance of the <see cref="ImportManyAttribute"/> class, importing the
        ///     set of exports with the specified contract name.
        /// </summary>
        /// <param name="contractName">
        ///      A <see cref="string"/> containing the contract name of the exports to import, or
        ///      <see langword="null"/> or an empty string ("") to use the default contract name.
        /// </param>
        public ImportManyAttribute(string contractName)
            : this(contractName, (Type)null)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ImportManyAttribute"/> class,
        /// importing the set of exports with the specified contract name and contract
        /// type.
        /// </summary>
        /// <param name="contractName">
        ///     The contract name of the exports to import, or null or an empty string ("") to
        ///     use the default contract name.
        /// </param>
        /// <param name="contractType">
        ///     The type of the export to import.
        /// </param>
        public ImportManyAttribute(string contractName, Type contractType)
        {
            this.ContractName = contractName;
            this.ContractType = contractType;
        }

        /// <summary>
        ///     Gets the contract name of the exports to import.
        /// </summary>
        public string ContractName { get; }

        /// <summary>
        ///     Gets the contract type of the export to import.
        /// </summary>
        public Type ContractType { get; }

        /// <summary>
        ///     Gets or sets a value indicating whether the property or field will be recomposed
        ///     when exports that provide the same contract that this import expects, have changed
        ///     in the container.
        /// </summary>
        public bool AllowRecomposition { get; set; }

        /// <summary>
        ///     Gets or sets a value indicating that the importer requires a specific
        ///     <see cref="CreationPolicy"/> for the exports used to satisfy this import. T
        /// </summary>
        public CreationPolicy RequiredCreationPolicy { get; set; }

        /// <summary>
        ///     Gets or sets a value indicating that the importer indicating that the composition engine
        ///     either should satisfy exports from the local or no local scope.
        /// </summary>
        public ImportSource Source { get; set; }
    }
}

#endif
