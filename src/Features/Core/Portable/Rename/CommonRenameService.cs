// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Host;
using Microsoft.CodeAnalysis.Text;
using Microsoft.CodeAnalysis.Options;
using Microsoft.CodeAnalysis.LanguageServices;
using Roslyn.Utilities;
using Microsoft.CodeAnalysis.Shared.Extensions;
using System;
using Microsoft.CodeAnalysis.FindSymbols;
using Microsoft.CodeAnalysis.Navigation;
using System.Linq;
using Microsoft.CodeAnalysis.Rename.ConflictEngine;
using System.Collections.Generic;

namespace Microsoft.CodeAnalysis.Rename
{
    internal abstract class CommonRenameService : RenameService
    {
        internal CommonRenameService()
        {
        }

        #region RenameInfo
        private const string You_must_rename_an_identifier = nameof(You_must_rename_an_identifier);
        private const string You_cannot_rename_this_element = nameof(You_cannot_rename_this_element);
        private const string Renaming_anonymous_type_members_is_not_yet_supported = nameof(Renaming_anonymous_type_members_is_not_yet_supported);
        private const string Please_resolve_errors_in_your_code_before_renaming_this_element = nameof(Please_resolve_errors_in_your_code_before_renaming_this_element);
        private const string You_cannot_rename_operators = nameof(You_cannot_rename_operators);
        private const string You_cannot_rename_elements_that_are_defined_in_metadata = nameof(You_cannot_rename_elements_that_are_defined_in_metadata);
        private const string You_cannot_rename_elements_from_previous_submissions = nameof(You_cannot_rename_elements_from_previous_submissions);

