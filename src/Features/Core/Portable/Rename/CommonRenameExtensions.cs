// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Host;
using Microsoft.CodeAnalysis.Text;
using Microsoft.CodeAnalysis.Options;
using System;

namespace Microsoft.CodeAnalysis.Rename
{
    internal static class CommonRenameExtensions
    {
        private const string SymbolProperty = "Symbol";
        private const string IsAttributePrefixProperty = "IsAttributePrefix";
        private const string IsShortenedTriggerSpanProperty = "IsShortenedTriggerSpan";

        public static RenameInfo WithSymbol(this RenameInfo info, ISymbol symbol, CancellationToken cancellationToken)
        {
            return info.WithItem(new RenameItem(SymbolKey.Create(symbol, cancellationToken).ToString()));
        }

        public static async Task<ISymbol> GetSymbolAsync(this RenameInfo info, Document document, CancellationToken cancellationToken)
        {
            var compilation = await document.Project.GetCompilationAsync(cancellationToken).ConfigureAwait(false);
            var resolution = SymbolKey.Resolve(info.Item.ItemId, compilation, cancellationToken: cancellationToken);
            return resolution.Symbol;
        }

        public static RenameInfo WithIsAttributePrefix(this RenameInfo info, bool isAttributePrefix)
        {
            return info.WithProperty(IsAttributePrefixProperty, isAttributePrefix);
        }

        public static bool GetIsAttributePrefix(this RenameInfo info)
        {
            return info.GetBooleanProperty(IsAttributePrefixProperty);
        }

        public static RenameInfo WithIsShortenedTriggerSpan(this RenameInfo info, bool isShortedTriggerSpan)
        {
            return info.WithProperty(IsShortenedTriggerSpanProperty, isShortedTriggerSpan);
        }

        public static bool GetIsShortenedTriggerSpan(this RenameInfo info)
        {
            return info.GetBooleanProperty(IsShortenedTriggerSpanProperty);
        }

        public static RenameInfo WithProperty(this RenameInfo info, string propertyName, bool value)
        {
            return info.WithProperties(info.Properties.WithBooleanProperty(propertyName, value));
        }

        public static bool GetBooleanProperty(this RenameInfo info, string propertyName)
        {
            return info.Properties.GetBooleanProperty(propertyName);
        }

        public static ImmutableDictionary<string, string> WithBooleanProperty(this ImmutableDictionary<string, string> properties, string propertyName, bool value)
        {
            if (value)
            {
                return properties.Add(propertyName, "true"));
            }
            else if (properties.ContainsKey(propertyName))
            {
                return properties.Remove(propertyName);
            }
            else
            {
                return properties;
            }
        }

        public static bool GetBooleanProperty(this ImmutableDictionary<string, string> properties, string propertyName)
        {
            string valueText;
            bool value;
            return properties.TryGetValue(propertyName, out valueText) && bool.TryParse(valueText, out value) && value;
        }

        public static ImmutableDictionary<string, string> WithSymbolProprety(this ImmutableDictionary<string, string> properties, string propertyName, ISymbol symbol, CancellationToken cancellationToken)
        {
            if (symbol != null)
            {
                return properties.Add(propertyName, SymbolKey.Create(symbol, cancellationToken).ToString());
            }
            else if (properties.ContainsKey(propertyName))
            {
                return properties.Remove(propertyName);
            }
            else
            {
                return properties;
            }
        }

        public static async Task<ISymbol> GetSymbolAsync(this ImmutableDictionary<string, string> properties, string propertyName, Document document, CancellationToken cancellationToken)
        {
            string symbolKey;
            if (properties.TryGetValue(propertyName, out symbolKey))
            {
                var compilation = await document.Project.GetCompilationAsync(cancellationToken).ConfigureAwait(false);
                var resolution = SymbolKey.Resolve(symbolKey, compilation, cancellationToken: cancellationToken);
                return resolution.Symbol;
            }
            else
            {
                return null;
            }
        }

        public static ImmutableDictionary<string, string> WithEnumProperty<TEnum>(this ImmutableDictionary<string, string> properties, string propertyName, TEnum value)
            where TEnum : struct
        {
            if (!object.Equals(value, default(TEnum)))
            {
                return properties.Add(propertyName, value.ToString());
            }
            else if (properties.ContainsKey(propertyName))
            {
                return properties.Remove(propertyName);
            }
            else
            {
                return properties;
            }
        }

        public static TEnum GetEnumProperty<TEnum>(this ImmutableDictionary<string, string> properties, string propertyName)
            where TEnum : struct
        {
            string valueText;
            TEnum value;
            if (properties.TryGetValue(propertyName, out valueText) && System.Enum.TryParse<TEnum>(valueText, out value))
            {
                return value;
            }
            else
            {
                return default(TEnum);
            }
        }

        public static ImmutableDictionary<string, string> WithIntProperty(this ImmutableDictionary<string, string> properties, string propertyName, int value)
        {
            if (value != 0)
            {
                return properties.Add(propertyName, value.ToString());
            }
            else if (properties.ContainsKey(propertyName))
            {
                return properties.Remove(propertyName);
            }
            else
            {
                return properties;
            }
        }

        public static int GetIntProperty(this ImmutableDictionary<string, string> properties, string propertyName)
        {
            string valueText;
            int value;
            if (properties.TryGetValue(propertyName, out valueText) && int.TryParse(valueText, out value))
            {
                return value;
            }
            else
            {
                return 0;
            }
        }
    }
}