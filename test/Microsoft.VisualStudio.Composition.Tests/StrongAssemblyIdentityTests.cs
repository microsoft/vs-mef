// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.Composition.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Reflection.Metadata;
    using System.Reflection.PortableExecutable;
    using System.Text;
    using System.Threading.Tasks;
    using Xunit;

    public class StrongAssemblyIdentityTests
    {
        private Assembly assembly;
        private AssemblyName assemblyName;
        private string assemblyPath;
        private Guid mvid;

        public StrongAssemblyIdentityTests()
        {
            this.assembly = typeof(StrongAssemblyIdentityTests).GetTypeInfo().Assembly;
            this.assemblyName = this.assembly.GetName();
            this.assemblyPath = new Uri(this.assemblyName.CodeBase!).LocalPath;
            this.mvid = Guid.NewGuid();
        }

        [Fact]
        public void CreateFrom_string_AssemblyName()
        {
            var id = StrongAssemblyIdentity.CreateFrom(this.assemblyPath, this.assemblyName);
            Assert.NotNull(id);
            Assert.Same(this.assemblyName, id.Name);
            Assert.Equal(GetMvid(this.assemblyPath), id.Mvid);
        }

        [Fact]
        public void CreateFrom_string_NullAssemblyName()
        {
            var id = StrongAssemblyIdentity.CreateFrom(this.assemblyPath, null);
            Assert.NotNull(id);
            Assert.Equal(this.assemblyName.FullName, id.Name.FullName);
            Assert.Equal(GetMvid(this.assemblyPath), id.Mvid);
        }

        [Fact]
        public void CreateFrom_Assembly_AssemblyName()
        {
            var id = StrongAssemblyIdentity.CreateFrom(this.assembly, this.assemblyName);
            Assert.NotNull(id);
            Assert.Same(this.assemblyName, id.Name);
            Assert.Equal(GetMvid(this.assemblyPath), id.Mvid);
        }

        [Fact]
        public void CreateFrom_Assembly_NullAssemblyName()
        {
            var id = StrongAssemblyIdentity.CreateFrom(this.assembly, null);
            Assert.NotNull(id);
            Assert.Equal(this.assemblyName.FullName, id.Name.FullName);
            Assert.Equal(GetMvid(this.assemblyPath), id.Mvid);
        }

        [Fact]
        public void Ctor()
        {
            var id = new StrongAssemblyIdentity(this.assemblyName, this.mvid);
            Assert.Same(this.assemblyName, id.Name);
            Assert.Equal(this.mvid, id.Mvid);
        }

        [Fact]
        public void Equal()
        {
            var id1a = new StrongAssemblyIdentity(this.assemblyName, this.mvid);
            var id1b = new StrongAssemblyIdentity(this.assemblyName, this.mvid);
            Assert.Equal(id1a, id1b);

            var id2 = new StrongAssemblyIdentity(this.assemblyName, Guid.NewGuid());
            Assert.NotEqual(id1a, id2);

            var id3 = new StrongAssemblyIdentity(typeof(string).GetTypeInfo().Assembly.GetName(), this.mvid);
            Assert.NotEqual(id1a, id3);
        }

        /// <summary>
        /// Gets the MVID for an assembly with the specified path.
        /// </summary>
        /// <param name="assemblyFile">The assembly to get the MVID from.</param>
        /// <returns>The MVID.</returns>
        private static Guid GetMvid(string assemblyFile)
        {
            using (var stream = File.OpenRead(assemblyFile))
            {
                using (var reader = new PEReader(stream))
                {
                    var metadataReader = reader.GetMetadataReader();
                    var mvidHandle = metadataReader.GetModuleDefinition().Mvid;
                    return metadataReader.GetGuid(mvidHandle);
                }
            }
        }
    }
}