        public override async Task<RenameInfo> GetRenameInfoAsync(Document document, int position, CancellationToken cancellationToken)
        {
            var triggerToken = GetTriggerToken(document, position, cancellationToken);
            if (triggerToken == default(SyntaxToken))
            {
                return RenameInfo.Create(You_must_rename_an_identifier);
            }

            var syntaxFactsService = document.Project.LanguageServices.GetService<ISyntaxFactsService>();
            if (syntaxFactsService.IsKeyword(triggerToken))
            {
                return RenameInfo.Create(You_must_rename_an_identifier);
            }

            var semanticModel = document.GetSemanticModelAsync(cancellationToken).WaitAndGetResult(cancellationToken);
            var semanticFacts = document.GetLanguageService<ISemanticFactsService>();

            var tokenRenameInfo = RenameUtilities.GetTokenRenameInfo(semanticFacts, semanticModel, triggerToken, cancellationToken);

            // Rename was invoked on a member group reference in a nameof expression.
            // Trigger the rename on any of the candidate symbols but force the 
            // RenameOverloads option to be on.
            var triggerSymbol = tokenRenameInfo.HasSymbols ? tokenRenameInfo.Symbols.First() : null;
            if (triggerSymbol == null)
            {
                return RenameInfo.Create(You_cannot_rename_this_element);
            }

            // see https://github.com/dotnet/roslyn/issues/10898
            // we are disabling rename for tuple fields for now
            // 1) compiler does not return correct location information in these symbols
            // 2) renaming tuple fields seems a complex enough thing to require some design
            if (triggerSymbol.ContainingType?.IsTupleType == true)
            {
                return RenameInfo.Create(You_cannot_rename_this_element);
            }

            // If rename is invoked on a member group reference in a nameof expression, then the
            // RenameOverloads option should be forced on.
            var forceRenameOverloads = tokenRenameInfo.IsMemberGroup;

            if (syntaxFactsService.IsTypeNamedVarInVariableOrFieldDeclaration(triggerToken, triggerToken.Parent))
            {
                // To check if var in this context is a real type, or the keyword, we need to 
                // speculatively bind the identifier "var". If it returns a symbol, it's a real type,
                // if not, it's the keyword.
                // see bugs 659683 (compiler API) and 659705 (rename/workspace api) for examples
                var symbolForVar = semanticModel.GetSpeculativeSymbolInfo(
                    triggerToken.SpanStart,
                    triggerToken.Parent,
                    SpeculativeBindingOption.BindAsTypeOrNamespace).Symbol;

                if (symbolForVar == null)
                {
                    return RenameInfo.Create(You_cannot_rename_this_element);
                }
            }

            var symbol = await RenameLocations.ReferenceProcessing.GetRenamableSymbolAsync(document, triggerToken.SpanStart, cancellationToken: cancellationToken).ConfigureAwait(false);
            if (symbol == null)
            {
                return RenameInfo.Create(You_cannot_rename_this_element);
            }

            if (symbol.Kind == SymbolKind.Alias && symbol.IsExtern)
            {
                return RenameInfo.Create(You_cannot_rename_this_element);
            }

            // Cannot rename constructors in VB.  TODO: this logic should be in the VB subclass of this type.
            var workspace = document.Project.Solution.Workspace;
            if (symbol != null &&
                symbol.Kind == SymbolKind.NamedType &&
                symbol.Language == LanguageNames.VisualBasic &&
                triggerToken.ToString().Equals("New", StringComparison.OrdinalIgnoreCase))
            {
                var originalSymbol = await SymbolFinder.FindSymbolAtPositionAsync(semanticModel, triggerToken.SpanStart, workspace, cancellationToken: cancellationToken).ConfigureAwait(false);

                if (originalSymbol != null && originalSymbol.IsConstructor())
                {
                    return RenameInfo.Create(You_cannot_rename_this_element);
                }
            }

            if (syntaxFactsService.IsTypeNamedDynamic(triggerToken, triggerToken.Parent))
            {
                if (symbol.Kind == SymbolKind.DynamicType)
                {
                    return RenameInfo.Create(You_cannot_rename_this_element);
                }
            }

            // we allow implicit locals and parameters of Event handlers
            if (symbol.IsImplicitlyDeclared &&
                symbol.Kind != SymbolKind.Local &&
                !(symbol.Kind == SymbolKind.Parameter &&
                  symbol.ContainingSymbol.Kind == SymbolKind.Method &&
                  symbol.ContainingType != null &&
                  symbol.ContainingType.IsDelegateType() &&
                  symbol.ContainingType.AssociatedSymbol != null))
            {
                // We enable the parameter in RaiseEvent, if the Event is declared with a signature. If the Event is declared as a 
                // delegate type, we do not have a connection between the delegate type and the event.
                // this prevents a rename in this case :(.
                return RenameInfo.Create(You_cannot_rename_this_element);
            }

            if (symbol.Kind == SymbolKind.Property && symbol.ContainingType.IsAnonymousType)
            {
                return RenameInfo.Create(Renaming_anonymous_type_members_is_not_yet_supported);
            }

            if (symbol.IsErrorType())
            {
                return RenameInfo.Create(Please_resolve_errors_in_your_code_before_renaming_this_element);
            }

            if (symbol.Kind == SymbolKind.Method && ((IMethodSymbol)symbol).MethodKind == MethodKind.UserDefinedOperator)
            {
                return RenameInfo.Create(You_cannot_rename_operators);
            }

            var symbolLocations = symbol.Locations;

            // Does our symbol exist in an unchangeable location?
            var navigationService = workspace.Services.GetService<IDocumentNavigationService>();
            foreach (var location in symbolLocations)
            {
                if (location.IsInMetadata)
                {
                    return RenameInfo.Create(You_cannot_rename_elements_that_are_defined_in_metadata);
                }
                else if (location.IsInSource)
                {
                    if (document.Project.IsSubmission)
                    {
                        var solution = document.Project.Solution;
                        var projectIdOfLocation = solution.GetDocument(location.SourceTree).Project.Id;

                        if (solution.Projects.Any(p => p.IsSubmission && p.ProjectReferences.Any(r => r.ProjectId == projectIdOfLocation)))
                        {
                            return RenameInfo.Create(You_cannot_rename_elements_from_previous_submissions);
                        }
                    }
                    else
                    {
#if false  // move to caller somehow or ???
                        var sourceText = location.SourceTree.GetTextAsync(cancellationToken).WaitAndGetResult(cancellationToken);
                        var textSnapshot = sourceText.FindCorrespondingEditorTextSnapshot();

                        if (textSnapshot != null)
                        {
                            var buffer = textSnapshot.TextBuffer;
                            var originalSpan = location.SourceSpan.ToSnapshotSpan(textSnapshot).TranslateTo(buffer.CurrentSnapshot, SpanTrackingMode.EdgeInclusive);

                            if (buffer.IsReadOnly(originalSpan) || !navigationService.CanNavigateToSpan(workspace, document.Id, location.SourceSpan))
                            {
                                return new FailureInlineRenameInfo(EditorFeaturesResources.You_cannot_rename_this_element);
                            }
                        }
#endif
                    }
                }
                else
                {
                    return RenameInfo.Create(You_cannot_rename_this_element);
                }
            }

            var triggerSpan = triggerToken.Span;
            var isRenamingAttributePrefix = CanRenameAttributePrefix(document, triggerSpan, symbol, cancellationToken);
            var actualTriggerSpan = GetReferenceEditSpan(document, triggerSpan, symbol, isRenamingAttributePrefix, cancellationToken);

            return RenameInfo.Create(
                triggerSpan: actualTriggerSpan,
                item: new RenameItem(SymbolKey.Create(symbol).ToString()),
                hasOverloads: RenameLocations.GetOverloadedSymbols(symbol).Any(),
                forceRenameOverloads: forceRenameOverloads,
                displayName: symbol.Name,
                fullDisplayName: symbol.ToDisplayString(),
                tags: Completion.GlyphTags.GetTags(symbol.GetGlyph()))
                .WithIsAttributePrefix(isRenamingAttributePrefix)
                .WithIsShortenedTriggerSpan(actualTriggerSpan != triggerSpan);
        }

