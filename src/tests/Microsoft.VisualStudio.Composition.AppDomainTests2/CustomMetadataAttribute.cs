// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.VisualStudio.Composition.AppDomainTests2
{
    using System;
    using System.Collections.Generic;
    using System.Composition;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

    [MetadataAttribute]
    [AttributeUsage(AttributeTargets.Class)]
    public class CustomMetadataAttribute : Attribute
    {
        public CustomEnum CustomValue
        {
            get { return CustomEnum.Value1; }
        }
    }

    public enum CustomEnum
    {
        Value1,
    }
}
