// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using EnvDTE;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Editor.Shared.Utilities;
using Microsoft.CodeAnalysis.Host;
using Microsoft.CodeAnalysis.Host.Mef;
using Microsoft.CodeAnalysis.Notification;
using Microsoft.CodeAnalysis.Options;
using Microsoft.CodeAnalysis.Packaging;
using Microsoft.CodeAnalysis.Shared.Utilities;
using Microsoft.CodeAnalysis.SolutionCrawler;
using Microsoft.CodeAnalysis.SymbolSearch;
using Microsoft.CodeAnalysis.Text;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.Feedback.Interop;
using Microsoft.VisualStudio.LanguageServices.Implementation.ProjectSystem.Extensions;
using Microsoft.VisualStudio.LanguageServices.Packaging;
using Microsoft.VisualStudio.LanguageServices.SymbolSearch;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.TextManager.Interop;
using Roslyn.Utilities;
using Roslyn.VisualStudio.ProjectSystem;

namespace Microsoft.VisualStudio.LanguageServices.Implementation.ProjectSystem
{
    /// <summary>
    /// The Workspace for running inside Visual Studio.
    /// </summary>
    internal abstract partial class VisualStudioWorkspaceImpl
    {
        public override bool IsPartialSolution
        {
            get { return true; }
        }

        public override IEnumerable<ProjectId> GetAllProjectIds()
        {
            return _projectTracker.GetProjectIds();
        }

        public override IEnumerable<ProjectId> GetReferencingProjectIds(ProjectId projectId)
        {
            return _projectTracker.GetReferencingProjectIds(projectId);
        }

        public override IEnumerable<ProjectId> GetReferencedProjectIds(ProjectId projectId)
        {
            return _projectTracker.GetReferencingProjectIds(projectId);
        }

        public override Task<CodeAnalysis.Solution> EnsureProjectsAvailableAsync(IEnumerable<ProjectId> projectIds, CancellationToken cancellationToken = default(CancellationToken))
        {
            _projectTracker.EnsureProjectsAvailable(projectIds);
            return System.Threading.Tasks.Task.FromResult(this.CurrentSolution);
        }

        public override void RemoveUnnecessaryProjects()
        {
            _projectTracker.RemoveUnnecessaryProjects();
        }
    }
}
