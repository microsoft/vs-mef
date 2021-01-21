// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace System.Runtime.InteropServices
{
    [AttributeUsage(AttributeTargets.Assembly)]
    internal sealed class ImportedFromTypeLibAttribute : Attribute
    {
        public ImportedFromTypeLibAttribute(string tlbFile)
        {
            this.Value = tlbFile;
        }

        public string Value { get; }
    }
}