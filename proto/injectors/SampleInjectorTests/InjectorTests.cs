using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CodeInjection;

namespace Test
{
    public class InjectorTests
    {
        public void TestRecordInjector()
        {
            var code = @"
[Record]
public partial class R
{
   public int X { get; } = 5;
   public string Y { get; }
}

public class RecordAttribute : System.Attribute { }
";

            var trees = GetInjectedTrees<SampleInjectors.RecordInjector>(code);

            var comp = GetInjectedCompilation<SampleInjectors.RecordInjector>(code);
            var dx = comp.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
            Debug.Assert(dx.Count == 0);
        }

        public void TestNPCInjector()
        {
            var code = @"
[NPC]
public partial class C
{
   public int X { get; set; }
}

public class NPCAttribute : System.Attribute { }
";

            var trees = GetInjectedTrees<SampleInjectors.NotifyPropertyChangedInjector>(code);

            var comp = GetInjectedCompilation<SampleInjectors.NotifyPropertyChangedInjector>(code);
            var dx = comp.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
            Debug.Assert(dx.Count == 0);
        }

        static readonly MetadataReference mscorlib = MetadataReference.CreateFromFile(typeof(int).Assembly.Location);
        static readonly MetadataReference system = MetadataReference.CreateFromFile(typeof(System.ComponentModel.INotifyPropertyChanged).Assembly.Location);

        static readonly CSharpCompilationOptions options = new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary);

        private Compilation GetInjectedCompilation<TInjector>(string code)
        {
            var tree = SyntaxFactory.ParseSyntaxTree(code);
            var compilation = CSharpCompilation.Create("test", options: options)
                .AddSyntaxTrees(tree)
                .AddReferences(mscorlib, system);

            var processor = new CodeInjectionProcessor();
            var injector = processor.GetInjector(typeof(TInjector));

            var trees = processor.Generate(compilation, new[] { injector }, cancellationToken: CancellationToken.None);

            return compilation.AddSyntaxTrees(trees);
        }

        private ImmutableArray<SyntaxTree> GetInjectedTrees<TInjector>(string code)
        {
            var tree = SyntaxFactory.ParseSyntaxTree(code);
            var compilation = CSharpCompilation.Create("test")
                .AddSyntaxTrees(tree)
                .AddReferences(mscorlib, system);

            var processor = new CodeInjectionProcessor();
            var injector = processor.GetInjector(typeof(TInjector));

            return processor.Generate(compilation, new[] { injector }, cancellationToken: CancellationToken.None);
        }
    }
}
