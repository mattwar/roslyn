// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

namespace Microsoft.CodeAnalysis.Rename
{
    internal sealed class RenameItem
    {
        public string ItemId { get; private set; }

        public RenameItem(string itemId)
        {
            this.ItemId = itemId;
        }
    }
}