        private SyntaxToken GetTriggerToken(Document document, int position, CancellationToken cancellationToken)
        {
            var syntaxTree = document.GetSyntaxTreeSynchronously(cancellationToken);
            var syntaxFacts = document.Project.LanguageServices.GetService<ISyntaxFactsService>();
            var token = syntaxTree.GetTouchingWordAsync(position, syntaxFacts, cancellationToken, findInsideTrivia: true).WaitAndGetResult(cancellationToken);

            return token;
        }

        private TextSpan GetReferenceEditSpan(Document document, TextSpan span, ISymbol renameSymbol, bool isRenamingAttributePrefix, CancellationToken cancellationToken)
        {
            var searchName = renameSymbol.Name;
            if (isRenamingAttributePrefix)
            {
                // We're only renaming the attribute prefix part.  We want to adjust the span of 
                // the reference we've found to only update the prefix portion.
                searchName = GetWithoutAttributeSuffix(renameSymbol.Name);
            }

            var spanText = GetSpanText(document, span, cancellationToken);
            var index = spanText.LastIndexOf(searchName, StringComparison.Ordinal);

            if (index < 0)
            {
                // Couldn't even find the search text at this reference location.  This might happen
                // if the user used things like unicode escapes.  IN that case, we'll have to rename
                // the entire identifier.
                return span;
            }

            return new TextSpan(span.Start + index, searchName.Length);
        }

        private static string GetWithoutAttributeSuffix(string value)
        {
            return value.GetWithoutAttributeSuffix(isCaseSensitive: true);
        }

