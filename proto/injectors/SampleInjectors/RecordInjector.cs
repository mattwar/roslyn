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
    [CodeInjector(LanguageNames.CSharp)]
    public class RecordInjector : BaseCodeInjector
    {
        public override void Initialize(GlobalInjectionContext context)
        {
            context.RegisterSymbolAction(sc =>
            {
                var s = sc.Symbol;

                var ts = s as INamedTypeSymbol;
                if (ts != null &&
                    ts.TypeKind == TypeKind.Class &&
                    !ts.IsAbstract &&
                    s.GetAttributes().Any(ad => ad.AttributeClass.Name.Equals("RecordAttribute")))
                {
                    var g = GetGenerator(ts.Language);

                    var cls = g.ClassDeclaration(ts.Name,
                        accessibility: ts.DeclaredAccessibility,
                        modifiers: DeclarationModifiers.Partial);

                    cls = ImmutableInjector.AddImmutableMembers(g, cls, ts);
                    cls = ValueInjector.AddValueMembers(sc, g, cls, ts);

                    var code = EmbedInContainer(g, cls, ts.ContainingNamespace);
                    var cu = g.CompilationUnit(code).NormalizeWhitespace();

                    sc.AddCompilationUnit(cu);
                }
            });
        }
    }
}
