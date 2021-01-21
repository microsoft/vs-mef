// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.Composition
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;

    [DebuggerDisplay("{" + nameof(DebuggerDisplay) + ",nq}")]
    public class Export
    {
        private readonly Lazy<object?> exportedValueGetter;

        public Export(string contractName, IReadOnlyDictionary<string, object?> metadata, Func<object?> exportedValueGetter)
            : this(new ExportDefinition(contractName, metadata), exportedValueGetter)
        {
        }

        public Export(ExportDefinition definition, Func<object?> exportedValueGetter)
            : this(definition, new Lazy<object?>(exportedValueGetter))
        {
        }

        public Export(ExportDefinition definition, Lazy<object?> exportedValueGetter)
        {
            Requires.NotNull(definition, nameof(definition));
            Requires.NotNull(exportedValueGetter, nameof(exportedValueGetter));

            this.Definition = definition;
            this.exportedValueGetter = exportedValueGetter;
        }

        public ExportDefinition Definition { get; private set; }

        /// <summary>
        /// Gets the metadata on the exported value.
        /// </summary>
        public IReadOnlyDictionary<string, object?> Metadata
        {
            get { return this.Definition.Metadata; }
        }

        /// <summary>
        /// Gets the exported value.
        /// </summary>
        /// <remarks>
        /// This may incur a value construction cost upon first retrieval.
        /// </remarks>
        public object? Value => this.ValueFilter(this.exportedValueGetter.Value);

        internal virtual object? ValueFilter(object? lazyValue) => lazyValue;

        private string DebuggerDisplay => this.Definition.ContractName;
    }
}
