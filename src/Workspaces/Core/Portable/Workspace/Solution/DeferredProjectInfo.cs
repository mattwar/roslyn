// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using Microsoft.CodeAnalysis.Diagnostics;
using Roslyn.Utilities;

namespace Microsoft.CodeAnalysis
{
    /// <summary>
    /// A class that represents all the arguments necessary to create a new project instance.
    /// </summary>
    public sealed class DeferredProjectInfo
    {
        /// <summary>
        /// The unique <see cref="ProjectId"/> of the project.
        /// </summary>
        public ProjectId Id { get; }

        /// <summary>
        /// The <see cref="ProjectId"/>s 
        /// </summary>
        public ImmutableArray<ProjectId> ReferencedProjectIds { get; }

        private DeferredProjectInfo(ProjectId id, ImmutableArray<ProjectId> referencedProjectIds)
        {
            this.Id = id;
            this.ReferencedProjectIds = referencedProjectIds.IsDefault ? ImmutableArray<ProjectId>.Empty : referencedProjectIds;
        }

        public static DeferredProjectInfo Create(ProjectId id, IEnumerable<ProjectId> referencedProjectIds = null)
        {
            return new DeferredProjectInfo(id, referencedProjectIds.ToImmutableArrayOrEmpty());
        }
    }
}