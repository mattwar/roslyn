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
    [CodeInjector(LanguageNames.CSharp, LanguageNames.VisualBasic)]
    public class ImmutableInjector : BaseCodeInjector
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
                    s.GetAttributes().Any(ad => ad.AttributeClass.Name.Equals("ImmutableAttribute")))
                {
                    var g = GetGenerator(ts.Language);

                    var cls = g.ClassDeclaration(ts.Name,
                        accessibility: ts.DeclaredAccessibility,
                        modifiers: DeclarationModifiers.Partial);

                    cls = AddImmutableMembers(g, cls, ts);

                    var code = EmbedInContainer(g, cls, ts.ContainingNamespace);
                    var cu = g.CompilationUnit(code).NormalizeWhitespace();

                    sc.AddCompilationUnit(cu);
                }
            });
        }

        public static SyntaxNode AddImmutableMembers(SyntaxGenerator g, SyntaxNode cls, ITypeSymbol ts)
        {
            // properties
            var props = ts.GetMembers().OfType<IPropertySymbol>().Where(p => p.IsReadOnly);

            cls = AddConstructor(g, cls, ts, props);
            cls = AddWithers(g, cls, ts, props);

            return cls;
        }

        private static SyntaxNode AddConstructor(SyntaxGenerator g, SyntaxNode cls, ITypeSymbol ts, IEnumerable<IPropertySymbol> props)
        {
            var cons = g.ConstructorDeclaration(ts.Name,
                accessibility: Accessibility.Public,
                parameters: props.Select(p => g.ParameterDeclaration(p.Name.ToLower(), g.TypeExpression(p.Type))),
                statements: props.Select(p => g.AssignmentStatement(g.MemberAccessExpression(g.ThisExpression(), g.IdentifierName(p.Name)), g.IdentifierName(p.Name.ToLower())))
                );

            cls = g.AddMembers(cls, cons);

            // add minimal constructor
            if (props.Any(p => GetPropertyInitializer(g, p) != null))
            {
                var minProps = props.Where(p => GetPropertyInitializer(g, p) == null);

                var minCons = g.ConstructorDeclaration(ts.Name,
                    accessibility: Accessibility.Public,
                    parameters: minProps.Select(p => g.ParameterDeclaration(p.Name.ToLower(), g.TypeExpression(p.Type))),
                    statements: minProps.Select(p => g.AssignmentStatement(g.MemberAccessExpression(g.ThisExpression(), g.IdentifierName(p.Name)), g.IdentifierName(p.Name.ToLower())))
                    );

                cls = g.AddMembers(cls, minCons);
            }

            return cls;
        }

        private static SyntaxNode GetPropertyInitializer(SyntaxGenerator g, IPropertySymbol p)
        {
            var decl = p.DeclaringSyntaxReferences.Select(r => g.GetDeclaration(r.GetSyntax())).FirstOrDefault();
            return decl != null ? g.GetExpression(decl) : null;
        }

        private static SyntaxNode AddWithers(SyntaxGenerator g, SyntaxNode cls, ITypeSymbol ts, IEnumerable<IPropertySymbol> props)
        {
            var withers = props.Select(p =>
                g.MethodDeclaration(
                    name: "With" + p.Name,
                    accessibility: Accessibility.Public,
                    parameters: new[] { g.ParameterDeclaration(p.Name.ToLower(), g.TypeExpression(p.Type)) },
                    returnType: g.TypeExpression(ts),
                    statements: new[] {
                        g.ReturnStatement(g.ObjectCreationExpression(g.TypeExpression(ts),
                            props.Select(p2 => p == p2
                                ? g.IdentifierName(p2.Name.ToLower())
                                : g.MemberAccessExpression(g.ThisExpression(), g.IdentifierName(p2.Name))))) }
                    )
            );

            return g.AddMembers(cls, withers);
        }
    }
}
