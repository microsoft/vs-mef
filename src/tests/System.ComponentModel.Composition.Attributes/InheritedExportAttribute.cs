// Copyright (c) Microsoft. All rights reserved.

#if NET40 || NET45

using System.ComponentModel.Composition;
using System.Runtime.CompilerServices;

[assembly: TypeForwardedTo(typeof(InheritedExportAttribute))]

#else

namespace System.ComponentModel.Composition
{
    using System;

    /// <summary>Specifies that a type provides a particular export, and that subclasses of that type will also provide that export.</summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface, AllowMultiple = true, Inherited = true)]
    public class InheritedExportAttribute : ExportAttribute
    {
        /// <summary>Initializes a new instance of the <see cref="InheritedExportAttribute" /> class. </summary>
        public InheritedExportAttribute()
            : this(null, null)
        {
        }

        /// <summary>Initializes a new instance of the <see cref="InheritedExportAttribute" /> class with the specified contract type.</summary>
        /// <param name="contractType">The type of the contract.</param>
        public InheritedExportAttribute(Type contractType)
            : this(null, contractType)
        {
        }

        /// <summary>Initializes a new instance of the <see cref="InheritedExportAttribute" /> class with the specified contract name.</summary>
        /// <param name="contractName">The name of the contract.</param>
        public InheritedExportAttribute(string contractName)
            : this(contractName, null)
        {
        }

        /// <summary>Initializes a new instance of the <see cref="InheritedExportAttribute" /> class with the specified contract name and type.</summary>
        /// <param name="contractName">The name of the contract.</param>
        /// <param name="contractType">The type of the contract.</param>
        public InheritedExportAttribute(string contractName, Type contractType)
            : base(contractName, contractType)
        {
        }
    }
}

#endif
