// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.CodeAnalysis.CodeInjection
{
    public abstract class CodeInjector
    {
        public abstract void Initialize(GlobalInjectionContext context);
    }

    public class CodeInjectionProcessor
    {
        private readonly GlobalInjectionContext _globalContext = new GlobalInjectionContext();

        public ImmutableArray<SyntaxTree> Generate(
            Compilation compilation,
            IEnumerable<CodeInjector> injectors,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            var compilationContext = new CompilationInjectionContext();
            foreach (var compilationAction in _globalContext.Actions)
            {
                compilationAction(compilationContext);
            }

            var symbols = new List<ITypeSymbol>();
            GatherTypeSymbols(compilation.GlobalNamespace, symbols);

            var addedTrees = new List<SyntaxTree>();
            foreach (var sym in symbols)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var symbolContext = new SymbolInjectionContext(compilation, sym, cancellationToken);

                foreach (var action in compilationContext.Actions)
                {
                    action(symbolContext);
                }

                addedTrees.AddRange(symbolContext.AddedTrees);
            }

            return addedTrees.ToImmutableArray();
        }

        private static void GatherTypeSymbols(INamespaceOrTypeSymbol symbol, List<ITypeSymbol> types)
        {
            var ts = symbol as ITypeSymbol;
            if (ts != null && !ts.IsImplicitlyDeclared)
            {
                types.Add(ts);

                foreach (var nestedType in ts.GetTypeMembers())
                {
                    GatherTypeSymbols(nestedType, types);
                }
            }

            var ns = symbol as INamespaceSymbol;
            if (ns != null)
            {
                foreach (var ms in ns.GetMembers())
                {
                    GatherTypeSymbols(ms, types);
                }
            }
        }

        private ConditionalWeakTable<Type, CodeInjector> _injectors
            = new ConditionalWeakTable<Type, CodeInjector>();

        public CodeInjector GetInjector(Type injectorType)
        {
            CodeInjector injector;
            if (!_injectors.TryGetValue(injectorType, out injector))
            {
                injector = _injectors.GetValue(injectorType, _ij => CreateInjector(_ij));
            }

            return injector;
        }

        private CodeInjector CreateInjector(Type injectorType)
        {
            var injector = (CodeInjector)Activator.CreateInstance(injectorType);
            _globalContext.Injector = injector;
            injector.Initialize(_globalContext);
            return injector;
        }

        private class AssemblyInjectors
        {
            public ImmutableDictionary<string, Lazy<ImmutableArray<CodeInjector>>> Injectors { get; }

            public AssemblyInjectors(CodeInjectionProcessor processor, Assembly assembly)
            {
                Injectors =
                    (from ti in assembly.DefinedTypes
                     let attr = ti.GetCustomAttribute<CodeInjectorAttribute>()
                     where attr != null
                     from language in attr.Languages
                     group ti.AsType() by language into g
                     select new KeyValuePair<string, Lazy<ImmutableArray<CodeInjector>>>(
                         g.Key,
                         new Lazy<ImmutableArray<CodeInjector>>(
                             () => g.Select(type => processor.GetInjector(type)).ToImmutableArray(), isThreadSafe: true)))
                    .ToImmutableDictionary();
            }
        }

        private ConditionalWeakTable<Assembly, AssemblyInjectors> assemblyInjectors
            = new ConditionalWeakTable<Assembly, AssemblyInjectors>();

        private AssemblyInjectors GetInjectors(Assembly assembly)
        {
            AssemblyInjectors injectors;
            if (!assemblyInjectors.TryGetValue(assembly, out injectors))
            {
                injectors = assemblyInjectors.GetValue(assembly, _assembly => new AssemblyInjectors(this, _assembly));
            }

            return injectors;
        }

        private ImmutableArray<CodeInjector> GetInjectors(Assembly assembly, string language)
        {
            var ai = GetInjectors(assembly);

            Lazy<ImmutableArray<CodeInjector>> lazyInjectors;
            if (ai.Injectors.TryGetValue(language, out lazyInjectors))
            {
                return lazyInjectors.Value;
            }

            return ImmutableArray<CodeInjector>.Empty;
        }

        public ImmutableArray<CodeInjector> GetInjectors(IEnumerable<Assembly> injectionAssemblies, string language)
        {
            List<CodeInjector> list = null;

            foreach (var assembly in injectionAssemblies)
            {
                foreach (var injector in GetInjectors(assembly, language))
                {
                    if (list == null)
                    {
                        list = new List<CodeInjector>();
                    }

                    list.Add(injector);
                }
            }

            if (list.Count > 0)
            {
                return list.ToImmutableArray();
            }
            else
            {
                return ImmutableArray<CodeInjector>.Empty;
            }
        }
    }

    public class GlobalInjectionContext
    {
        private readonly List<Action<CompilationInjectionContext>> _actions
            = new List<Action<CompilationInjectionContext>>();

        internal CodeInjector Injector { get; set; }

        public GlobalInjectionContext()
        {
        }

        public void RegisterCompilationAction(Action<CompilationInjectionContext> action)
        {
            _actions.Add(ctx =>
            {
                ctx.Injector = Injector;
                action(ctx);
            });
        }

        internal IReadOnlyList<Action<CompilationInjectionContext>> Actions
        {
            get { return _actions; }
        }
    }

    public class CompilationInjectionContext
    {
        public Compilation Compilation { get; }

        internal CodeInjector Injector { get; set; }

        private readonly List<Action<SymbolInjectionContext>> _actions
            = new List<Action<SymbolInjectionContext>>();

        public void RegisterSymbolAction(Action<SymbolInjectionContext> action)
        {
            _actions.Add(ctx =>
            {
                ctx.Injector = this.Injector;
                action(ctx);
            });
        }

        internal IReadOnlyList<Action<SymbolInjectionContext>> Actions
        {
            get { return _actions; }
        }
    }

    public class SymbolInjectionContext
    {
        public Compilation Compilation { get; }
        public ISymbol Symbol { get; }
        public CancellationToken CancellationToken { get; }

        internal CodeInjector Injector { get; set; }

        internal readonly List<SyntaxTree> AddedTrees
           = new List<SyntaxTree>();

        internal SymbolInjectionContext(
            Compilation compilation, 
            ISymbol symbol, 
            CancellationToken cancellationToken)
        {
            this.Compilation = compilation;
            this.Symbol = symbol;
            this.CancellationToken = cancellationToken;
            this.Injector = null;
        }

        public void AddCompilationUnit(SyntaxNode root)
        {
            string path = "$" + Symbol.Name + "_" + this.Injector.GetType().Name;
            root = root.NormalizeWhitespace();

            var symbolDeclTree = this.Symbol.DeclaringSyntaxReferences.First().SyntaxTree;
            var tree = symbolDeclTree.WithRootAndOptions(root, symbolDeclTree.Options).WithFilePath(path);

            this.AddedTrees.Add(tree);
        }
    }
}
