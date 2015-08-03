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
        public abstract void Initialize(InjectionContext context);
    }

    public class CodeInjectionProcessor
    {
        public static ImmutableArray<SyntaxTree> Generate(
            Compilation compilation,
            IEnumerable<CodeInjector> injectors,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            // factor this so it occurs only once per injector
            var context = new InjectionContext();
            foreach (CodeInjector injector in injectors)
            {
                context.Injector = injector;
                injector.Initialize(context);
            }

            var compilationContext = new CompilationInjectionContext();
            foreach (var compilationAction in context.Actions)
            {
                compilationAction(compilationContext);
            }

            // for now just consider types in global namespace
            var symbols = compilation.GlobalNamespace.GetTypeMembers();

            var addedTrees = new List<SyntaxTree>();
            foreach (var sym in symbols)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var symbolContext = new SymbolInjectionContext(compilation, sym, cancellationToken);

                foreach (var action in context.Actions)
                {
                    action(compilationContext);
                }

                addedTrees.AddRange(symbolContext.AddedTrees);
            }

            return addedTrees.ToImmutableArray();
        }

        private class AssemblyInjectors
        {
            public ImmutableDictionary<string, Lazy<ImmutableArray<CodeInjector>>> Injectors { get; }

            public AssemblyInjectors(Assembly assembly)
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
                             () => g.Select(type => (CodeInjector)Activator.CreateInstance(type)).ToImmutableArray(), isThreadSafe: true)))
                    .ToImmutableDictionary();
            }
        }

        private static ConditionalWeakTable<Assembly, AssemblyInjectors> s_assemblyInjectors
            = new ConditionalWeakTable<Assembly, AssemblyInjectors>();

        private static AssemblyInjectors GetInjectors(Assembly assembly)
        {
            AssemblyInjectors injectors;
            if (!s_assemblyInjectors.TryGetValue(assembly, out injectors))
            {
                injectors = s_assemblyInjectors.GetValue(assembly, _assembly => new AssemblyInjectors(_assembly));
            }

            return injectors;
        }

        private static ImmutableArray<CodeInjector> GetInjectors(Assembly assembly, string language)
        {
            var ai = GetInjectors(assembly);

            Lazy<ImmutableArray<CodeInjector>> lazyInjectors;
            if (ai.Injectors.TryGetValue(language, out lazyInjectors))
            {
                return lazyInjectors.Value;
            }

            return ImmutableArray<CodeInjector>.Empty;
        }

        public static ImmutableArray<CodeInjector> GetInjectors(IEnumerable<Assembly> injectionAssemblies, string language)
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

    public class InjectionContext
    {
        private readonly List<Action<CompilationInjectionContext>> _actions
            = new List<Action<CompilationInjectionContext>>();

        internal CodeInjector Injector { get; set; }

        internal InjectionContext()
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
            string path = Symbol.Name + "_" + this.Injector.GetType().Name;
            root = root.NormalizeWhitespace();

            var symbolDeclTree = this.Symbol.DeclaringSyntaxReferences.First().SyntaxTree;
            var tree = symbolDeclTree.WithRootAndOptions(root, symbolDeclTree.Options).WithFilePath(path);

            this.AddedTrees.Add(tree);
        }
    }
}
