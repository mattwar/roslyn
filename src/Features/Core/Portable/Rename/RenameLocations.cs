// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Host;
using Microsoft.CodeAnalysis.Text;
using Microsoft.CodeAnalysis.Options;

namespace Microsoft.CodeAnalysis.Rename
{
    internal sealed class RenameLocationsX
    {
        public ImmutableArray<RenameLocationX> Locations { get; }
        public ImmutableArray<RenameItem> ReferencedItems { get; }

        public RenameLocationsX(
            ImmutableArray<RenameLocationX> locations, 
            ImmutableArray<RenameItem> referencedItems)
        {
            this.Locations = locations.IsDefault ? ImmutableArray<RenameLocationX>.Empty : locations;
            this.ReferencedItems = referencedItems.IsDefault ? ImmutableArray<RenameItem>.Empty : referencedItems;
        }

        public static readonly RenameLocationsX Empty = new RenameLocationsX(ImmutableArray<RenameLocationX>.Empty, ImmutableArray<RenameItem>.Empty);
    }
}