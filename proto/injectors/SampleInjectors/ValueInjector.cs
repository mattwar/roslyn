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
    public class ValueInjector : BaseCodeInjector
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
                    s.GetAttributes().Any(ad => ad.AttributeClass.Name.Equals("ValueAttribute")))
                {
                    var g = GetGenerator(ts.Language);

                    var cls = g.ClassDeclaration(ts.Name,
                        accessibility: ts.DeclaredAccessibility,
                        modifiers: DeclarationModifiers.Partial);
                        
                    cls = AddValueMembers(sc, g, cls, ts);

                    var code = EmbedInContainer(g, cls, ts.ContainingNamespace);
                    var cu = g.CompilationUnit(code).NormalizeWhitespace();

                    sc.AddCompilationUnit(cu);
                }
            });
        }

        public static SyntaxNode AddValueMembers(SymbolInjectionContext sc, SyntaxGenerator g, SyntaxNode cls, ITypeSymbol ts)
        {
            // properties
            var props = ts.GetMembers().OfType<IPropertySymbol>().Where(p => p.IsReadOnly);

            return AddValueEquals(sc, g, cls, ts, props);
        }

        private static SyntaxNode AddValueEquals(SymbolInjectionContext sc, SyntaxGenerator g, SyntaxNode cls, ITypeSymbol ts, IEnumerable<IPropertySymbol> props)
        {
            var iequatable = sc.Compilation.GetTypeByMetadataName("System.IEquatable`1");
            var iequatableT = iequatable.Construct(ts);
            var ieq = g.TypeExpression(iequatableT);

            var eqc = sc.Compilation.GetTypeByMetadataName("System.Collections.Generic.EqualityComparer`1");

            // make class implement IEquatable<T>
            cls = g.AddInterfaceType(cls, ieq);

            // Add IEquatable<T>.Equals
            // compare each field using EqualityComparer<T>.Default.Equals(x,y)
            SyntaxNode expr = null;
            foreach (var prop in props)
            {
                var eqcT = eqc.Construct(prop.Type);
                var eqcTName = g.TypeExpression(eqcT);

                var propName = g.IdentifierName(prop.Name);
                var compare = g.InvocationExpression(
                    g.MemberAccessExpression(g.QualifiedName(eqcTName, g.IdentifierName("Default")), "Equals"),
                    new[] { g.MemberAccessExpression(g.ThisExpression(), propName), g.MemberAccessExpression(g.IdentifierName("other"), propName) });
                expr = expr == null ? compare : g.LogicalAndExpression(expr, compare);
            }

            var method = g.MethodDeclaration(
                "Equals",
                parameters: new[] { g.ParameterDeclaration("other", g.TypeExpression(ts)) },
                returnType: g.TypeExpression(SpecialType.System_Boolean),
                accessibility: Accessibility.Public,
                statements: new[] { expr != null ? g.ReturnStatement(expr) : g.ReturnStatement(g.TrueLiteralExpression()) });

            method = g.AsPublicInterfaceImplementation(method, ieq);
            cls = g.AddMembers(cls, method);

            // override object.Equals
            var objEquals = g.MethodDeclaration(
                "Equals",
                parameters: new[] { g.ParameterDeclaration("other", g.TypeExpression(SpecialType.System_Object)) },
                accessibility: Accessibility.Public,
                modifiers: DeclarationModifiers.Override,
                returnType: g.TypeExpression(SpecialType.System_Boolean),
                statements: new[]
                {
                    g.LocalDeclarationStatement("typed", g.TryCastExpression(g.IdentifierName("other"), g.TypeExpression(ts))),
                    g.IfStatement(
                        condition: g.ReferenceNotEqualsExpression(g.IdentifierName("typed"), g.NullLiteralExpression()),
                        trueStatements: new [] { g.ReturnStatement(g.InvocationExpression(g.MemberAccessExpression(g.ThisExpression(), g.IdentifierName("Equals")), new[] { g.IdentifierName("typed") })) },
                        falseStatement: g.ReturnStatement(g.FalseLiteralExpression()))
                });

            cls = g.AddMembers(cls, objEquals);

            // override object.GetHashCode
            // use EqualityComparer<T>.Default.GetHashCode for each member
            SyntaxNode totalHashExpr = null;
            foreach (var prop in props)
            {
                var eqcT = eqc.Construct(prop.Type);
                var eqcTName = g.TypeExpression(eqcT);

                var hashExpr = g.InvocationExpression(
                    g.MemberAccessExpression(g.QualifiedName(eqcTName, g.IdentifierName("Default")), "GetHashCode"),
                    g.MemberAccessExpression(g.ThisExpression(), prop.Name));

                totalHashExpr = totalHashExpr == null ? hashExpr : g.AddExpression(totalHashExpr, hashExpr);
            }

            var getHashCode = g.MethodDeclaration(
                "GetHashCode",
                accessibility: Accessibility.Public,
                modifiers: DeclarationModifiers.Override,
                returnType: g.TypeExpression(SpecialType.System_Int32),
                statements: new[] { totalHashExpr != null ? g.ReturnStatement(totalHashExpr) : g.ReturnStatement(g.LiteralExpression(0)) }
                );

            cls = g.AddMembers(cls, getHashCode);

            return cls;
        }
    }
}
