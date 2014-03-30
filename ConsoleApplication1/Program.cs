using System;
using System.Collections.Generic;
using System.Composition;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Composition;

namespace ConsoleApplication1
{
    class Program
    {
        static void Main(string[] args)
        {
            var config = CompositionConfiguration.LoadDefault();
            var container = config.CreateContainer();
            Foo foo = container.GetExportedValue<Foo>();
        }
    }

    [Export]
    public class Foo {
        [Import]
        public Bar Bar { get; set; }
    }

    [Export]
    public class Bar { }
}
