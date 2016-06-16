// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Linq;
using Roslyn.Utilities;

namespace Microsoft.CodeAnalysis
{
    internal class DeferredProjectState
    {
        public DeferredProjectInfo Info { get; }
        public ValueSource<ProjectState> Source { get; }

        public DeferredProjectState(DeferredProjectInfo info, ValueSource<ProjectState> source)
        {
            this.Info = info;
            this.Source = source;
        }

        public DeferredProjectState(ProjectState state)
            : this(DeferredProjectInfo.Create(state.Id, state.ProjectReferences.Select(pr => pr.ProjectId)), new ConstantValueSource<ProjectState>(state))
        {
        }
    }
}