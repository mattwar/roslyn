// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using Microsoft.CodeAnalysis.Text;
using Roslyn.Utilities;

namespace Microsoft.CodeAnalysis
{
    internal enum ProjectReferenceMode
    {
        /// <summary>
        /// Project references are converted to metadata references if possible.
        /// </summary>
        MetadataReferences,

        /// <summary>
        /// Project references are converted to deep compilation references if possible (same language) falling back to metadata references otherwise.
        /// Compilation references transitively reference all referenced project compilations, etc.
        /// </summary>
        DeepCompilationReferences,

        /// <summary>
        /// Project references are converted to shallow compilation references if possible (same language) falling back to metadata references otherwise.
        /// Compilation references do not transitively reference other referenced project compilations unless the root compilation already does.
        /// </summary>
        ShallowCompilationReferences
    }
}