        private bool CanRenameAttributePrefix(Document document, TextSpan triggerSpan, ISymbol renameSymbol, CancellationToken cancellationToken)
        {
            // if this isn't an attribute, or it doesn't have the 'Attribute' suffix, then clearly
            // we can't rename just the attribute prefix.
            if (!this.IsRenamingAttributeTypeWithAttributeSuffix(renameSymbol))
            {
                return false;
            }

            // Ok, the symbol is good.  Now, make sure that the trigger text starts with the prefix
            // of the attribute.  If it does, then we can rename just the attribute prefix (otherwise
            // we need to rename the entire attribute).
            var nameWithoutAttribute = renameSymbol.Name.GetWithoutAttributeSuffix(isCaseSensitive: true);
            var triggerText = GetSpanText(document, triggerSpan, cancellationToken);

            return triggerText.StartsWith(triggerText); // TODO: Always true? What was it supposed to do?
        }

        private bool IsRenamingAttributeTypeWithAttributeSuffix(ISymbol renameSymbol)
        {
            if (renameSymbol.IsAttribute() || (renameSymbol.Kind == SymbolKind.Alias && ((IAliasSymbol)renameSymbol).Target.IsAttribute()))
            {
                var name = renameSymbol.Name;
                if (name.TryGetWithoutAttributeSuffix(isCaseSensitive: true, result: out name))
                {
                    return true;
                }
            }

            return false;
        }

        private static string GetSpanText(Document document, TextSpan triggerSpan, CancellationToken cancellationToken)
        {
            var sourceText = document.GetTextAsync(cancellationToken).WaitAndGetResult(cancellationToken);
            var triggerText = sourceText.ToString(triggerSpan);
            return triggerText;
        }
        #endregion

        #region RenameLocations

        public override async Task<RenameLocationsX> GetRenameLocationsAsync(
            Document document, RenameInfo info, OptionSet optionSet, CancellationToken cancellationToken)
        {
            var symbol = await info.GetSymbolAsync(document, cancellationToken).ConfigureAwait(false);

            var options = optionSet ?? document.Options;

            if (symbol != null)
            {
                var renameLocations = await RenameLocations.FindAsync(symbol, document.Project.Solution, options, cancellationToken).ConfigureAwait(false);

                var locations = renameLocations.Locations
                    .Where(loc => loc.Location.IsInSource)
                    .Select(loc => CreateLocation(loc));

                var implicitLocations = renameLocations.ImplicitLocations
                    .Where(loc => loc.Location.IsInSource)
                    .Select(loc => CreateLocation(loc));

                var referencedItems = renameLocations.ReferencedSymbols
                     .Select(sym => CreateRenameItem(sym));

                return new RenameLocationsX(
                    locations: locations.Concat(implicitLocations).ToImmutableArray(),
                    referencedItems: referencedItems.ToImmutableArray());
            }
            else
            {
                return RenameLocationsX.Empty;
            }
        }

        private static RenameLocationX CreateLocation(RenameLocation location)
        {
            return RenameLocationX.Create(
                location.DocumentId,
                location.Location.SourceSpan,
                ImmutableDictionary<string, string>.Empty
                    .WithBooleanProperty("IsCandidateLocation", location.IsCandidateLocation)
                    .WithBooleanProperty("IsMethodGroup", location.IsMethodGroupReference)
                    .WithBooleanProperty("IsAccessor", location.IsRenamableAccessor)
                    .WithBooleanProperty("IsAlias", location.IsRenamableAliasUsage)
                    .WithBooleanProperty("IsWrittenTo", location.IsWrittenTo)
                    .WithIntProperty("StringOrCommentStart", location.ContainingLocationForStringOrComment.Start)
                    .WithIntProperty("StringOrCommentLength", location.ContainingLocationForStringOrComment.Length));
        }

