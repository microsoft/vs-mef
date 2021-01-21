// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.Composition
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Globalization;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

    public class ComposedPartDiagnostic
    {
        public ComposedPartDiagnostic(ComposedPart part, string formattedMessage)
            : this(ImmutableHashSet.Create(part), formattedMessage)
        {
        }

        public ComposedPartDiagnostic(ComposedPart part, string unformattedMessage, params object?[] args)
            : this(part, string.Format(CultureInfo.CurrentCulture, unformattedMessage, args))
        {
        }

        public ComposedPartDiagnostic(IEnumerable<ComposedPart> parts, string formattedMessage)
        {
            Requires.NotNull(parts, nameof(parts));
            Requires.NotNullOrEmpty(formattedMessage, nameof(formattedMessage));

            this.Parts = ImmutableList.CreateRange(parts);
            this.Message = formattedMessage;
        }

        public ComposedPartDiagnostic(IEnumerable<ComposedPart> parts, string unformattedMessage, params object?[] args)
            : this(parts, string.Format(CultureInfo.CurrentCulture, unformattedMessage, args))
        {
        }

        public IReadOnlyCollection<ComposedPart> Parts { get; private set; }

        public string Message { get; private set; }
    }
}
