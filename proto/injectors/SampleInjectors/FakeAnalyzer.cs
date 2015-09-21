using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace SampleInjectors
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class FakeAnalyzer : DiagnosticAnalyzer
    {
        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get { return ImmutableArray<DiagnosticDescriptor>.Empty; } }

        public override void Initialize(AnalysisContext context)
        {
        }
    }
}
