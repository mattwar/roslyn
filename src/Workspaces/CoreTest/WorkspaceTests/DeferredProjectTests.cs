// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis.Host;
using Microsoft.CodeAnalysis.Test.Utilities;
using Microsoft.CodeAnalysis.Text;
using Roslyn.Test.Utilities;
using Roslyn.Utilities;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Microsoft.CodeAnalysis.Shared.Utilities;
using System;
using CS = Microsoft.CodeAnalysis.CSharp;

namespace Microsoft.CodeAnalysis.UnitTests
{
    public partial class DeferredProjectTests : WorkspaceTestBase
    {
        [Fact, Trait(Traits.Feature, Traits.Features.Workspace)]
        public void TestGetDeferredProject()
        {
            var id = ProjectId.CreateNewId();

            var loader = new FakeDeferredProjectLoader(
                ProjectInfo.Create(
                    id,
                    version: VersionStamp.Default,
                    name: "TestProject",
                    assemblyName: "TestProject.dll",
                    language: LanguageNames.CSharp));

            var info = SolutionInfo.Create(
                    SolutionId.CreateNewId(),
                    VersionStamp.Create(),
                    deferredProjects: new[] { DeferredProjectInfo.Create(id) },
                    deferredProjectLoader: loader);

            using (var ws = new AdhocWorkspace())
            {
                var solution = ws.AddSolution(info);
                Assert.Equal(0, loader.ProjectsLoaded);
                var project = solution.GetProject(id);
                Assert.NotNull(project);
                Assert.Equal(1, loader.ProjectsLoaded);
            }
        }

        [Fact, Trait(Traits.Feature, Traits.Features.Workspace)]
        public void TestGetDeferredProjectAsync()
        {
            var id = ProjectId.CreateNewId();

            var loader = new FakeDeferredProjectLoader(
                ProjectInfo.Create(
                    id,
                    version: VersionStamp.Default,
                    name: "TestProject",
                    assemblyName: "TestProject.dll",
                    language: LanguageNames.CSharp));

            var info = SolutionInfo.Create(
                    SolutionId.CreateNewId(),
                    VersionStamp.Create(),
                    deferredProjects: new[] { DeferredProjectInfo.Create(id) },
                    deferredProjectLoader: loader);

            using (var ws = new AdhocWorkspace())
            {
                var solution = ws.AddSolution(info);
                Assert.Equal(0, loader.ProjectsLoaded);
                var project = solution.GetProjectAsync(id).Result;
                Assert.NotNull(project);
                Assert.Equal(1, loader.ProjectsLoaded);
            }
        }

        [Fact, Trait(Traits.Feature, Traits.Features.Workspace)]
        public void TestGetNonDeferredProjectAsync()
        {
            var id = ProjectId.CreateNewId();

             var projectInfo = ProjectInfo.Create(
                    id,
                    version: VersionStamp.Default,
                    name: "TestProject",
                    assemblyName: "TestProject.dll",
                    language: LanguageNames.CSharp);

            var info = SolutionInfo.Create(
                    SolutionId.CreateNewId(),
                    VersionStamp.Create(),
                    projects: new [] { projectInfo });

            using (var ws = new AdhocWorkspace())
            {
                var solution = ws.AddSolution(info);
                var project = solution.GetProjectAsync(id).Result;
                Assert.NotNull(project);
            }
        }

        [Fact, Trait(Traits.Feature, Traits.Features.Workspace)]
        public void TestTryGetDeferredProjectUnloaded()
        {
            var id = ProjectId.CreateNewId();

            var loader = new FakeDeferredProjectLoader(
                ProjectInfo.Create(
                    id,
                    version: VersionStamp.Default,
                    name: "TestProject",
                    assemblyName: "TestProject.dll",
                    language: LanguageNames.CSharp));

            var info = SolutionInfo.Create(
                    SolutionId.CreateNewId(),
                    VersionStamp.Create(),
                    deferredProjects: new[] { DeferredProjectInfo.Create(id) },
                    deferredProjectLoader: loader);

            using (var ws = new AdhocWorkspace())
            {
                var solution = ws.AddSolution(info);
                Assert.Equal(0, loader.ProjectsLoaded);
                Project project;
                Assert.Equal(false, solution.TryGetProject(id, out project));
                Assert.Equal(0, loader.ProjectsLoaded);
            }
        }

        [Fact, Trait(Traits.Feature, Traits.Features.Workspace)]
        public void TestGetDeferredReferencedProject()
        {
            var id1 = ProjectId.CreateNewId();
            var id2 = ProjectId.CreateNewId();

            var loader = new FakeDeferredProjectLoader(
                ProjectInfo.Create(
                    id1,
                    version: VersionStamp.Default,
                    name: "TestProject1",
                    assemblyName: "TestProject1.dll",
                    language: LanguageNames.CSharp,
                    projectReferences: new[] { new ProjectReference(id2) }),
                ProjectInfo.Create(
                    id2,
                    version: VersionStamp.Default,
                    name: "TestProject2",
                    assemblyName: "TestProject2.dll",
                    language: LanguageNames.CSharp));

            var info = SolutionInfo.Create(
                    SolutionId.CreateNewId(),
                    VersionStamp.Create(),
                    deferredProjects: new[] {
                        DeferredProjectInfo.Create(id1, referencedProjectIds: new [] { id2 }),
                        DeferredProjectInfo.Create(id2) },
                    deferredProjectLoader: loader);

            using (var ws = new AdhocWorkspace())
            {
                var solution = ws.AddSolution(info);
                Assert.Equal(0, loader.ProjectsLoaded);
                var project1 = solution.GetProject(id1);
                Assert.Equal(1, loader.ProjectsLoaded);
                var project2 = solution.GetProject(project1.ProjectReferences.First().ProjectId);
                Assert.Equal(2, loader.ProjectsLoaded);
            }
        }

        private class FakeDeferredProjectLoader : DeferredProjectLoader
        {
            private readonly ImmutableArray<ProjectInfo> _projectInfos;

            private int _projectsLoaded;

            public int ProjectsLoaded
            {
                get { return _projectsLoaded; }
            }

            public FakeDeferredProjectLoader(params ProjectInfo[] projectInfos)
            {
                _projectInfos = projectInfos.ToImmutableArrayOrEmpty();
            }

            public override Task<ProjectInfo> GetProjectInfoAsync(ProjectId projectId, CancellationToken cancellationToken)
            {
                var info = _projectInfos.FirstOrDefault(pi => pi.Id == projectId);

                if (info == null)
                {
                    return Task.FromException<ProjectInfo>(new ArgumentException("The project is not deferred.", nameof(projectId)));
                }
                else
                {
                    _projectsLoaded++;
                    return Task.FromResult(info);
                }
            }
        }
    }
}

