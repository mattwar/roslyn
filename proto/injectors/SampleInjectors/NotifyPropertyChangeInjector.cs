using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CodeInjection;
using Microsoft.CodeAnalysis.Editing;
using System.ComponentModel;

namespace SampleInjectors
{
    [CodeInjector(LanguageNames.CSharp)]
    public class NotifyPropertyChangedInjector : BaseCodeInjector
    {
        public override void Initialize(GlobalInjectionContext context)
        {
            context.RegisterCompilationStartAction(cc =>
            {
                var inpcType = cc.Compilation.GetTypeByMetadataName("System.ComponentModel.INotifyPropertyChanged");
                var eventHandlerType = cc.Compilation.GetTypeByMetadataName("System.ComponentModel.PropertyChangedEventHandler");
                var eventArgsType = cc.Compilation.GetTypeByMetadataName("System.ComponentModel.PropertyChangedEventArgs");

                if (inpcType != null && eventHandlerType != null && eventArgsType != null)
                {
                    cc.RegisterSymbolAction(sc =>
                    {
                        var s = sc.Symbol;

                        var ts = s as INamedTypeSymbol;
                        if (ts != null &&
                            ts.TypeKind == TypeKind.Class &&
                            !ts.IsAbstract &&
                            s.GetAttributes().Any(ad => ad.AttributeClass.Name.Equals("NPCAttribute")))
                        {
                            var g = GetGenerator(ts.Language);

                            var cls = g.ClassDeclaration(ts.Name,
                                accessibility: ts.DeclaredAccessibility,
                                modifiers: DeclarationModifiers.Partial);

                            cls = AddNotifyPropertyChanged(sc, g, cls, ts, inpcType, eventHandlerType, eventArgsType);

                            var code = EmbedInContainer(g, cls, ts.ContainingNamespace);
                            var cu = g.CompilationUnit(code).NormalizeWhitespace();

                            sc.AddCompilationUnit(cu);
                        }
                    });
                }
            });
        }

        public static SyntaxNode AddNotifyPropertyChanged(
            SymbolInjectionContext ctx, SyntaxGenerator g, SyntaxNode cls, ITypeSymbol ts,
            ITypeSymbol inpcType, ITypeSymbol eventHandlerType, ITypeSymbol eventArgsType)
        {
            // properties
            var props = ts.GetMembers().OfType<IPropertySymbol>().Where(p => !p.IsAbstract);

            // add declaration of INotifyPropertyChanged if its not already defined
            if (!ts.AllInterfaces.Any(t => t.Equals(inpcType)))
            {
                cls = g.AddInterfaceType(cls, g.TypeExpression(inpcType));
                cls = g.AddMembers(cls, g.EventDeclaration("PropertyChanged", g.TypeExpression(eventHandlerType), Accessibility.Public));
            }

            // supersede all properties with ones that raise the PropertyChanged event.
            foreach (var prop in props)
            {
                SyntaxNode[] getterStatements = prop.IsWriteOnly ? null : new SyntaxNode[]
                {
                    g.ReturnStatement(g.IdentifierName("superseded"))
                };

                SyntaxNode[] setterStatements = prop.IsReadOnly ? null : new SyntaxNode[]
                {
                    g.IfStatement(
                        condition: g.ValueNotEqualsExpression(g.IdentifierName("superseded"), g.IdentifierName("value")),
                        trueStatements: new SyntaxNode[]
                        {
                            g.AssignmentStatement(g.IdentifierName("superseded"), g.IdentifierName("value")),
                            g.RaiseEventStatement(g.IdentifierName("PropertyChanged"), g.ThisExpression(), g.ObjectCreationExpression(eventArgsType, g.LiteralExpression(prop.Name)))
                        })
                };

                cls = g.AddMembers(
                    cls,
                    g.PropertyDeclaration(
                        name: prop.Name, 
                        type: g.TypeExpression(prop.Type), 
                        accessibility: prop.DeclaredAccessibility, 
                        modifiers: DeclarationModifiers.From(prop) | DeclarationModifiers.Supersede,
                        getAccessorStatements: getterStatements,
                        setAccessorStatements: setterStatements));
            }

            return cls;
        }
    }
}
