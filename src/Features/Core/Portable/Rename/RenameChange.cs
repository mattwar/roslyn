// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Host;
using Microsoft.CodeAnalysis.Text;
using Microsoft.CodeAnalysis.Options;

namespace Microsoft.CodeAnalysis.Rename
{
    internal sealed class RenameChange
    {
        public DocumentId DocumentId { get; private set; }
        public RenameChangeKind ChangeKind { get; private set; }
        public TextSpan OriginalSpan { get; private set; }
        public TextSpan NewSpan { get; private set; }

        public RenameChange(
            RenameChangeKind kind,
            DocumentId documentId,
            TextSpan originalSpan,
            TextSpan newSpan)
        {
            this.ChangeKind = kind;
            this.DocumentId = documentId;
            this.OriginalSpan = originalSpan;
            this.NewSpan = newSpan;
        }
    }
}