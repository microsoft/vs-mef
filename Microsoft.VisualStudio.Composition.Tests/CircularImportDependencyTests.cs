namespace Microsoft.VisualStudio.Composition.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Composition;
    using System.Composition.Hosting;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Xunit;
    using MefV1 = System.ComponentModel.Composition;

    public class CircularImportDependencyTests
    {
        static IContainer _container = null;

        [Fact(Skip = "Still need to handle circular MEF dependencies.")]
        [MefFact(CompositionEngines.V1Compat)]
        public void CircularImportDependency(IContainer container)
        {
            _container = container;
            IPackageRestoreManager packageRestoreManager = container.GetExportedValue<IPackageRestoreManager>();
            Assert.NotNull(packageRestoreManager);
        }

        public interface ISolutionManager
        {
            int Method1();
        }
        public interface IVsPackageInstaller
        {
            int Method2();
        }
        public interface IPackageRestoreManager
        {
            int Method3();
        }

        public interface IVsPackageInstallerServices
        {
            int Method4();
        }

        public interface IVsPackageManagerFactory
        {
            int Method5();
        }

        [MefV1.Export(typeof(IPackageRestoreManager))]
        public class PackageRestoreManager : IPackageRestoreManager
        {
            ISolutionManager _solutionManager = null;

            [MefV1.ImportingConstructor]
            public PackageRestoreManager(ISolutionManager solutionManager)
            {
                _solutionManager = solutionManager;
            }

            public int Method3() { return 3; }
        }

        [MefV1.PartCreationPolicy(MefV1.CreationPolicy.Shared)]
        [MefV1.Export(typeof(ISolutionManager))]
        public class SolutionManager : ISolutionManager
        {
            public SolutionManager()
            {
                DoSomeThing();
            }

            private void DoSomeThing()
            {
                // This matches what Microsoft.VisualStudio.Web.Application GetNugetProjectTypeContext is doing when it uses
                // IComponentModel.GetService<IVsPackageInstallerServices>() on the callstack above the SolutionManager constructor.
                IVsPackageInstallerServices packageInstallerServices = _container.GetExportedValue<IVsPackageInstallerServices>();
            }

            public int Method1() { return 1; }
        }


        [MefV1.Export(typeof(IVsPackageInstallerServices))]
        public class VsPackageInstallerServices : IVsPackageInstallerServices
        {

            IVsPackageManagerFactory _packageManagerFactory = null;
            [MefV1.ImportingConstructor]
            public VsPackageInstallerServices(IVsPackageManagerFactory packageManagerFactory)
            {
                _packageManagerFactory = packageManagerFactory;
            }

            public int Method4() { return 4; }
        }

        [MefV1.PartCreationPolicy(MefV1.CreationPolicy.Shared)]
        [MefV1.Export(typeof(IVsPackageManagerFactory))]
        public class VsPackageManagerFactory : IVsPackageManagerFactory
        {
            private readonly ISolutionManager _solutionManager;

            [MefV1.ImportingConstructor]
            public VsPackageManagerFactory(ISolutionManager solutionManager)
            {
                _solutionManager = solutionManager;
                int i = _solutionManager.Method1();
            }

            public int Method5() { return 5; }
        }
    }
}
