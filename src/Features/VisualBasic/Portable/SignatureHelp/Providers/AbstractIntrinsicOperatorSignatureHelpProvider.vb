' Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

Imports System.Threading
Imports Microsoft.CodeAnalysis.LanguageServices
Imports Microsoft.CodeAnalysis.SignatureHelp
Imports Microsoft.CodeAnalysis.Text
Imports Microsoft.CodeAnalysis.VisualBasic.Utilities.IntrinsicOperators

Namespace Microsoft.CodeAnalysis.VisualBasic.SignatureHelp.Providers
    Friend MustInherit Class AbstractIntrinsicOperatorSignatureHelpProvider(Of TSyntaxNode As SyntaxNode)
        Inherits AbstractVisualBasicSignatureHelpProvider

        Protected MustOverride Function IsTriggerToken(token As SyntaxToken) As Boolean
        Protected MustOverride Function IsArgumentListToken(node As TSyntaxNode, token As SyntaxToken) As Boolean
        Protected MustOverride Function GetIntrinsicOperatorDocumentation(node As TSyntaxNode, document As Document, cancellationToken As CancellationToken) As IEnumerable(Of AbstractIntrinsicOperatorDocumentation)

        Private Function TryGetSyntaxNode(root As SyntaxNode, position As Integer, syntaxFacts As ISyntaxFactsService, triggerReason As SignatureHelpTriggerKind, cancellationToken As CancellationToken, ByRef node As TSyntaxNode) As Boolean
            Return CommonSignatureHelpUtilities.TryGetSyntax(
                root,
                position,
                syntaxFacts,
                triggerReason,
                AddressOf IsTriggerToken,
                AddressOf IsArgumentListToken,
                cancellationToken,
                node)
        End Function

        Protected Overrides Async Function ProvideSignaturesWorkerAsync(context As SignatureContext) As Task
            Dim document = context.Document
            Dim position = context.Position
            Dim trigger = context.Trigger
            Dim cancellationToken = context.CancellationToken

            Dim root = Await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(False)

            Dim node As TSyntaxNode = Nothing
            If Not TryGetSyntaxNode(root, position, Document.GetLanguageService(Of ISyntaxFactsService), trigger.Kind, CancellationToken, node) Then
                Return
            End If

            Dim items As New List(Of SignatureHelpItem)

            Dim semanticModel = Await Document.GetSemanticModelForNodeAsync(node, CancellationToken).ConfigureAwait(False)
            For Each documentation In GetIntrinsicOperatorDocumentation(node, Document, CancellationToken)
                Dim signatureHelpItem = GetSignatureHelpItemForIntrinsicOperator(Document, semanticModel, node.SpanStart, documentation, CancellationToken)
                context.AddItem(signatureHelpItem)
            Next

            Dim textSpan = CommonSignatureHelpUtilities.GetSignatureHelpSpan(node, node.SpanStart, Function(n) n.ChildTokens.FirstOrDefault(Function(c) c.Kind = SyntaxKind.CloseParenToken))
            Dim syntaxFacts = document.GetLanguageService(Of ISyntaxFactsService)

            context.SetApplicableSpan(textSpan)
            context.SetState(GetCurrentArgumentState(root, position, syntaxFacts, textSpan, cancellationToken))
        End Function

        Friend Function GetSignatureHelpItemForIntrinsicOperator(document As Document, semanticModel As SemanticModel, position As Integer, documentation As AbstractIntrinsicOperatorDocumentation, cancellationToken As CancellationToken) As SignatureHelpItem
            Dim parameters As New List(Of SignatureHelpSymbolParameter)

            For i = 0 To documentation.ParameterCount - 1
                Dim capturedIndex = i
                parameters.Add(
                    New SignatureHelpSymbolParameter(
                        name:=documentation.GetParameterName(i),
                        isOptional:=False,
                        documentationFactory:=Function(c As CancellationToken) documentation.GetParameterDocumentation(capturedIndex).ToSymbolDisplayParts().ToTaggedText(),
                        displayParts:=documentation.GetParameterDisplayParts(i)))
            Next

            Dim suffixParts = documentation.GetSuffix(semanticModel, position, Nothing, cancellationToken)

            Dim symbolDisplayService = document.Project.LanguageServices.GetService(Of ISymbolDisplayService)()
            Dim anonymousTypeDisplayService = document.Project.LanguageServices.GetService(Of IAnonymousTypeDisplayService)()

            Return CreateItem(
                Nothing, semanticModel, position,
                symbolDisplayService, anonymousTypeDisplayService,
                isVariadic:=False,
                documentationFactory:=Function(c) SpecializedCollections.SingletonEnumerable(New TaggedText(TextTags.Text, documentation.DocumentationText)),
                prefixParts:=documentation.PrefixParts,
                separatorParts:=GetSeparatorParts(),
                suffixParts:=suffixParts,
                parameters:=parameters)
        End Function

        Protected Overridable Function GetCurrentArgumentStateWorker(node As SyntaxNode, position As Integer) As SignatureHelpState
            Dim commaTokens As New List(Of SyntaxToken)
            commaTokens.AddRange(node.ChildTokens().Where(Function(token) token.Kind = SyntaxKind.CommaToken))

            ' Also get any leading skipped tokens on the next token after this node
            Dim nextToken = node.GetLastToken().GetNextToken()

            For Each leadingTrivia In nextToken.LeadingTrivia
                If leadingTrivia.Kind = SyntaxKind.SkippedTokensTrivia Then
                    commaTokens.AddRange(leadingTrivia.GetStructure().ChildTokens().Where(Function(token) token.Kind = SyntaxKind.CommaToken))
                End If
            Next

            ' Count how many commas are before us
            Return New SignatureHelpState(
                argumentIndex:=commaTokens.Where(Function(token) token.SpanStart < position).Count(),
                argumentCount:=commaTokens.Count() + 1,
                argumentName:=Nothing, argumentNames:=Nothing)
        End Function

        Protected Overrides Function GetCurrentArgumentState(root As SyntaxNode, position As Integer, syntaxFacts As ISyntaxFactsService, currentSpan As TextSpan, cancellationToken As CancellationToken) As SignatureHelpState
            Dim node As TSyntaxNode = Nothing
            If TryGetSyntaxNode(root, position, syntaxFacts, SignatureHelpTriggerKind.Other, cancellationToken, node) AndAlso
                currentSpan.Start = node.SpanStart Then

                Return GetCurrentArgumentStateWorker(node, position)
            End If

            Return Nothing
        End Function
    End Class
End Namespace
