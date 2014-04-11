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

        /// <summary>
        /// This test documents the V2 behavior of disposing all values in the immediate container
        /// of a scoping part when its export is disposed of, but not doing so recursively.
        /// </summary>
        [MefFact(CompositionEngines.V2, NoCompatGoal = true)]
        public void DisposeExportDisposesAllSubContainersV2(IContainer container)
        {
            var projectService = container.GetExportedValue<ProjectService>();
            var projectExport = projectService.ProjectFactory.CreateExport();
            var configuredProjectExport = projectExport.Value.ConfiguredProjectFactory.CreateExport();

            Assert.Equal(0, projectExport.Value.DisposalCount);
            Assert.Equal(0, projectExport.Value.ActiveConfiguration.DisposalCount);
            Assert.Equal(0, configuredProjectExport.Value.DisposalCount);
            Assert.Equal(0, configuredProjectExport.Value.SubscriptionService.DisposalCount);

            projectExport.Dispose();

            Assert.Equal(1, projectExport.Value.DisposalCount);
            Assert.Equal(1, projectExport.Value.ActiveConfiguration.DisposalCount);
            Assert.Equal(0, configuredProjectExport.Value.DisposalCount);
            Assert.Equal(0, configuredProjectExport.Value.SubscriptionService.DisposalCount);
        }

        /// <summary>
        /// This test documents the V3 behavior of disposing all values in the immediate container
        /// of a scoping part when its export is disposed of, and also recursively disposing of descendent containers.
        /// </summary>
        [MefFact(CompositionEngines.Unspecified/*V3EmulatingV2*/)]
        public void DisposeExportDisposesAllSubContainersV3AsV2(IContainer container)
        {
            var projectService = container.GetExportedValue<ProjectService>();
            var projectExport = projectService.ProjectFactory.CreateExport();
            var configuredProjectExport = projectExport.Value.ConfiguredProjectFactory.CreateExport();

            Assert.Equal(0, projectExport.Value.DisposalCount);
            Assert.Equal(0, projectExport.Value.ActiveConfiguration.DisposalCount);
            Assert.Equal(0, configuredProjectExport.Value.DisposalCount);
            Assert.Equal(0, configuredProjectExport.Value.SubscriptionService.DisposalCount);

            projectExport.Dispose();

            Assert.Equal(1, projectExport.Value.DisposalCount);
            Assert.Equal(1, projectExport.Value.ActiveConfiguration.DisposalCount);
            Assert.Equal(1, configuredProjectExport.Value.DisposalCount);
            Assert.Equal(1, configuredProjectExport.Value.SubscriptionService.DisposalCount);
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
        public class Project : IDisposable
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

            internal int DisposalCount { get; private set; }

            public void Dispose()
            {
                this.DisposalCount++;
            }

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
        public class ProjectActiveConfiguration : IDisposable
        {
            [Import]
            public Project Project { get; set; }

            internal int DisposalCount { get; private set; }

            public void Dispose()
            {
                this.DisposalCount++;
            }
        }

        [Export, Shared("ConfiguredProject")]
        public class ConfiguredProject : IDisposable
        {
            [Import]
            public Project Project { get; set; }

            [Import]
            public ProjectSubscriptionService SubscriptionService { get; set; }

            internal int DisposalCount { get; private set; }

            public void Dispose()
            {
                this.DisposalCount++;
            }
        }

        [Export, Shared("ConfiguredProject")]
        public class ProjectSubscriptionService : IDisposable
        {
            internal int DisposalCount { get; private set; }

            public void Dispose()
            {
                this.DisposalCount++;
            }
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
