// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Linq;
using Microsoft.CodeAnalysis;
using Roslyn.Test.Utilities;
using Roslyn.VisualStudio.Test.Utilities;
using Roslyn.VisualStudio.Test.Utilities.Common;
using Roslyn.VisualStudio.Test.Utilities.Input;
using Xunit;

namespace Roslyn.VisualStudio.IntegrationTests.CSharp
{
    [Collection(nameof(SharedIntegrationHostFixture))]
    public class CSharpWorkspace : AbstractEditorTest
    {
        protected override string LanguageName => LanguageNames.CSharp;

        public CSharpWorkspace(VisualStudioInstanceFactory instanceFactory)
            : base(instanceFactory, nameof(CSharpWorkspace))
        {
        }

        [Fact]
        public void TestRemoveMetadataReference()
        {
            var references = VisualStudioWorkspaceOutOfProc.GetMetadataReferenceNames(ProjectName);
            var reference = references.FirstOrDefault(r => r.Contains("System.Core"));
            Assert.NotNull(reference);

            Editor.MessageBox("Attach now");
            VisualStudioWorkspaceOutOfProc.RemoveMetadataReference(ProjectName, reference);

            Editor.MessageBox("Removed");
            var newReferences = VisualStudioWorkspaceOutOfProc.GetMetadataReferenceNames(ProjectName);
            var newReference = newReferences.First(r => r.Contains("System.Core"));
            Assert.Null(newReference);
        }
    }
}