        private static async Task<RenameLocation> GetRenameLocationAsync(RenameLocationX location, Solution solution, CancellationToken cancellationToken)
        {
            var doc = solution.GetDocument(location.DocumentId);
            var tree = await doc.GetSyntaxTreeAsync(cancellationToken).ConfigureAwait(false);
            var loc = tree.GetLocation(location.Span);
            return new RenameLocation(
                loc,
                location.DocumentId,
                location.Properties.GetBooleanProperty("IsCandidateLocation"),
                location.Properties.GetBooleanProperty("IsMethodGroup"),
                location.Properties.GetBooleanProperty("IsAlias"),
                location.Properties.GetBooleanProperty("IsAccessor"),
                location.Properties.GetBooleanProperty("IsWrittenTo"),
                new TextSpan(location.Properties.GetIntProperty("StringOrCommentStart"), location.Properties.GetIntProperty("StringOrCommentLength")));
        }

        private RenameLocationX CreateLocation(ReferenceLocation location)
        {
            return RenameLocationX.Create(
                location.Document.Id,
                location.Location.SourceSpan,
                ImmutableDictionary<string, string>.Empty
                    .WithSymbolProprety("AliasSymbol", location.Alias, CancellationToken.None)
                    .WithBooleanProperty("IsImplicit", location.IsImplicit)
                    .WithBooleanProperty("IsWrittenTo", location.IsWrittenTo)
                    .WithEnumProperty<CandidateReason>("CandidateReason", location.CandidateReason));
        }

        private static async Task<ReferenceLocation> GetReferenceLocationAsync(RenameLocationX location, Solution solution, CancellationToken cancellationToken)
        {
            var doc = solution.GetDocument(location.DocumentId);
            var tree = await doc.GetSyntaxTreeAsync(cancellationToken).ConfigureAwait(false);
            var loc = tree.GetLocation(location.Span);
            return new ReferenceLocation(
                doc,
                (IAliasSymbol)(await location.Properties.GetSymbolAsync("AliasSymbol", doc, cancellationToken).ConfigureAwait(false)),
                loc,
                location.Properties.GetBooleanProperty("IsImplicit"),
                location.Properties.GetBooleanProperty("IsWrittenTo"),
                location.Properties.GetEnumProperty<CandidateReason>("CandidateReason"));
        }

        private RenameItem CreateRenameItem(ISymbol symbol)
        {
            return new RenameItem(SymbolKey.Create(symbol).ToString());
        }

        private static async Task<ISymbol> GetSymbolAsync(Document document, RenameItem item, CancellationToken cancellationToken)
        {
            var compilation = await document.Project.GetCompilationAsync(cancellationToken).ConfigureAwait(false);
            return SymbolKey.Resolve(item.ItemId, compilation).Symbol;
        }

        #endregion

        #region Rename

        public override async Task<RenameResult> RenameAsync(
            Document document, RenameInfo info, string replacementText, RenameLocationsX locations, 
            OptionSet optionSet, CancellationToken cancellationToken)
        {
            var symbol = await info.GetSymbolAsync(document, cancellationToken).ConfigureAwait(false);

            var options = optionSet ?? document.Options;

            if (symbol != null)
            {
                var solution = document.Project.Solution;
                var renameLocations = (await locations.Locations.Where(loc => !loc.Properties.ContainsKey("IsImplicit")).SelectAsync((loc, ct) => GetRenameLocationAsync(loc, solution, ct), cancellationToken).ConfigureAwait(false)).ToImmutableHashSet();
                var implicitLocations = (await locations.Locations.Where(loc => loc.Properties.ContainsKey("IsImplicit")).SelectAsync((loc, ct) => GetReferenceLocationAsync(loc, solution, ct), cancellationToken).ConfigureAwait(false)).ToImmutableArray();
                var referencedSymbols = (await locations.ReferencedItems.SelectAsync((item, ct) => GetSymbolAsync(document, item, ct), cancellationToken).ConfigureAwait(false)).ToImmutableArray();

                // recompute location set
                var locationSet = await RenameLocations.FindAsync(
                    symbol, document.Project.Solution, 
                    renameLocations, implicitLocations, referencedSymbols,
                    options, cancellationToken).ConfigureAwait(false);

                var conflicts = await ConflictResolver.ResolveConflictsAsync(
                    locationSet, symbol.Name,
                    GetFinalSymbolName(info, replacementText), 
                    optionSet, 
                    hasConflict: null, 
                    cancellationToken: cancellationToken).ConfigureAwait(false);

                return new RenameResult(
                    conflicts.NewSolution,
                    GetAllChanges(conflicts),
                    conflicts.ReplacementTextValid);
            }
            else
            {
                return new RenameResult(document.Project.Solution, ImmutableArray<RenameChange>.Empty, replacementTextValid: false);
            }
        }

