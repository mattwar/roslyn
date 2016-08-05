// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Host;
using Microsoft.CodeAnalysis.Text;
using Microsoft.CodeAnalysis.Options;

namespace Microsoft.CodeAnalysis.Rename
{
    internal abstract class RenameService : ILanguageService
    {
        public abstract Task<RenameInfo> GetRenameInfoAsync(Document document, int position, CancellationToken cancellationToken);
        public abstract Task<RenameLocationsX> GetRenameLocationsAsync(Document document, RenameInfo info, OptionSet optionSet, CancellationToken cancellationToken);
        public abstract Task<RenameResult> RenameAsync(Document document, RenameInfo info, string replacementText, RenameLocationsX locations, OptionSet optionSet, CancellationToken cancellationToken);
    }
}
