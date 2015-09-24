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
using Microsoft.CodeAnalysis.Text;

namespace Microsoft.CodeAnalysis.CodeInjection
{
    /// <summary>
    /// CodeInjectors generated source code at runtime based on analysis of and existing compilation.
    /// The generated source code is compiled into the compilation.
    /// </summary>
    public abstract class CodeInjector
    {
        public abstract void Initialize(GlobalInjectionContext context);
    }

    /// <summary>
    /// Place this attribute onto a type to cause it to be considered a <see cref="CodeInjector"/>.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class CodeInjectorAttribute : Attribute
    {
        /// <summary>
        /// The source languages to which this <see cref="CodeInjector"/> applies.  See <see cref="LanguageNames"/>.
        /// </summary>
        public string[] Languages { get; }

        /// <summary>
        /// Attribute constructor used to specify automatic application of a <see cref="CodeInjector"/>.
        /// </summary>
        /// <param name="firstLanguage">One language to which the <see cref="CodeInjector"/> applies.</param>
        /// <param name="additionalLanguages">Additional languages to which the <see cref="CodeInjector"/> applies. See <see cref="LanguageNames"/>.</param>
        public CodeInjectorAttribute(string firstLanguage, params string[] additionalLanguages)
        {
            if (firstLanguage == null)
            {
                throw new ArgumentNullException(nameof(firstLanguage));
            }

            if (additionalLanguages == null)
            {
                throw new ArgumentNullException(nameof(additionalLanguages));
            }

            var languages = new string[additionalLanguages.Length + 1];
            languages[0] = firstLanguage;

            if (additionalLanguages.Length > 0)
            {
                Array.Copy(additionalLanguages, 0, languages, 1, additionalLanguages.Length);
            }

            this.Languages = languages;
        }
    }

    public class CodeInjectionProcessor
    {
        private readonly GlobalInjectionContext _globalContext;

        private ConditionalWeakTable<Type, CodeInjector> _injectors
            = new ConditionalWeakTable<Type, CodeInjector>();

        public CodeInjectionProcessor()
        {
            _globalContext = new GlobalInjectionContext(this);
        }

        public ImmutableArray<SyntaxTree> Generate(
            Compilation compilation,
            IEnumerable<CodeInjector> injectors,
            string generatedFileDirectoryPath = null,
            Action<Diagnostic> reportDiagnostic = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            var compilationStartContext = new CompilationStartInjectionContext(this, generatedFileDirectoryPath, compilation, cancellationToken);
            foreach (var compilationAction in _globalContext.CompilationStartActions)
            {
                cancellationToken.ThrowIfCancellationRequested();
                DoInjectorAction(compilationAction, compilationStartContext, reportDiagnostic);
            }

            var symbols = new List<ITypeSymbol>();
            GatherTypeSymbols(compilation.Assembly.GlobalNamespace, symbols);

            var symbolContext = new SymbolInjectionContext(compilationStartContext);
            foreach (var sym in symbols)
            {
                symbolContext.Symbol = sym;

                foreach (var action in compilationStartContext.SymbolActions)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    DoInjectorAction(action, symbolContext, reportDiagnostic);
                }

                foreach (var action in _globalContext.SymbolActions)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    DoInjectorAction(action, symbolContext, reportDiagnostic);
                }
            }

            var endCompilationContext = new CompilationEndInjectionContext(compilationStartContext);
            foreach (var endCompilationAction in compilationStartContext.CompilationEndActions)
            {
                cancellationToken.ThrowIfCancellationRequested();
                DoInjectorAction(endCompilationAction, endCompilationContext, reportDiagnostic);
            }

            foreach (var endCompilationAction in _globalContext.CompilationEndActions)
            {
                cancellationToken.ThrowIfCancellationRequested();
                DoInjectorAction(endCompilationAction, endCompilationContext, reportDiagnostic);
            }

            var result = compilationStartContext.AddedTrees.ToImmutableArray();

