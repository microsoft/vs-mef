namespace Microsoft.VisualStudio.Composition.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Xunit;
    using Xunit.Abstractions;
    using Xunit.Sdk;

    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    [XunitTestCaseDiscoverer("Microsoft.VisualStudio.Composition.Tests.MefFactDiscoverer", "Microsoft.VisualStudio.Composition.Tests")]
    public class MefFactAttribute : FactAttribute
    {
        public MefFactAttribute(CompositionEngines compositionVersions)
        {
            this.CompositionVersions = compositionVersions;
        }

        public MefFactAttribute(CompositionEngines compositionVersions, params Type[] parts)
            : this(compositionVersions)
        {
            Requires.NotNull(parts, "parts");

            this.Parts = parts;
            this.Assemblies = ImmutableList<string>.Empty;
        }

        public MefFactAttribute(CompositionEngines compositionVersions, string newLineSeparatedAssemblyNames, params Type[] parts)
            : this(compositionVersions, parts)
        {
            Requires.NotNullOrEmpty(newLineSeparatedAssemblyNames, "newLineSeparatedAssemblyNames");
            this.Assemblies = newLineSeparatedAssemblyNames
                .Split(new char[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(l => l.Trim())
                .Where(l => !string.IsNullOrEmpty(l))
                .ToImmutableList();
        }

        public MefFactAttribute(CompositionEngines compositionVersions, params string[] assemblies)
            : this(compositionVersions)
        {
            Requires.NotNull(assemblies, "assemblies");

            this.Assemblies = assemblies.ToImmutableList();
        }

        public bool InvalidConfiguration { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether to suppress claiming the test is skipped before it includes V3 runs.
        /// </summary>
        public bool NoCompatGoal { get; set; }

        internal CompositionEngines CompositionVersions { get; }

        internal Type[] Parts { get; }

        internal IReadOnlyList<string> Assemblies { get; }

        internal static MefFactAttribute Instantiate(IAttributeInfo attribute)
        {
            return (MefFactAttribute)((ReflectionAttributeInfo)attribute).Attribute;
        }
    }
}
