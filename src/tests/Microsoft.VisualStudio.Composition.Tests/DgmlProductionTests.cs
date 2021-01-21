// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.Composition.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Composition;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using System.Xml.Linq;
    using Xunit;
    using Xunit.Abstractions;

    public class DgmlProductionTests
    {
        internal const string Namespace = "http://schemas.microsoft.com/vs/2009/dgml";

        private readonly ITestOutputHelper output;

        public DgmlProductionTests(ITestOutputHelper output)
        {
            this.output = output;
        }

        #region Basic DGML

        [Fact]
        public async Task CreateDgmlFromConfiguration()
        {
            XDocument dgml = await this.ProduceDgmlHelperAsync(
                typeof(Exporter),
                typeof(Importer));

            var nodes = dgml.Root.Element(XName.Get("Nodes", Namespace)).Elements(XName.Get("Node", Namespace));
            var links = dgml.Root.Element(XName.Get("Links", Namespace)).Elements(XName.Get("Link", Namespace));
            var exportingPartNode = nodes.Single(e => e.Attribute("Label").Value.Contains("Exporter"));
            var importingPartNode = nodes.Single(e => e.Attribute("Label").Value.Contains("Importer"));
            var link = links.Single();
            Assert.Equal(exportingPartNode.Attribute("Id").Value, link.Attribute("Source").Value);
            Assert.Equal(importingPartNode.Attribute("Id").Value, link.Attribute("Target").Value);
        }

        [Export(typeof(IEquatable<Exporter>))]
        public class Exporter : IEquatable<Exporter>
        {
            public bool Equals(Exporter? other)
            {
                throw new NotImplementedException();
            }
        }

        [Export]
        public class Importer
        {
            [Import]
            public IEquatable<Exporter> ImportingProperty { get; set; } = null!;
        }

        #endregion

        #region Scopes

        [Fact]
        public async Task CreateDgmlFromConfigurationWithScopes()
        {
            XDocument dgml = await this.ProduceDgmlHelperAsync(
                typeof(Root),
                typeof(PartInScope1));

            var nodes = dgml.Root.Element(XName.Get("Nodes", Namespace)).Elements(XName.Get("Node", Namespace));
            var links = dgml.Root.Element(XName.Get("Links", Namespace)).Elements(XName.Get("Link", Namespace));

            var partInScope1Node = nodes.Single(e => e.Attribute("Id").Value.Contains("PartInScope1"));
            var scopeContainerNode = nodes.Single(e => e.Attribute("Id").Value == "Scope1");
            Assert.True(scopeContainerNode.Attribute("Label").Value.Contains("Scope1"));

            var scopeLink = links.Single(l => l.Attribute("Source").Value == scopeContainerNode.Attribute("Id").Value && l.Attribute("Target").Value == partInScope1Node.Attribute("Id").Value);
        }

        [Export]
        public class Root
        {
            [Import, SharingBoundary("Scope1")]
            public ExportFactory<PartInScope1> ScopeFactory { get; set; } = null!;
        }

        [Export, Shared("Scope1")]
        public class PartInScope1 { }

        #endregion

        private async Task<XDocument> ProduceDgmlHelperAsync(params Type[] parts)
        {
            var catalog = TestUtilities.EmptyCatalog.AddParts(await TestUtilities.V2Discovery.CreatePartsAsync(parts));
            var configuration = CompositionConfiguration.Create(catalog);
            XDocument dgml = configuration.CreateDgml();
            Assert.NotNull(dgml);

            string dgmlPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".dgml");
            File.WriteAllText(dgmlPath, dgml.ToString());
            this.output.WriteLine("DGML written to: \"{0}\"", dgmlPath);
            this.output.WriteLine(dgml.ToString());
            return dgml;
        }
    }
}
