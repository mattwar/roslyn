// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Host;
using Microsoft.CodeAnalysis.Text;
using Microsoft.CodeAnalysis.Options;

namespace Microsoft.CodeAnalysis.Rename
{
    internal sealed class RenameLocationX
    {
        public DocumentId DocumentId { get; private set; }
        public TextSpan Span { get; private set; }
        public ImmutableDictionary<string, string> Properties { get; private set; }

        private RenameLocationX(DocumentId documentId, TextSpan span, ImmutableDictionary<string, string> properties)
        {
            this.DocumentId = documentId;
            this.Span = span;
            this.Properties = properties ?? ImmutableDictionary<string, string>.Empty;
        }

        public static RenameLocationX Create(
            DocumentId documentId,
            TextSpan span,
            ImmutableDictionary<string, string> properities = default(ImmutableDictionary<string, string>))
        {
            if (documentId == null)
            {
                throw new ArgumentNullException(nameof(documentId));
            }

            return new RenameLocationX(documentId, span, properities);
        }
    }
}