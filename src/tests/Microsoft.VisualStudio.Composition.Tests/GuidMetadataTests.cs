// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.Composition.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Composition;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Xunit;
    using MefV1 = System.ComponentModel.Composition;

    public class GuidMetadataTests
    {
        [MefFact(CompositionEngines.V3EmulatingV1 | CompositionEngines.V3EmulatingV2)]
        public void GuidMetadata(IContainer container)
        {
            var part = container.GetExportedValue<ImportingPart>();
            Assert.Equal(Guid.Parse("{A97C49F7-8E06-41D1-A0A5-2E0135E17A2E}"), part.ImportingProperty.Metadata.ServiceGuid);
        }

        [Export, MefV1.Export]
        public class ImportingPart
        {
            [Import, MefV1.Import]
            public Lazy<ExportedPart, IProfferedServiceMetadataView> ImportingProperty { get; set; } = null!;
        }

        [ExportVsProfferedProjectService("{A97C49F7-8E06-41D1-A0A5-2E0135E17A2E}")]
        [ExportVsProfferedProjectServiceV1("{A97C49F7-8E06-41D1-A0A5-2E0135E17A2E}")]
        public class ExportedPart { }

        public interface IProfferedServiceMetadataView
        {
            /// <summary>
            /// Gets the service GUID.
            /// </summary>
            Guid ServiceGuid { get; }
        }

        [MetadataAttribute]
        [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method | AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Interface, AllowMultiple = false)]
        public class ExportVsProfferedProjectServiceAttribute : ExportAttribute
        {
            /// <summary>
            /// Initializes a new instance of the <see cref="ExportVsProfferedProjectServiceAttribute"/> class.
            /// </summary>
            /// <param name="serviceGuid">The identifier by which the service is proferred.</param>
            public ExportVsProfferedProjectServiceAttribute(string serviceGuid)
            {
                this.ServiceGuid = Guid.Parse(serviceGuid);
            }

            /// <summary>
            /// Gets the identifier by which the service is proffered.
            /// </summary>
            public Guid ServiceGuid { get; private set; }
        }

        [MefV1.MetadataAttribute]
        [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method | AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Interface, AllowMultiple = false)]
        public class ExportVsProfferedProjectServiceV1Attribute : MefV1.ExportAttribute
        {
            /// <summary>
            /// Initializes a new instance of the <see cref="ExportVsProfferedProjectServiceV1Attribute"/> class.
            /// </summary>
            /// <param name="serviceGuid">The identifier by which the service is proferred.</param>
            public ExportVsProfferedProjectServiceV1Attribute(string serviceGuid)
            {
                this.ServiceGuid = Guid.Parse(serviceGuid);
            }

            /// <summary>
            /// Gets the identifier by which the service is proffered.
            /// </summary>
            public Guid ServiceGuid { get; private set; }
        }
    }
}