using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CodeInjection;
using Microsoft.CodeAnalysis.Editing;

namespace SampleInjectors
{
    public abstract class BaseCodeInjector : CodeInjector
    {
        private static AdhocWorkspace ws = new AdhocWorkspace();

        public SyntaxGenerator GetGenerator(string languageName)
        {
            return SyntaxGenerator.GetGenerator(ws, LanguageNames.CSharp);
        }

        public SyntaxNode EmbedInContainer(SyntaxGenerator g, SyntaxNode declaration, INamespaceSymbol container)
        {
            var code = declaration;

            var ns = GetFullNamespace(container);
            if (!string.IsNullOrEmpty(ns))
            {
                code = g.NamespaceDeclaration(g.DottedName(ns), code);
            }

            // TODO: add support for nested types

            return code;
        }

        public static string GetFullNamespace(INamespaceSymbol symbol)
        {
            if (symbol == null || symbol.Kind != SymbolKind.Namespace || ((INamespaceSymbol)symbol).IsGlobalNamespace)
            {
                return "";
            }

            var containing = GetFullNamespace(symbol.ContainingNamespace);
            if (string.IsNullOrEmpty(containing))
            {
                return symbol.Name;
            }
            else
            {
                return containing = "." + symbol.Name;
            }
        }
    }
}