        private const string AttributeSuffix = "Attribute";

        public static string GetFinalSymbolName(RenameInfo info, string replacementText)
        {
            if (info.GetIsAttributePrefix())
            {
                return replacementText + AttributeSuffix;
            }

            return replacementText;
        }


        private ImmutableArray<RenameChange> GetAllChanges(ConflictResolution conflicts)
        {
            var list = new List<RenameChange>();

            foreach (var docId in conflicts.DocumentIds)
            {
                list.AddRange(GetChanges(conflicts, docId));
            }

            return list.ToImmutableArray();
        }

        private IEnumerable<RenameChange> GetChanges(ConflictResolution conflicts, DocumentId documentId)
        {
            var nonComplexifiedSpans = GetNonComplexifiedChanges(conflicts, documentId);
            var complexifiedSpans = GetComplexifiedChanges(conflicts, documentId);

            return nonComplexifiedSpans.Concat(complexifiedSpans);
        }

        private IEnumerable<RenameChange> GetNonComplexifiedChanges(ConflictResolution conflicts, DocumentId documentId)
        {
            var modifiedSpans = conflicts.RenamedSpansTracker.GetModifiedSpanMap(documentId);
            var locationsForDocument = conflicts.GetRelatedLocationsForDocument(documentId);

            // The RenamedSpansTracker doesn't currently track unresolved conflicts for
            // unmodified locations.  If the document wasn't modified, we can just use the 
            // original span as the new span, but otherwise we need to filter out 
            // locations that aren't in modifiedSpans. 
            if (modifiedSpans.Any())
            {
                return locationsForDocument.Where(loc => modifiedSpans.ContainsKey(loc.ConflictCheckSpan))
                                           .Select(loc => new RenameChange(GetChangeKind(loc), loc.DocumentId, loc.ConflictCheckSpan, modifiedSpans[loc.ConflictCheckSpan]));
            }
            else
            {
                return locationsForDocument.Select(loc => new RenameChange(GetChangeKind(loc), loc.DocumentId, loc.ConflictCheckSpan, loc.ConflictCheckSpan));
            }
        }

        private IEnumerable<RenameChange> GetComplexifiedChanges(ConflictResolution conflicts, DocumentId documentId)
        {
            return conflicts.RenamedSpansTracker.GetComplexifiedSpans(documentId)
                .Select(s => new RenameChange(RenameChangeKind.Complexified, documentId, s.Item1, s.Item2));
        }

        private static RenameChangeKind GetChangeKind(RelatedLocation location)
        {
            switch (location.Type)
            {
                case RelatedLocationType.NoConflict:
                    return RenameChangeKind.Replaced;
                case RelatedLocationType.ResolvedReferenceConflict:
                    return RenameChangeKind.ResolvedReferenceConflict;
                case RelatedLocationType.ResolvedNonReferenceConflict:
                    return RenameChangeKind.ResolvedNonReferenceConflict;
                case RelatedLocationType.UnresolvableConflict:
                case RelatedLocationType.UnresolvedConflict:
                    return RenameChangeKind.UnresolvedConflict;
                default:
                case RelatedLocationType.PossiblyResolvableConflict:
                    throw ExceptionUtilities.Unreachable;
            }
        }
        #endregion
    }
}