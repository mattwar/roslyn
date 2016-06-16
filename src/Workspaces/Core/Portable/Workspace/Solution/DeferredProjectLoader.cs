// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.CodeAnalysis
{
    public abstract class DeferredProjectLoader
    {
        /// <summary>
        /// Gets the <see cref="ProjectInfo"/> for the specfied project.
        /// </summary>
        public abstract Task<ProjectInfo> GetProjectInfoAsync(ProjectId projectId, CancellationToken cancellationToken);
    }
}