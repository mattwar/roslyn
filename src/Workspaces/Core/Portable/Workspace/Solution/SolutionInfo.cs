// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Host;
using Microsoft.CodeAnalysis.Text;
using Roslyn.Utilities;

namespace Microsoft.CodeAnalysis
{
    /// <summary>
    /// A class that represents all the arguments necessary to create a new solution instance.
    /// </summary>
    public sealed class SolutionInfo
    {
        /// <summary>
        /// The unique Id of the solution.
        /// </summary>
        public SolutionId Id { get; }

        /// <summary>
        /// The version of the solution.
        /// </summary>
        public VersionStamp Version { get; }

        /// <summary>
        /// The path to the solution file, or null if there is no solution file.
        /// </summary>
        public string FilePath { get; }

        /// <summary>
        /// A list of projects initially associated with the solution.
        /// </summary>
        public IReadOnlyList<ProjectInfo> Projects { get; }

        /// <summary>
        /// A list of deferred projects that may get loaded on demand.
        /// </summary>
        public IReadOnlyList<DeferredProjectInfo> DeferredProjects { get; }

        /// <summary>
        /// A <see cref="DeferredProjectLoader"/> that will be used to load the deferred projects.
        /// </summary>
        public DeferredProjectLoader DeferredProjectLoader { get; }

        private SolutionInfo(
            SolutionId id,
            VersionStamp version,
            string filePath,
            IEnumerable<ProjectInfo> projects,
            IEnumerable<DeferredProjectInfo> deferredProjects,
            DeferredProjectLoader deferredProjectLoader)
        {
            this.Id = id;
            this.Version = version;
            this.FilePath = filePath;
            this.Projects = projects.ToImmutableReadOnlyListOrEmpty();
            this.DeferredProjects = deferredProjects.ToImmutableReadOnlyListOrEmpty();
            this.DeferredProjectLoader = deferredProjectLoader;
        }

        /// <summary>
        /// Create a new instance of a SolutionInfo.
        /// </summary>
        public static SolutionInfo Create(
            SolutionId id,
            VersionStamp version,
            string filePath,
            IEnumerable<ProjectInfo> projects)
        {
            return new SolutionInfo(id, version, filePath, projects, deferredProjects: null, deferredProjectLoader: null);
        }

        /// <summary>
        /// Create a new instance of a SolutionInfo.
        /// </summary>
        public static SolutionInfo Create(
            SolutionId id,
            VersionStamp version,
            string filePath = null,
            IEnumerable<ProjectInfo> projects = null,
            IEnumerable<DeferredProjectInfo> deferredProjects = null,
            DeferredProjectLoader deferredProjectLoader = null)
        {
            return new SolutionInfo(id, version, filePath, projects, deferredProjects, deferredProjectLoader);
        }
    }
}
