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
        /// Project references are converted to metadata references if possible
        /// </summary>
        Metadata,

        /// <summary>
        /// Project refereces are converted to compilation references if possible.
        /// Deep compilation references transitively reference all referenced project project references, etc.
        /// </summary>
        DeepCompilation,

        /// <summary>
        /// Project references are converted to shallow compilation references if possible.
        /// Shallow compilation reference don't reference their own project references unless the root also does
        /// </summary>
        ShallowCompilation
    }
}