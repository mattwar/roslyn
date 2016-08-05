// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Host;
using Microsoft.CodeAnalysis.Text;
using Microsoft.CodeAnalysis.Options;

namespace Microsoft.CodeAnalysis.Rename
{
    internal sealed class RenameInfo
    {
        /// <summary>
        /// Whether or not the entity at the selected location can be renamed.
        /// </summary>
        public bool CanRename { get; private set; }

        /// <summary>
        /// Provides the reason that can be displayed to the user if the entity at the selected 
        /// location cannot be renamed.
        /// </summary>
        public string LocalizedErrorMessage { get; private set; }

        /// <summary>
        /// The span of the entity that is being renamed.
        /// </summary>
        public TextSpan TriggerSpan { get; private set; }

        /// <summary>
        /// The renameable item identified at the location in the document
        /// </summary>
        public RenameItem Item { get; private set; }

        /// <summary>
        /// Whether or not this entity has overloads that can also be renamed if the user wants.
        /// </summary>
        public bool HasOverloads { get; private set; }

        /// <summary>
        /// Whether the Rename Overloads option should be forced to true. Used if rename is invoked from within a nameof expression.
        /// </summary>
        public bool ForceRenameOverloads { get; private set; }

        /// <summary>
        /// The short name of the symbol being renamed, for use in displaying information to the user.
        /// </summary>
        public string DisplayName { get; private set; }

        /// <summary>
        /// The full name of the symbol being renamed, for use in displaying information to the user.
        /// </summary>
        public string FullDisplayName { get; private set; }

        /// <summary>
        /// Tags that correspond to UI representation
        /// </summary>
        public ImmutableArray<string> Tags { get; private set; }

        /// <summary>
        /// Properties specific to the service
        /// </summary>
        public ImmutableDictionary<string, string> Properties { get; private set; }

        private RenameInfo(
            bool canRename,
            string localizedErrorMessage,
            TextSpan triggerSpan,
            RenameItem item,
            bool hasOverloads,
            bool forceRenameOverloads,
            string displayName,
            string fullDisplayName,
            ImmutableArray<string> tags,
            ImmutableDictionary<string, string> properties)
        {
            this.CanRename = canRename;
            this.LocalizedErrorMessage = localizedErrorMessage ?? "";
            this.TriggerSpan = triggerSpan;
            this.Item = item;
            this.HasOverloads = hasOverloads;
            this.ForceRenameOverloads = forceRenameOverloads;
            this.DisplayName = displayName ?? "";
            this.FullDisplayName = fullDisplayName ?? this.DisplayName;
            this.Tags = tags.IsDefault ? ImmutableArray<string>.Empty : tags;
            this.Properties = properties ?? ImmutableDictionary<string, string>.Empty;
        }

        public static RenameInfo Create(
            TextSpan triggerSpan,
            RenameItem item,
            bool hasOverloads = false,
            bool forceRenameOverloads = false,
            string displayName = null,
            string fullDisplayName = null,
            ImmutableArray<string> tags = default(ImmutableArray<string>),
            ImmutableDictionary<string, string> properties = default(ImmutableDictionary<string, string>))
        {
            return new RenameInfo(
                canRename: true,
                localizedErrorMessage: null,
                triggerSpan: triggerSpan,
                item: item,
                hasOverloads: hasOverloads,
                forceRenameOverloads: forceRenameOverloads,
                displayName: displayName,
                fullDisplayName: fullDisplayName,
                tags: tags,
                properties: properties);
        }

        public static RenameInfo Create(
            string localizedErrorMessage)
        {
            return new RenameInfo(
                canRename: false,
                localizedErrorMessage: localizedErrorMessage,
                triggerSpan: default(TextSpan),
                item: null,
                hasOverloads: false,
                forceRenameOverloads: false,
                displayName: null,
                fullDisplayName: null,
                tags: default(ImmutableArray<string>),
                properties: default(ImmutableDictionary<string, string>));
        }

