﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using Microsoft.VisualStudio.Shell.Interop;

namespace Microsoft.VisualStudio.LanguageServices.Implementation.Suppression
{
    /// <summary>
    /// Service to allow adding or removing bulk suppressions (in source or suppressions file).
    /// </summary>
    /// <remarks>TODO: Move to the core platform layer.</remarks>
    internal interface IVisualStudioSuppressionFixService
    {
        /// <summary>
        /// Adds source suppressions for diagnostics.
        /// </summary>
        /// <param name="selectedErrorListEntriesOnly">If true, then only the currently selected entries in the error list will be suppressed. Otherwise, all suppressable entries in the error list will be suppressed.</param>
        /// <param name="suppressInSource">If true, then suppressions will be generated inline in the source file. Otherwise, they will be generated in a separate global suppressions file.</param>
        bool AddSuppressions(bool selectedErrorListEntriesOnly, bool suppressInSource);

        /// <summary>
        /// Removes source suppressions for suppressed diagnostics.
        /// </summary>
        /// <param name="selectedErrorListEntriesOnly">If true, then only the currently selected entries in the error list will be unsuppressed. Otherwise, all unsuppressable entries in the error list will be unsuppressed.</param>
        bool RemoveSuppressions(bool selectedErrorListEntriesOnly);
    }
}
