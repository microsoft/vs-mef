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

    public class DgmlProduction
    {
        internal const string Namespace = "http://schemas.microsoft.com/vs/2009/dgml";
        
        [Fact]
        public void CreateDgmlFromConfiguration()
        {
            var configuration = CompositionConfiguration.Create(
                new AttributedPartDiscovery(),
                typeof(Exporter),
                typeof(Importer));
            XDocument dgml = configuration.CreateDgml();
            Assert.NotNull(dgml);

            string dgmlPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".dgml");
            File.WriteAllText(dgmlPath, dgml.ToString());
            Console.WriteLine("DGML written to: \"{0}\"", dgmlPath);
            Console.WriteLine(dgml);

            var nodes = dgml.Root.Element(XName.Get("Nodes", Namespace)).Elements(XName.Get("Node", Namespace));
            var links = dgml.Root.Element(XName.Get("Links", Namespace)).Elements(XName.Get("Link", Namespace));
            var exportingPartNode = nodes.Single(e => e.Attribute("Label").Value.Contains("Exporter"));
            var importingPartNode = nodes.Single(e => e.Attribute("Label").Value.Contains("Importer"));
            var link = links.Single();
            Assert.Equal(exportingPartNode.Attribute("Id").Value, link.Attribute("Source").Value);
            Assert.Equal(importingPartNode.Attribute("Id").Value, link.Attribute("Target").Value);
        }

        [Export]
        public class Exporter { }

        [Export]
        public class Importer
        {
            [Import]
            public Exporter ImportingProperty { get; set; }
        }
    }
}