        private RenameInfo With(
            Optional<bool> canRename = default(Optional<bool>),
            Optional<string> localizedErrorMessage = default(Optional<string>),
            Optional<TextSpan> triggerSpan = default(Optional<TextSpan>),
            Optional<RenameItem> item = default(Optional<RenameItem>),
            Optional<bool> hasOverloads = default(Optional<bool>),
            Optional<bool> forceRenameOverloads = default(Optional<bool>),
            Optional<string> displayName = default(Optional<string>),
            Optional<string> fullDisplayName = default(Optional<string>),
            Optional<ImmutableArray<string>> tags = default(Optional<ImmutableArray<string>>),
            Optional<ImmutableDictionary<string, string>> properties = default(Optional<ImmutableDictionary<string, string>>))
        {
            var newCanRename = canRename.HasValue ? canRename.Value : this.CanRename;
            var newLocalizedErrorMessage = localizedErrorMessage.HasValue ? localizedErrorMessage.Value : this.LocalizedErrorMessage;
            var newTriggerSpan = triggerSpan.HasValue ? triggerSpan.Value : this.TriggerSpan;
            var newItem = item.HasValue ? item.Value : this.Item;
            var newHasOverloads = hasOverloads.HasValue ? hasOverloads.Value : this.HasOverloads;
            var newForceRenameOverloads = forceRenameOverloads.HasValue ? forceRenameOverloads.Value : this.ForceRenameOverloads;
            var newDisplayName = displayName.HasValue ? displayName.Value : this.DisplayName;
            var newFullDisplayName = fullDisplayName.HasValue ? fullDisplayName.Value : this.FullDisplayName;
            var newTags = tags.HasValue ? tags.Value : this.Tags;
            var newProperties = properties.HasValue ? properties.Value : this.Properties;

            if (newCanRename != this.CanRename ||
                newLocalizedErrorMessage != this.LocalizedErrorMessage ||
                newTriggerSpan != this.TriggerSpan ||
                newItem != this.Item ||
                newHasOverloads != this.HasOverloads ||
                newForceRenameOverloads != this.ForceRenameOverloads ||
                newDisplayName != this.DisplayName ||
                newFullDisplayName != this.FullDisplayName ||
                newTags != this.Tags ||
                newProperties != this.Properties)
            {
                return new RenameInfo(newCanRename, newLocalizedErrorMessage, newTriggerSpan, newItem, newHasOverloads, newForceRenameOverloads, newDisplayName, newFullDisplayName, newTags, newProperties);
            }
            else
            {
                return this;
            }
        }

        public RenameInfo WithCanRename(bool canRename)
        {
            return With(canRename: canRename);
        }

        public RenameInfo WithLocalizedErrorMessage(string localizedErrorMessage)
        {
            return With(localizedErrorMessage: localizedErrorMessage);
        }

        public RenameInfo WithTriggerSpan(TextSpan triggerSpan)
        {
            return With(triggerSpan: triggerSpan);
        }

        public RenameInfo WithItem(RenameItem item)
        {
            return With(item: item);
        }

        public RenameInfo WithHasOverloads(bool hasOverloads)
        {
            return With(hasOverloads: hasOverloads);
        }

        public RenameInfo WithForceRenameOverloads(bool forceRenameOverloads)
        {
            return With(forceRenameOverloads: forceRenameOverloads);
        }

        public RenameInfo WithDisplayName(string displayName)
        {
            return With(displayName: displayName);
        }

        public RenameInfo WithFullDisplayName(string fullDisplayName)
        {
            return With(fullDisplayName: fullDisplayName);
        }

        public RenameInfo WithTags(ImmutableArray<string> tags)
        {
            return With(tags: tags);
        }

        public RenameInfo WithProperties(ImmutableDictionary<string, string> properties)
        {
            return With(properties: properties);
        }
    }
}