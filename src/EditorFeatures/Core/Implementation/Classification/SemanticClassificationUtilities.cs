﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Classification;
using Microsoft.CodeAnalysis.Editor.Shared.Extensions;
using Microsoft.CodeAnalysis.Editor.Shared.Utilities;
using Microsoft.CodeAnalysis.Editor.Tagging;
using Microsoft.CodeAnalysis.ErrorReporting;
using Microsoft.CodeAnalysis.Internal.Log;
using Microsoft.CodeAnalysis.LanguageServices;
using Microsoft.CodeAnalysis.Text;
using Microsoft.CodeAnalysis.Text.Shared.Extensions;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Tagging;
using Roslyn.Utilities;

namespace Microsoft.CodeAnalysis.Editor.Implementation.Classification
{
    internal static class SemanticClassificationUtilities
    {
        public static async Task ProduceTagsAsync(TaggerContext<IClassificationTag> context, DocumentSnapshotSpan spanToTag,
            ClassificationService classificationService, ClassificationTypeMap typeMap)
        {
            var document = spanToTag.Document;
            if (document == null)
            {
                return;
            }

            if (await TryClassifyContainingMemberSpan(context, spanToTag, classificationService, typeMap).ConfigureAwait(false))
            {
                return;
            }

            // We weren't able to use our specialized codepaths for semantic classifying. 
            // Fall back to classifying the full span that was asked for.
            await ClassifySpansAsync(context, spanToTag, classificationService, typeMap).ConfigureAwait(false);
        }

        private static async Task<bool> TryClassifyContainingMemberSpan(TaggerContext<IClassificationTag> context, DocumentSnapshotSpan spanToTag,
            ClassificationService classificationService, ClassificationTypeMap typeMap)
        {
            var range = context.TextChangeRange;
            if (range == null)
            {
                // There was no text change range, we can't just reclassify a member body.
                return false;
            }

            // there was top level edit, check whether that edit updated top level element
            var document = spanToTag.Document;
            var cancellationToken = context.CancellationToken;

            var lastSemanticVersion = (VersionStamp?)context.State;
            if (lastSemanticVersion != null)
            {
                var currentSemanticVersion = await document.Project.GetDependentSemanticVersionAsync(cancellationToken).ConfigureAwait(false);
                if (lastSemanticVersion.Value != currentSemanticVersion)
                {
                    // A top level change was made.  We can't perform this optimization.
                    return false;
                }
            }

            var service = document.Project.LanguageServices.GetService<ISyntaxFactsService>();

            // perf optimization. Check whether all edits since the last update has happened within
            // a member. If it did, it will find the member that contains the changes and only refresh
            // that member.  If possible, try to get a speculative binder to make things even cheaper.

            var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);

            var changedSpan = new TextSpan(range.Value.Span.Start, range.Value.NewLength);
            var member = service.GetContainingMemberDeclaration(root, changedSpan.Start);
            if (member == null || !member.FullSpan.Contains(changedSpan))
            {
                // The edit was not fully contained in a member.  Reclassify everything.
                return false;
            }

            var subTextSpan = service.GetMemberBodySpanForSpeculativeBinding(member);
            if (subTextSpan.IsEmpty)
            {
                // Wasn't a member we could reclassify independently.
                return false;
            }

            var subSpan = subTextSpan.Contains(changedSpan) ? subTextSpan.ToSpan() : member.FullSpan.ToSpan();

            var subSpanToTag = new DocumentSnapshotSpan(spanToTag.Document,
                new SnapshotSpan(spanToTag.SnapshotSpan.Snapshot, subSpan));

            // re-classify only the member we're inside.
            await ClassifySpansAsync(context, subSpanToTag, classificationService, typeMap).ConfigureAwait(false);
            return true;
        }

        private static async Task ClassifySpansAsync(TaggerContext<IClassificationTag> context, DocumentSnapshotSpan spanToTag,
            ClassificationService classificationService, ClassificationTypeMap typeMap)
        {
            try
            {
                var document = spanToTag.Document;
                var snapshotSpan = spanToTag.SnapshotSpan;
                var snapshot = snapshotSpan.Snapshot;

                var cancellationToken = context.CancellationToken;
                using (Logger.LogBlock(FunctionId.Tagger_SemanticClassification_TagProducer_ProduceTags, cancellationToken))
                {
                    var classifiedSpans = ClassificationUtilities.GetOrCreateClassifiedSpanList();

                    var newSpans = await classificationService.GetSemanticClassificationsAsync(
                        document, snapshotSpan.Span.ToTextSpan(), cancellationToken).ConfigureAwait(false);

                    CombineSpans(classifiedSpans, newSpans);

                    ClassificationUtilities.Convert(typeMap, snapshotSpan.Snapshot, classifiedSpans, context.AddTag);
                    ClassificationUtilities.ReturnClassifiedSpanList(classifiedSpans);

                    var version = await document.Project.GetDependentSemanticVersionAsync(cancellationToken).ConfigureAwait(false);

                    // Let the context know that this was the span we actually tried to tag.
                    context.SetSpansTagged(SpecializedCollections.SingletonEnumerable(spanToTag));
                    context.State = version;
                }
            }
            catch (Exception e) when (FatalError.ReportUnlessCanceled(e))
            {
                throw ExceptionUtilities.Unreachable;
            }
        }

        private static void CombineSpans(List<ClassifiedSpan> spans, IEnumerable<ClassifiedSpan> newSpans)
        {
            var spanSet = SharedPools.Default<HashSet<ClassifiedSpan>>().Allocate();
            try
            {
                spanSet.AddAll(spans);

                foreach (var span in newSpans)
                {
                    if (!spanSet.Contains(span))
                    {
                        spanSet.Add(span);
                        spans.Add(span);
                    }
                }
            }
            finally
            {
                SharedPools.Default<HashSet<ClassifiedSpan>>().ClearAndFree(spanSet);
            }
        }
    }
}
