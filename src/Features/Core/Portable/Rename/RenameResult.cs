// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Collections.Immutable;

namespace Microsoft.CodeAnalysis.Rename
{
    internal sealed class RenameResult
    {
        /// <summary>
        /// The solution after the rename (with conflicts resolved, if possible)
        /// </summary>
        public Solution Solution { get; private set; }

        /// <summary>
        /// The specific changes that were made, and/or where conflicts occurred
        /// </summary>
        public ImmutableArray<RenameChange> Changes { get; private set; }

        /// <summary>
        /// Whether or not the replacement text entered by the user is valid.
        /// </summary>
        bool ReplacementTextValid { get; }

        public RenameResult(
            Solution newSolution,
            ImmutableArray<RenameChange> changes,
            bool replacementTextValid)
        {
            this.Solution = newSolution;
            this.Changes = changes;
            this.ReplacementTextValid = replacementTextValid;
        }
    }
}