            return result;
        }

        private void DoInjectorAction<TArg>(Action<TArg> action, TArg arg, Action<Diagnostic> reportDiagnotic)
        {
            try
            {
                action(arg);
            }
            catch (Exception e)
            {
                if (reportDiagnotic != null)
                {
                    var dx = Diagnostics.AnalyzerExecutor.CreateDriverExceptionDiagnostic(e);
                    reportDiagnotic(dx);
                }
            }
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

        public CodeInjector GetInjector(Type injectorType)
        {
            CodeInjector injector;
            if (!_injectors.TryGetValue(injectorType, out injector))
            {
                injector = _injectors.GetValue(injectorType, _ij => CreateInjector(_ij));
            }

            return injector;
        }

        private object gate = new object();

        private CodeInjector CreateInjector(Type injectorType)
        {
            var injector = (CodeInjector)Activator.CreateInstance(injectorType);

            // use lock to keep injectors from initializing in parallel
            lock (gate)
            {
                _globalContext.Injector = injector;
                injector.Initialize(_globalContext);
            }

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

            if (list != null)
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
        private CodeInjectionProcessor _processor;

        internal CodeInjector Injector { get; set; }

        internal GlobalInjectionContext(CodeInjectionProcessor processor)
        {
            _processor = processor;
        }

        internal readonly List<Action<CompilationStartInjectionContext>> CompilationStartActions
            = new List<Action<CompilationStartInjectionContext>>();

        internal readonly List<Action<SymbolInjectionContext>> SymbolActions
            = new List<Action<SymbolInjectionContext>>();

        internal readonly List<Action<CompilationEndInjectionContext>> CompilationEndActions
            = new List<Action<CompilationEndInjectionContext>>();

        public void RegisterCompilationStartAction(Action<CompilationStartInjectionContext> action)
        {
            var injector = this.Injector;
            this.CompilationStartActions.Add(ctx =>
            {
                ctx.Injector = injector;
                action(ctx);
            });
        }

        public void RegisterCompilationEndAction(Action<CompilationEndInjectionContext> action)
        {
            var injector = this.Injector;
            this.CompilationEndActions.Add(ctx =>
            {
                ctx.Injector = injector;
                action(ctx);
            });
        }

        public void RegisterSymbolAction(Action<SymbolInjectionContext> action)
        {
            var injector = this.Injector;
            this.SymbolActions.Add(ctx =>
            {
                ctx.Injector = injector;
                action(ctx);
            });
        }
    }

    public class CompilationStartInjectionContext
    {
        internal readonly CodeInjectionProcessor Processor;
        internal readonly string GeneratedCodeDirectory;

        public Compilation Compilation { get; private set; }
        public CancellationToken CancellationToken { get; private set; }

        internal CodeInjector Injector { get; set; }

        internal readonly List<Action<SymbolInjectionContext>> SymbolActions
            = new List<Action<SymbolInjectionContext>>();

        internal readonly List<Action<CompilationEndInjectionContext>> CompilationEndActions
            = new List<Action<CompilationEndInjectionContext>>();

        internal List<SyntaxTree> AddedTrees = new List<SyntaxTree>();

        internal CompilationStartInjectionContext(CodeInjectionProcessor processor, string generateCodeDirectory, Compilation compilation, CancellationToken cancellationToken)
        {
            this.Processor = processor;
            this.GeneratedCodeDirectory = generateCodeDirectory;
            this.Compilation = compilation;
            this.CancellationToken = cancellationToken;
        }

        public void RegisterSymbolAction(Action<SymbolInjectionContext> action)
        {
            var injector = this.Injector;
            this.SymbolActions.Add(ctx =>
            {
                ctx.Injector = injector;
                action(ctx);
            });
        }

        public void RegisterCompilationEndAction(Action<CompilationEndInjectionContext> action)
        {
            var injector = this.Injector;
            this.CompilationEndActions.Add(ctx =>
            {
                ctx.Injector = injector;
                action(ctx);
            });
        }

        internal void AddCompilationUnit(CodeInjector injector, string name, SyntaxNode root)
        {
            root = root.NormalizeWhitespace(eol: Environment.NewLine);
            AddCompilationUnit(injector, name, root.ToFullString());
        }

        internal void AddCompilationUnit(CodeInjector injector, string name, string text)
        {
            AddCompilationUnit(injector, name, SourceText.From(text));
        }

        internal void AddCompilationUnit(CodeInjector injector, string name, SourceText text)
        {
            var path = $"${injector.GetType().Name}_{name}.cs";

            if (!string.IsNullOrWhiteSpace(this.GeneratedCodeDirectory))
            {
                path = System.IO.Path.Combine(this.GeneratedCodeDirectory, path);
            }

            // use existing tree to make a new tree instance.
            var existingTree = this.Compilation.SyntaxTrees.ElementAt(0);

            if (text.Encoding == null)
            {
                text = SourceText.From(text.ToString(), existingTree.Encoding ?? System.Text.Encoding.UTF8);
            }

            var newTree = existingTree.WithChangedText(text).WithFilePath(path);

            this.AddedTrees.Add(newTree);
        }
    }

    public class CompilationEndInjectionContext
    {
        private CompilationStartInjectionContext _startContext;

        public Compilation Compilation { get { return _startContext.Compilation; } }
        public CancellationToken CancellationToken { get { return _startContext.CancellationToken; } }

        internal CodeInjector Injector { get; set; }

        internal CompilationEndInjectionContext(CompilationStartInjectionContext startContext)
        {
            _startContext = startContext;
        }

        public void AddCompilationUnit(string name, SyntaxNode root)
        {
            _startContext.AddCompilationUnit(this.Injector, name, root);
        }

        public void AddCompilationUnit(string name, string text)
        {
            _startContext.AddCompilationUnit(this.Injector, name, text);
        }

        public void AddCompilationUnit(string name, SourceText text)
        {
            _startContext.AddCompilationUnit(this.Injector, name, text);
        }
    }

    public class SymbolInjectionContext
    {
        private CompilationStartInjectionContext _startContext;

        public Compilation Compilation { get { return _startContext.Compilation; } }
        public CancellationToken CancellationToken { get { return _startContext.CancellationToken; } }

        public ISymbol Symbol { get; internal set; }
        internal CodeInjector Injector { get; set; }

        internal SymbolInjectionContext(CompilationStartInjectionContext startContext)
        {
            _startContext = startContext;
        }

        public void AddCompilationUnit(SyntaxNode root)
        {
            _startContext.AddCompilationUnit(this.Injector, this.Symbol.Name, root);
        }

        public void AddCompilationUnit(string text)
        {
            _startContext.AddCompilationUnit(this.Injector, this.Symbol.Name, text);
        }

        public void AddCompilationUnit(SourceText text)
        {
            _startContext.AddCompilationUnit(this.Injector, this.Symbol.Name, text);
        }
    }
}
