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
    public class ProxyInjector : BaseCodeInjector
    {
        public override void Initialize(GlobalInjectionContext context)
        {
            context.RegisterSymbolAction(sc =>
            {
                var s = sc.Symbol;

                var ts = s as INamedTypeSymbol;
                if (ts != null && ts.TypeKind == TypeKind.Interface && s.GetAttributes().Any(ad => ad.AttributeClass.Name.Equals("ProxyAttribute")))
                {
                    var g = GetGenerator(ts.Language);
                    var itype = g.TypeExpression(ts);

                    // houston we have a proxy interface!
                    var impl = g.ClassDeclaration("Proxy_" + s.Name, interfaceTypes: new[] { itype });

                    // now implement all abstract properties...
                    foreach (var prop in ts.GetMembers().OfType<IPropertySymbol>().Where(p => p.IsAbstract))
                    {
                        var mods = prop.IsReadOnly ? DeclarationModifiers.ReadOnly : prop.IsWriteOnly ? DeclarationModifiers.WriteOnly : DeclarationModifiers.None;

                        var decl = g.PropertyDeclaration(
                            prop.Name,
                            g.TypeExpression(prop.Type),
                            Accessibility.Public,
                            mods,
                            !mods.IsWriteOnly ? new SyntaxNode[] { g.ReturnStatement(g.DefaultExpression(prop.Type)) } : null,
                            !mods.IsReadOnly ? new SyntaxNode[] { } : null);

                        decl = g.AsPublicInterfaceImplementation(decl, itype);
                        impl = g.AddMembers(impl, decl);
                    }

                    var cu = g.CompilationUnit(impl).NormalizeWhitespace();

                    sc.AddCompilationUnit(cu);
                }
            });
        }
    }
}
