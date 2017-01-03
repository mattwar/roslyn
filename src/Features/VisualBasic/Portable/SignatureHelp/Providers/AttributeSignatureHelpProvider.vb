' Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

Imports System.Threading
Imports Microsoft.CodeAnalysis
Imports Microsoft.CodeAnalysis.DocumentationComments
Imports Microsoft.CodeAnalysis.LanguageServices
Imports Microsoft.CodeAnalysis.SignatureHelp
Imports Microsoft.CodeAnalysis.Text
Imports Microsoft.CodeAnalysis.VisualBasic.Syntax

Namespace Microsoft.CodeAnalysis.VisualBasic.SignatureHelp.Providers
    Friend Class AttributeSignatureHelpProvider
        Inherits AbstractVisualBasicSignatureHelpProvider

        Public Overrides Function IsTriggerCharacter(ch As Char) As Boolean
            Return ch = "("c OrElse ch = ","c
        End Function

        Public Overrides Function IsRetriggerCharacter(ch As Char) As Boolean
            Return ch = ")"c
        End Function

        Private Function TryGetAttributeExpression(root As SyntaxNode, position As Integer, syntaxFacts As ISyntaxFactsService, triggerReason As SignatureHelpTriggerKind, cancellationToken As CancellationToken, ByRef attribute As AttributeSyntax) As Boolean
            If Not CommonSignatureHelpUtilities.TryGetSyntax(root, position, syntaxFacts, triggerReason, AddressOf IsTriggerToken, AddressOf IsArgumentListToken, cancellationToken, attribute) Then
                Return False
            End If

            Return attribute.ArgumentList IsNot Nothing
        End Function

        Private Function IsTriggerToken(token As SyntaxToken) As Boolean
            Return token.IsKind(SyntaxKind.OpenParenToken, SyntaxKind.CommaToken) AndAlso
                TypeOf token.Parent Is ArgumentListSyntax AndAlso
                TypeOf token.Parent.Parent Is AttributeSyntax
        End Function

        Private Shared Function IsArgumentListToken(node As AttributeSyntax, token As SyntaxToken) As Boolean
            Return node.ArgumentList IsNot Nothing AndAlso
                node.ArgumentList.Span.Contains(token.SpanStart) AndAlso
                token <> node.ArgumentList.CloseParenToken
        End Function

        Protected Overrides Async Function ProvideSignaturesWorkerAsync(context As SignatureContext) As Task
            Dim document = context.Document
            Dim position = context.Position
            Dim trigger = context.Trigger
            Dim cancellationToken = context.CancellationToken

            Dim root = Await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(False)

            Dim attribute As AttributeSyntax = Nothing
            If Not TryGetAttributeExpression(root, position, document.GetLanguageService(Of ISyntaxFactsService), trigger.Kind, cancellationToken, attribute) Then
                Return
            End If

            Dim semanticModel = Await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(False)
            Dim attributeType = TryCast(semanticModel.GetTypeInfo(attribute, cancellationToken).Type, INamedTypeSymbol)
            If attributeType Is Nothing Then
                Return
            End If

            Dim within = semanticModel.GetEnclosingNamedTypeOrAssembly(position, cancellationToken)
            If within Is Nothing Then
                Return
            End If

            Dim symbolDisplayService = document.Project.LanguageServices.GetService(Of ISymbolDisplayService)()
            Dim accessibleConstructors = attributeType.InstanceConstructors.
                                                       WhereAsArray(Function(c) c.IsAccessibleWithin(within)).
                                                       FilterToVisibleAndBrowsableSymbolsAndNotUnsafeSymbols(document.ShouldHideAdvancedMembers(), semanticModel.Compilation).
                                                       Sort(symbolDisplayService, semanticModel, attribute.SpanStart)

            If Not accessibleConstructors.Any() Then
                Return
            End If

            Dim anonymousTypeDisplayService = document.Project.LanguageServices.GetService(Of IAnonymousTypeDisplayService)()
            Dim textSpan = SignatureHelpUtilities.GetSignatureHelpSpan(attribute.ArgumentList)
            Dim syntaxFacts = document.GetLanguageService(Of ISyntaxFactsService)

            context.AddItems(accessibleConstructors.Select(
                Function(c) Convert(c, within, attribute, semanticModel, symbolDisplayService, anonymousTypeDisplayService, cancellationToken)))

            context.SetSpan(textSpan)
            context.SetState(GetCurrentArgumentState(root, position, syntaxFacts, textSpan, cancellationToken))
        End Function

        Protected Overrides Function GetCurrentArgumentState(root As SyntaxNode, position As Integer, syntaxFacts As ISyntaxFactsService, currentSpan As TextSpan, cancellationToken As CancellationToken) As SignatureHelpState
            Dim expression As AttributeSyntax = Nothing
            If TryGetAttributeExpression(root, position, syntaxFacts, SignatureHelpTriggerKind.Other, cancellationToken, expression) AndAlso
                currentSpan.Start = SignatureHelpUtilities.GetSignatureHelpSpan(expression.ArgumentList).Start Then

                Return SignatureHelpUtilities.GetSignatureHelpState(expression.ArgumentList, position)
            End If

            Return Nothing
        End Function

        Private Overloads Function Convert(constructor As IMethodSymbol,
                                           within As ISymbol,
                                           attribute As AttributeSyntax,
                                           semanticModel As SemanticModel,
                                           symbolDisplayService As ISymbolDisplayService,
                                           anonymousTypeDisplayService As IAnonymousTypeDisplayService,
                                           cancellationToken As CancellationToken) As SignatureHelpItem
            Dim position = attribute.SpanStart
            Dim namedParameters = constructor.ContainingType.GetAttributeNamedParameters(semanticModel.Compilation, within).
                                                             OrderBy(Function(s) s.Name).
                                                             ToList()

            Dim isVariadic =
                constructor.Parameters.Length > 0 AndAlso constructor.Parameters.Last().IsParams AndAlso namedParameters.Count = 0

            Dim item = CreateItem(
                constructor, semanticModel, position,
                symbolDisplayService, anonymousTypeDisplayService,
                isVariadic,
                GetPreambleParts(constructor, semanticModel, position),
                GetSeparatorParts(),
                GetPostambleParts(constructor),
                GetParameters(constructor, semanticModel, position, namedParameters, cancellationToken))
            Return item
        End Function

        Private Function GetParameters(constructor As IMethodSymbol,
                                       semanticModel As SemanticModel,
                                       position As Integer,
                                       namedParameters As List(Of ISymbol),
                                       cancellationToken As CancellationToken) As IList(Of CommonParameterData)
            Dim result = New List(Of CommonParameterData)

            For Each parameter In constructor.Parameters
                result.Add(Convert(parameter, semanticModel, position, cancellationToken))
            Next

            For i = 0 To namedParameters.Count - 1
                Dim namedParameter = namedParameters(i)

                Dim type = If(TypeOf namedParameter Is IFieldSymbol,
                               DirectCast(namedParameter, IFieldSymbol).Type,
                               DirectCast(namedParameter, IPropertySymbol).Type)

                Dim displayParts = New List(Of SymbolDisplayPart)

                displayParts.Add(New SymbolDisplayPart(
                    If(TypeOf namedParameter Is IFieldSymbol, SymbolDisplayPartKind.FieldName, SymbolDisplayPartKind.PropertyName),
                    namedParameter, namedParameter.Name.ToIdentifierToken.ToString()))
                displayParts.Add(Punctuation(SyntaxKind.ColonEqualsToken))
                displayParts.AddRange(type.ToMinimalDisplayParts(semanticModel, position))

                result.Add(New CommonParameterData(
                    namedParameter.Name,
                    isOptional:=True,
                    symbol:=namedParameter,
                    position:=position,
                    displayParts:=displayParts.ToImmutableArrayOrEmpty(),
                    prefixDisplayParts:=GetParameterPrefixDisplayParts(i).ToImmutableArrayOrEmpty()))
            Next

            Return result
        End Function

        Private Shared Function GetParameterPrefixDisplayParts(i As Integer) As List(Of SymbolDisplayPart)
            If i = 0 Then
                Return New List(Of SymbolDisplayPart) From {
                    New SymbolDisplayPart(SymbolDisplayPartKind.Text, Nothing, VBFeaturesResources.Properties),
                    Punctuation(SyntaxKind.ColonToken),
                    Space()
                }
            End If

            Return Nothing
        End Function

        Private Function GetPreambleParts(method As IMethodSymbol, semanticModel As SemanticModel, position As Integer) As IList(Of SymbolDisplayPart)
            Dim result = New List(Of SymbolDisplayPart)()
            result.AddRange(method.ContainingType.ToMinimalDisplayParts(semanticModel, position))
            result.Add(Punctuation(SyntaxKind.OpenParenToken))
            Return result
        End Function

        Private Function GetPostambleParts(method As IMethodSymbol) As IList(Of SymbolDisplayPart)
            Return {Punctuation(SyntaxKind.CloseParenToken)}
        End Function
    End Class
End Namespace
