namespace Microsoft.VisualStudio.Composition.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using System.Composition;
    using Xunit;
    using System.Composition.Hosting;
    using CompositionFailedException = Microsoft.VisualStudio.Composition.CompositionFailedException;

    public class ProjectSystemMockTests
    {
        [MefFact(CompositionEngines.V2Compat)]
        public void GetProjectService(IContainer container)
        {
            var projectService = container.GetExportedValue<ProjectService>();
            Assert.NotNull(projectService);
        }

        [MefFact(CompositionEngines.V2Compat)]
        public void ProjectFactory(IContainer container)
        {
            var projectService = container.GetExportedValue<ProjectService>();
            var project = projectService.CreateProject();
            Assert.NotNull(project);
            Assert.Same(project.Value.ProjectService, projectService);
            Assert.NotNull(project.Value.ActiveConfiguration);
            Assert.Same(project.Value.ActiveConfiguration.Project, project.Value);
        }

        [MefFact(CompositionEngines.V2Compat)]
        public void ProjectScopeExportsNotAvailableAtRoot(IContainer container)
        {
            Assert.Throws<CompositionFailedException>(() => container.GetExportedValue<ProjectActiveConfiguration>());
        }

        [MefFact(CompositionEngines.V2Compat)]
        public void ScopedCompositionTree(IContainer container)
        {
            var projectService = container.GetExportedValue<ProjectService>();
            var project1 = projectService.CreateProject();
            var project2 = projectService.CreateProject();
            Assert.NotSame(project1, project2);
            Assert.NotSame(project1.Value.ActiveConfiguration, project2.Value.ActiveConfiguration);

            var projectConfiguration1a = project1.Value.CreateConfiguration();
            var projectConfiguration1b = project1.Value.CreateConfiguration();
            Assert.NotNull(projectConfiguration1a);
            Assert.NotNull(projectConfiguration1b);
            Assert.NotSame(projectConfiguration1a.Value, projectConfiguration1b.Value);

            Assert.Same(projectConfiguration1a.Value.Project, project1.Value);
            Assert.NotSame(projectConfiguration1a.Value.SubscriptionService, projectConfiguration1b.Value.SubscriptionService);
        }

        [MefFact(CompositionEngines.V2Compat)]
        public void CapabilityFiltering(IContainer container)
        {
            var projectService = container.GetExportedValue<ProjectService>();

            var project1 = projectService.CreateProject("Capability1");
            Assert.Equal(1, project1.Value.GetApplicableExtensions().Length);
            Assert.IsType<ProjectPartA>(project1.Value.GetApplicableExtensions().Single().Value);

            var project2 = projectService.CreateProject("Capability2");
            Assert.Equal(1, project2.Value.GetApplicableExtensions().Length);
            Assert.IsType<ProjectPartB>(project2.Value.GetApplicableExtensions().Single().Value);
        }

        #region MEF parts

        [Export, Shared]
        public class ProjectService
        {
            [Import, SharingBoundary("Project")]
            public ExportFactory<Project> ProjectFactory { get; set; }

            public Export<Project> CreateProject(params string[] capabilities)
            {
                var project = this.ProjectFactory.CreateExport();
                project.Value.Capabilities = capabilities;
                return project;
            }
        }

        [Export, Shared]
        public class ProjectLock
        {
        }

        [Export, Shared("Project")]
        public class Project
        {
            [Import]
            public ProjectService ProjectService { get; set; }

            [Import, SharingBoundary("ConfiguredProject")]
            public ExportFactory<ConfiguredProject> ConfiguredProjectFactory { get; set; }

            [Import]
            public ProjectActiveConfiguration ActiveConfiguration { get; set; }

            [ImportMany("ProjectExtension")]
            public Lazy<object, IDictionary<string, object>>[] Extensions { get; set; }

            public string[] Capabilities { get; set; }

            public Lazy<object, IDictionary<string, object>>[] GetApplicableExtensions()
            {
                return this.Extensions.Where(export => this.Capabilities.Contains(export.Metadata["ProjectCapabilityRequires"])).ToArray();
            }

            public Export<ConfiguredProject> CreateConfiguration()
            {
                return this.ConfiguredProjectFactory.CreateExport();
            }
        }
        
        [Export, Shared("Project")]
        public class ProjectActiveConfiguration
        {
            [Import]
            public Project Project { get; set; }
        }

        [Export, Shared("ConfiguredProject")]
        public class ConfiguredProject
        {
            [Import]
            public Project Project { get; set; }

            [Import]
            public ProjectSubscriptionService SubscriptionService { get; set; }
        }

        [Export, Shared("ConfiguredProject")]
        public class ProjectSubscriptionService
        {
        }

        [Export("ProjectExtension", typeof(object))]
        [ExportMetadata("ProjectCapabilityRequires", "Capability1")]
        public class ProjectPartA { }

        [Export("ProjectExtension", typeof(object))]
        [ExportMetadata("ProjectCapabilityRequires", "Capability2")]
        public class ProjectPartB
        {
        }

        #endregion
    }
}
