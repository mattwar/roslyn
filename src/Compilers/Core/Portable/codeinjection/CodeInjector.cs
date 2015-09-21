﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.
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

        private readonly List<Action<CompilationStartInjectionContext>> _globalCompilationStartActions
            = new List<Action<CompilationStartInjectionContext>>();

        private readonly List<Action<SymbolInjectionContext>> _globalSymbolActions
            = new List<Action<SymbolInjectionContext>>();

        private readonly List<Action<SymbolInjectionContext>> _symbolActions
            = new List<Action<SymbolInjectionContext>>();

        private readonly List<Action<CompilationEndInjectionContext>> _globalCompilationEndActions
            = new List<Action<CompilationEndInjectionContext>>();

        private readonly List<Action<CompilationEndInjectionContext>> _compilationEndActions
            = new List<Action<CompilationEndInjectionContext>>();

        private string _generatedFileDirectoryPath;
        private Compilation _compilation;
        private CancellationToken _cancellationToken;
        private ISymbol _symbol;

        private readonly List<SyntaxTree> _addedTrees
           = new List<SyntaxTree>();

        public CodeInjectionProcessor()
        {
            _globalContext = new GlobalInjectionContext(this);
        }

        internal Compilation Compilation
        {
            get { return _compilation; }
        }

        internal ISymbol Symbol
        {
            get { return _symbol; }
        }

        internal CancellationToken CancellationToken
        {
            get { return _cancellationToken; }
        }

        public ImmutableArray<SyntaxTree> Generate(
            Compilation compilation,
            IEnumerable<CodeInjector> injectors,
            string generatedFileDirectoryPath = null,
            Action<Diagnostic> reportDiagnostic = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            _generatedFileDirectoryPath = generatedFileDirectoryPath;
            _compilation = compilation;
            _cancellationToken = cancellationToken;

            _addedTrees.Clear();
            _symbolActions.Clear();
            _compilationEndActions.Clear();

            var compilationContext = new CompilationStartInjectionContext(this);
            foreach (var compilationAction in _globalCompilationStartActions)
            {
                cancellationToken.ThrowIfCancellationRequested();
                DoInjectorAction(compilationAction, compilationContext, reportDiagnostic);
            }

            var symbols = new List<ITypeSymbol>();
            GatherTypeSymbols(compilation.GlobalNamespace, symbols);

            var symbolContext = new SymbolInjectionContext(this);
            foreach (var sym in symbols)
            {
                _symbol = sym;

                foreach (var action in _symbolActions)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    DoInjectorAction(action, symbolContext, reportDiagnostic);
                }

                foreach (var action in _globalSymbolActions)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    DoInjectorAction(action, symbolContext, reportDiagnostic);
                }
            }

            var endCompilationContext = new CompilationEndInjectionContext(this);
            foreach (var endCompilationAction in _compilationEndActions)
            {
                cancellationToken.ThrowIfCancellationRequested();
                DoInjectorAction(endCompilationAction, endCompilationContext, reportDiagnostic);
            }

            foreach (var endCompilationAction in _globalCompilationEndActions)
            {
                cancellationToken.ThrowIfCancellationRequested();
                DoInjectorAction(endCompilationAction, endCompilationContext, reportDiagnostic);
            }

            var result = _addedTrees.ToImmutableArray();

            _generatedFileDirectoryPath = null;
            _compilation = null;
            _cancellationToken = default(CancellationToken);
            _addedTrees.Clear();

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

        internal void AddCompilationStartAction(Action<CompilationStartInjectionContext> action)
        {
            _globalCompilationStartActions.Add(action);
        }

        internal void AddSymbolAction(Action<SymbolInjectionContext> action)
        {
            _symbolActions.Add(action);
        }

        internal void AddGlobalSymbolAction(Action<SymbolInjectionContext> action)
        {
            _globalSymbolActions.Add(action);
        }

        internal void AddCompilationEndAction(Action<CompilationEndInjectionContext> action)
        {
            _compilationEndActions.Add(action);
        }

        internal void AddGlobalCompilationEndAction(Action<CompilationEndInjectionContext> action)
        {
            _globalCompilationEndActions.Add(action);
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

            if (!string.IsNullOrWhiteSpace(_generatedFileDirectoryPath))
            {
                path = System.IO.Path.Combine(_generatedFileDirectoryPath, path);
            }

            // use existing tree to make a new tree instance.
            var existingTree = _compilation.SyntaxTrees.ElementAt(0);

            if (text.Encoding == null)
            {
                text = SourceText.From(text.ToString(), existingTree.Encoding ?? System.Text.Encoding.UTF8);
            }

            var newTree = existingTree.WithChangedText(text).WithFilePath(path);

            _addedTrees.Add(newTree);
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

        public void RegisterCompilationStartAction(Action<CompilationStartInjectionContext> action)
        {
            _processor.AddCompilationStartAction(ctx =>
            {
                ctx.Injector = this.Injector;
                action(ctx);
            });
        }

        public void RegisterCompilationEndAction(Action<CompilationEndInjectionContext> action)
        {
            _processor.AddGlobalCompilationEndAction(ctx =>
            {
                ctx.Injector = this.Injector;
                action(ctx);
            });
        }

        public void RegisterSymbolAction(Action<SymbolInjectionContext> action)
        {
            _processor.AddGlobalSymbolAction(ctx =>
            {
                ctx.Injector = this.Injector;
                action(ctx);
            });
        }
    }

    public class CompilationStartInjectionContext
    {
        private CodeInjectionProcessor _processor;

        public Compilation Compilation { get { return _processor.Compilation; } }
        public CancellationToken CancellationToken { get { return _processor.CancellationToken; } }

        internal CodeInjector Injector { get; set; }

        internal CompilationStartInjectionContext(CodeInjectionProcessor processor)
        {
            _processor = processor;
        }

        public void RegisterSymbolAction(Action<SymbolInjectionContext> action)
        {
            _processor.AddSymbolAction(ctx =>
            {
                ctx.Injector = this.Injector;
                action(ctx);
            });
        }

        public void RegisterCompilationEndAction(Action<CompilationEndInjectionContext> action)
        {
            _processor.AddCompilationEndAction(ctx =>
            {
                ctx.Injector = this.Injector;
                action(ctx);
            });
        }
    }

    public class CompilationEndInjectionContext
    {
        private CodeInjectionProcessor _processor;

        public Compilation Compilation { get { return _processor.Compilation; } }
        public CancellationToken CancellationToken { get { return _processor.CancellationToken; } }

        internal CodeInjector Injector { get; set; }

        internal CompilationEndInjectionContext(CodeInjectionProcessor processor)
        {
            _processor = processor;
        }

        public void AddCompilationUnit(string name, SyntaxNode root)
        {
            _processor.AddCompilationUnit(this.Injector, name, root);
        }

        public void AddCompilationUnit(string name, string text)
        {
            _processor.AddCompilationUnit(this.Injector, name, text);
        }

        public void AddCompilationUnit(string name, SourceText text)
        {
            _processor.AddCompilationUnit(this.Injector, name, text);
        }
    }

    public class SymbolInjectionContext
    {
        private CodeInjectionProcessor _processor;

        public Compilation Compilation { get { return _processor.Compilation; } }
        public CancellationToken CancellationToken { get { return _processor.CancellationToken; } }
        public ISymbol Symbol { get { return _processor.Symbol; } }

        internal CodeInjector Injector { get; set; }

        internal SymbolInjectionContext(CodeInjectionProcessor processor)
        {
            _processor = processor;
        }

        public void AddCompilationUnit(SyntaxNode root)
        {
            _processor.AddCompilationUnit(this.Injector, this.Symbol.Name, root);
        }

        public void AddCompilationUnit(string text)
        {
            _processor.AddCompilationUnit(this.Injector, this.Symbol.Name, text);
        }

        public void AddCompilationUnit(SourceText text)
        {
            _processor.AddCompilationUnit(this.Injector, this.Symbol.Name, text);
        }
    }
}
