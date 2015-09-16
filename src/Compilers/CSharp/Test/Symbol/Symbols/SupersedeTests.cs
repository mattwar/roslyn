// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Linq;
using Microsoft.CodeAnalysis.CSharp.Symbols;
using System.IO;
using System.Threading;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.CSharp.Test.Utilities;
using Microsoft.CodeAnalysis.Emit;
using Microsoft.CodeAnalysis.Text;
using Xunit;

namespace Microsoft.CodeAnalysis.CSharp.UnitTests
{
    public class SupersedeTests : CSharpTestBase
    {
        [Fact]
        public void SupersedeInSameClass()
        {
            var text = @"
using System;

class C
{
    void M() { Console.WriteLine(""original M""); }
    supersede void M() { Console.WriteLine(""superseded M""); superseded(); }

    void F() { M(); }
}
";
            //var tree = SyntaxFactory.ParseSyntaxTree(text);
            //var comp = CSharpCompilation.Create("test").AddSyntaxTrees(tree);
            var comp = CreateCompilationWithMscorlib(text);

            /*
            var classC = (INamedTypeSymbol)comp.Assembly.GlobalNamespace.GetMembers("C")[0];
            var method = classC.GetMembers("M")[0];
            var root = method.DeclaringSyntaxReferences[0].GetSyntax();
            var invocation = root.DescendantNodes().OfType<InvocationExpressionSyntax>().First();
            var model = comp.GetSemanticModel(root.SyntaxTree);
            var symbol = model.GetSymbolInfo(invocation.Expression);
            */

            var dx = comp.GetDiagnostics();
            Assert.Equal(0, dx.Length);

            var outputStream = new MemoryStream();
            var result = comp.Emit(peStream: outputStream, pdbStream: null, xmlDocumentationStream: null, win32Resources: null, manifestResources: null, options: EmitOptions.Default, cancellationToken: CancellationToken.None);
        }

        [Fact]
        public void SupersedeWithProperties()
        {
            var text = @"
class X
{
    public static void Main(string[] args)
    {
       var c = new C();
       c.P = 5;
    }
}

class C
{
    private int _p;
    public int P 
    { 
        get { return _p; } 
        set { _p = value; } 
    }
}

partial class C
{
    public supersede int P 
    { 
        get { return superseded; }
        set { superseded = value; }
    }
 }
";
            //var tree = SyntaxFactory.ParseSyntaxTree(text);
            //var comp = CSharpCompilation.Create("test").AddSyntaxTrees(tree);
            var comp = CreateCompilationWithMscorlib(text);

            var dx = comp.GetDiagnostics();
            Assert.Equal(0, dx.Length);

            var outputStream = new MemoryStream();
            var result = comp.Emit(peStream: outputStream, pdbStream: null, xmlDocumentationStream: null, win32Resources: null, manifestResources: null, options: EmitOptions.Default, cancellationToken: CancellationToken.None);
        }

        [Fact]
        public void SupersedeWithNPC()
        {
            var text1 = @"
class X
{
    public static void Main(string[] args)
    {
    }
}

public partial class C
{
   public int X { get; set; }
}";

    var text2 = @"
public partial class C : global::System.ComponentModel.INotifyPropertyChanged
{
    public event global::System.ComponentModel.PropertyChangedEventHandler PropertyChanged;
    public supersede System.Int32 X
    {
        get
        {
            return superseded;
        }

        set
        {
            if ((superseded) != (value))
            {
                superseded = (value);
                {
                    var ev = PropertyChanged;
                    if ((ev) != (null))
                    {
                        ev(this, new global::System.ComponentModel.PropertyChangedEventArgs(""X""));
                    }
                }
            }
        }
    }
}";
            var tree1 = SyntaxFactory.ParseSyntaxTree(text1);
            var tree2 = SyntaxFactory.ParseSyntaxTree(text2);

            //var comp = CSharpCompilation.Create("test").AddSyntaxTrees(tree);
            var comp = CreateCompilation(trees: new[] { tree1, tree2 }, references: new[] { CSharpTestBase.MscorlibRef, CSharpTestBase.SystemRef });
            
            var dx = comp.GetDiagnostics();
            Assert.Equal(0, dx.Length);

            var outputStream = new MemoryStream();
            var result = comp.Emit(peStream: outputStream, pdbStream: null, xmlDocumentationStream: null, win32Resources: null, manifestResources: null, options: EmitOptions.Default, cancellationToken: CancellationToken.None);
        }
    }
}