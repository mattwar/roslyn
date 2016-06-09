// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Shared.Extensions;
using Microsoft.CodeAnalysis.Text;
using Roslyn.Utilities;

namespace Microsoft.CodeAnalysis
{
    // The parts of a workspace that deal with the full set of projects available to the host when the workspace
    // is operating in partial solution mode.
    public abstract partial class Workspace
    {
        /// <summary>
        /// True if the <see cref="CurrentSolution"/> represents only a subset of the possible projects.
        /// </summary>
        public virtual bool IsPartialSolution
        {
            get { return false; }
        }

        /// <summary>
        /// Gets the <see cref="ProjectId"/> for all projects that reference the specified project.
        /// This may produce <see cref="ProjectId"/>'s that refer to projects that are not part of the <see cref="CurrentSolution"/>.
        /// </summary>
        public virtual IEnumerable<ProjectId> GetReferencingProjectIds(ProjectId projectId)
        {
            return this.CurrentSolution.GetProjectDependencyGraph().GetProjectsThatDirectlyDependOnThisProject(projectId);
        }

        public virtual IEnumerable<ProjectId> GetReferencedProjectIds(ProjectId projectId)
        {
            return this.CurrentSolution.GetProjectDependencyGraph().GetProjectsThatThisProjectDirectlyDependsOn(projectId);
        }

        /// <summary>
        /// Gets a list of all the <see cref="ProjectId"/>'s known to the <see cref="Workspace"/>.
        /// This may produce <see cref="ProjectId"/>'s that refer to projects that are not part of the <see cref="CurrentSolution"/>.
        /// </summary>
        public virtual IEnumerable<ProjectId> GetAllProjectIds()
        {
            return this.CurrentSolution.ProjectIds;
        }

        /// <summary>
        /// Ensure that the listed projects are available in the current solution.
        /// </summary>
        /// <returns>Returns the current solution with the specified projects available.</returns>
        public virtual Task<Solution> EnsureProjectsAvailableAsync(IEnumerable<ProjectId> projects, CancellationToken cancellationToken = default(CancellationToken))
        {
            return Task.FromResult(this.CurrentSolution);
        }

        /// <summary>
        /// Remove unnecessary projects from the <see cref="CurrentSolution"/>. 
        /// This typically removes projects that are not associated with open documents.
        /// </summary>
        public virtual void RemoveUnnecessaryProjects()
        {
            // do nothing
        }
    }
}