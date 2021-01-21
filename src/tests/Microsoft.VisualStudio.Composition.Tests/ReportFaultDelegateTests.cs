// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.Composition.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Composition;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Text;
    using System.Threading.Tasks;
    using Xunit;
    using Xunit.Abstractions;
    using Xunit.Extensions;
    using Xunit.Sdk;

    public class ReportFaultDelegateTests
    {
        public static IEnumerable<object[]> ReportFaultTestCases
        {
            get
            {
                return new[]
                {
                    new object[] { ExportWithLazyImportOfBadConstructorExport.GetTestCase() },
                    new object[] { ExportWithLazyImportOfBadExportingMember.GetTestCase() },
                    new object[] { ExportWithLazyImportOfTwoBadExports.GetTestCase() },
                };
            }
        }

        public class ReportFaultTestCase
        {
            public IFaultReportingExportProviderFactory? ExportProviderFactory { get; set; }

            public Type? ExpectedBaseExceptionType { get; set; }

            public Exception? ExpectedInnerException { get; set; }

            public int ExpectedCallbackCount { get; set; }

            public Func<ExportProvider, BaseImportingClass>? GetBaseImportingClassFromExportProvider { get; set; }

            public IEnumerable<Action<BaseImportingClass>>? ActionsToRunAgainstBaseImportingClass { get; set; }
        }

        [Fact]
        public void CreateExportProvider_ThrowsArgumentNullException()
        {
            var discovery = TestUtilities.V2Discovery;
            List<ComposablePartDefinition> parts = new List<ComposablePartDefinition>();
            parts.Add(discovery.CreatePart(typeof(ExportWithFailingConstructor))!);

            var exportProviderFactory = CreateExportProviderFactory(parts);
            Assert.Throws<ArgumentNullException>(() => exportProviderFactory.CreateExportProvider(null!));
        }

        [Fact]
        public void CreateExportProviderDefaultArgs_StillThrowsException()
        {
            var discovery = TestUtilities.V2Discovery;
            List<ComposablePartDefinition> parts = new List<ComposablePartDefinition>();
            parts.Add(discovery.CreatePart(typeof(ExportWithFailingConstructor))!);
            parts.Add(discovery.CreatePart(typeof(ExportWithLazyImportOfBadConstructorExport))!);

            var exportProviderFactory = CreateExportProviderFactory(parts);
            Assert.NotNull(exportProviderFactory);

            var exportProvider = exportProviderFactory.CreateExportProvider();
            Assert.NotNull(exportProvider);

            var exportWithLazyImport = exportProvider.GetExportedValue<ExportWithLazyImportOfBadConstructorExport>();
            Assert.NotNull(exportWithLazyImport);
            Assert.ThrowsAny<Exception>(() => exportWithLazyImport.FailingConstructor.Value);
        }

        [Theory, MemberData(nameof(ReportFaultTestCases))]
        public void ReportFault_IsCalledCorrectly(ReportFaultTestCase testCase)
        {
            Assert.NotNull(testCase.ExportProviderFactory);

            int timesCallbackIsCalled = 0;
            Exception? exceptionPassedToCallback = null;
            var exportProvider = testCase.ExportProviderFactory!.CreateExportProvider(
                (ex, import, export) =>
                {
                    Assert.NotNull(ex);
                    Assert.NotNull(import);
                    Assert.NotNull(export);
                    Assert.NotNull(ex.InnerException);

                    Assert.IsType(testCase.ExpectedBaseExceptionType, ex);
                    Assert.Equal(testCase.ExpectedInnerException, ex.InnerException);

                    timesCallbackIsCalled++;
                    exceptionPassedToCallback = ex;
                });
            Assert.NotNull(exportProvider);

            var exportWithLazyImport = testCase.GetBaseImportingClassFromExportProvider!(exportProvider);
            Assert.NotNull(exportWithLazyImport);

            foreach (var action in testCase.ActionsToRunAgainstBaseImportingClass!)
            {
                try
                {
                    action(exportWithLazyImport);
                }
                catch (Exception e) when (!(e is XunitException))
                {
                    Assert.IsType(testCase.ExpectedBaseExceptionType, e);
                    Assert.Equal(exceptionPassedToCallback, e);
                }
            }

            Assert.Equal(testCase.ExpectedCallbackCount, timesCallbackIsCalled);
        }

        private static IFaultReportingExportProviderFactory CreateExportProviderFactory(IEnumerable<ComposablePartDefinition> parts)
        {
            var catalog = TestUtilities.EmptyCatalog.AddParts(parts);
            var configuration = CompositionConfiguration.Create(catalog);
            var runtimeComposition = RuntimeComposition.CreateRuntimeComposition(configuration);

            return (IFaultReportingExportProviderFactory)runtimeComposition.CreateExportProviderFactory();
        }

        #region Testcases and Export/Imports
        [Export]
        public class ExportWithLazyImportOfBadConstructorExport : BaseImportingClass
        {
            [Import]
            public Lazy<ExportWithFailingConstructor> FailingConstructor { get; set; } = null!;

            public static ReportFaultTestCase GetTestCase()
            {
                List<ComposablePartDefinition> parts = new List<ComposablePartDefinition>();
                var discovery = TestUtilities.V2Discovery;
                parts.Add(discovery.CreatePart(typeof(ExportWithFailingConstructor))!);
                parts.Add(discovery.CreatePart(typeof(ExportWithLazyImportOfBadConstructorExport))!);

                var exportProviderFactory = CreateExportProviderFactory(parts);

                return new ReportFaultTestCase
                {
                    ExportProviderFactory = exportProviderFactory,
                    ExpectedBaseExceptionType = typeof(CompositionFailedException),
                    ExpectedInnerException = ExportWithFailingConstructor.ThrownException,
                    ExpectedCallbackCount = 1,
                    GetBaseImportingClassFromExportProvider = new Func<ExportProvider, BaseImportingClass>((exportProvider) =>
                    {
                        return exportProvider.GetExportedValue<ExportWithLazyImportOfBadConstructorExport>();
                    }),
                    ActionsToRunAgainstBaseImportingClass = new Action<BaseImportingClass>[]
                    {
                        new Action<BaseImportingClass>((failingImport) =>
                        {
                            ExportWithLazyImportOfBadConstructorExport import = (ExportWithLazyImportOfBadConstructorExport)failingImport;
                            var unused = import.FailingConstructor.Value;
                        }),
                    },
                };
            }
        }

        [Export]
        public class ExportWithLazyImportOfBadExportingMember : BaseImportingClass
        {
            [Import]
            public Lazy<DummyClass> FailingExport { get; set; } = null!;

            public static ReportFaultTestCase GetTestCase()
            {
                List<ComposablePartDefinition> parts = new List<ComposablePartDefinition>();
                var discovery = TestUtilities.V2Discovery;
                parts.Add(discovery.CreatePart(typeof(ClassWithExportingMemberThatFails))!);
                parts.Add(discovery.CreatePart(typeof(ExportWithLazyImportOfBadExportingMember))!);

                var exportProviderFactory = CreateExportProviderFactory(parts);
                return new ReportFaultTestCase
                {
                    ExportProviderFactory = exportProviderFactory,
                    ExpectedBaseExceptionType = typeof(TargetInvocationException),
                    ExpectedInnerException = ClassWithExportingMemberThatFails.ThrownException,
                    ExpectedCallbackCount = 1,
                    GetBaseImportingClassFromExportProvider = new Func<ExportProvider, BaseImportingClass>((exportProvider) =>
                    {
                        return exportProvider.GetExportedValue<ExportWithLazyImportOfBadExportingMember>();
                    }),
                    ActionsToRunAgainstBaseImportingClass = new Action<BaseImportingClass>[]
                    {
                        new Action<BaseImportingClass>((failingImport) =>
                        {
                            ExportWithLazyImportOfBadExportingMember import = (ExportWithLazyImportOfBadExportingMember)failingImport;
                            var unused = import.FailingExport.Value;
                        }),
                    },
                };
            }
        }

        [Export]
        public class ExportWithLazyImportOfTwoBadExports : BaseImportingClass
        {
            [ImportMany]
            public IEnumerable<Lazy<FailingExport>> FailingExports { get; set; } = null!;

            public static ReportFaultTestCase GetTestCase()
            {
                List<ComposablePartDefinition> parts = new List<ComposablePartDefinition>();
                var discovery = TestUtilities.V2Discovery;
                parts.Add(discovery.CreatePart(typeof(FailingExport1))!);
                parts.Add(discovery.CreatePart(typeof(FailingExport2))!);
                parts.Add(discovery.CreatePart(typeof(ExportWithLazyImportOfTwoBadExports))!);

                var exportProviderFactory = CreateExportProviderFactory(parts);

                // Even though we're calling the failing class multiple times, the ExpectedStackFrame,
                // ExpectedBaseException, and ExpectedInnerException should be the same for this testcase
                return new ReportFaultTestCase
                {
                    ExportProviderFactory = exportProviderFactory,
                    ExpectedBaseExceptionType = typeof(CompositionFailedException),
                    ExpectedInnerException = FailingExport.ThrownException,
                    ExpectedCallbackCount = 2,
                    GetBaseImportingClassFromExportProvider = new Func<ExportProvider, BaseImportingClass>((exportProvider) =>
                    {
                        return exportProvider.GetExportedValue<ExportWithLazyImportOfTwoBadExports>();
                    }),
                    ActionsToRunAgainstBaseImportingClass = new Action<BaseImportingClass>[]
                    {
                        new Action<BaseImportingClass>((failingImport) =>
                        {
                            ExportWithLazyImportOfTwoBadExports import = (ExportWithLazyImportOfTwoBadExports)failingImport;
                            var unused = import.FailingExports.ElementAt(0).Value;
                        }),
                        new Action<BaseImportingClass>((failingImport) =>
                        {
                            ExportWithLazyImportOfTwoBadExports import = (ExportWithLazyImportOfTwoBadExports)failingImport;
                            var unused = import.FailingExports.ElementAt(1).Value;
                        }),
                    },
                };
            }
        }

        [Export]
        public class ExportWithFailingConstructor
        {
            public static readonly Exception ThrownException = new Exception("ExportWithFailingConstructor");

            public ExportWithFailingConstructor()
            {
                throw ThrownException;
            }
        }

        public class ClassWithExportingMemberThatFails
        {
            public static readonly Exception ThrownException = new Exception("ExportingMemberWithFailingGetter");

            [Export]
            public DummyClass FailingExport
            {
                get
                {
                    throw ThrownException;
                }
            }
        }

        [Export(typeof(FailingExport))]
        public class FailingExport1 : FailingExport
        {
            public FailingExport1()
            {
                throw FailingExport.ThrownException;
            }
        }

        [Export(typeof(FailingExport))]
        public class FailingExport2 : FailingExport
        {
            public FailingExport2()
            {
                throw FailingExport.ThrownException;
            }
        }

        public class FailingExport
        {
            public static readonly Exception ThrownException = new Exception("FailingExport");
        }

        public class DummyClass { }

        public class BaseImportingClass { }
        #endregion
    }
}
