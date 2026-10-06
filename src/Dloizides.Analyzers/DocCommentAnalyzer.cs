using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Dloizides.Analyzers.Rules;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;

namespace Dloizides.Analyzers;

/// <summary>Reports DLZ0002 for doc comments on non-public members and DLZ0003 for long or over-tagged ones.</summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class DocCommentAnalyzer : DiagnosticAnalyzer
{
    private const string NonMember = "a non-member";

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(Descriptors.DocCommentPublicOnly, Descriptors.DocCommentShape);

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSemanticModelAction(AnalyzeModel);
    }

    private static void AnalyzeModel(SemanticModelAnalysisContext context)
    {
        var tree = context.SemanticModel.SyntaxTree;
        if (CommentTrivia.IsExcludedPath(tree.FilePath))
            return;
        var root = tree.GetRoot(context.CancellationToken);
        foreach (var token in root.DescendantTokens())
            foreach (var block in DocBlocks(token.LeadingTrivia))
                AnalyzeBlock(context, token, block);
    }

    private static void AnalyzeBlock(SemanticModelAnalysisContext context, SyntaxToken owner, IReadOnlyList<SyntaxTrivia> block)
    {
        var span = TextSpan.FromBounds(block[0].SpanStart, block[block.Count - 1].Span.End);
        var location = Location.Create(context.SemanticModel.SyntaxTree, span);
        var text = new StringBuilder();
        foreach (var trivia in block)
            text.AppendLine(trivia.ToFullString());
        var shapeViolation = DocCommentText.FindViolation(text.ToString());
        if (shapeViolation is not null)
            context.ReportDiagnostic(Diagnostic.Create(Descriptors.DocCommentShape, location, shapeViolation));
        var symbol = OwningSymbol(context.SemanticModel, owner);
        if (symbol is null || !IsVisibleOutsideAssembly(symbol))
            context.ReportDiagnostic(Diagnostic.Create(Descriptors.DocCommentPublicOnly, location, symbol?.Name ?? NonMember));
    }

    private static IEnumerable<IReadOnlyList<SyntaxTrivia>> DocBlocks(SyntaxTriviaList trivia)
    {
        var current = new List<SyntaxTrivia>();
        foreach (var item in trivia)
        {
            if (CommentTrivia.IsDoc(item))
            {
                current.Add(item);
                continue;
            }
            if (CommentTrivia.IsLayout(item))
                continue;
            if (current.Count > 0)
                yield return current;
            current = new List<SyntaxTrivia>();
        }
        if (current.Count > 0)
            yield return current;
    }

    private static ISymbol? OwningSymbol(SemanticModel model, SyntaxToken token)
    {
        var member = token.Parent?.AncestorsAndSelf().FirstOrDefault(IsDeclaration);
        if (member is null || member.GetFirstToken() != token)
            return null;
        return member switch
        {
            BaseFieldDeclarationSyntax field => model.GetDeclaredSymbol(field.Declaration.Variables.First()),
            _ => model.GetDeclaredSymbol(member),
        };
    }

    private static bool IsDeclaration(SyntaxNode node) =>
        node is MemberDeclarationSyntax or LocalFunctionStatementSyntax or StatementSyntax;

    private static bool IsVisibleOutsideAssembly(ISymbol symbol)
    {
        if (symbol is IMethodSymbol { ExplicitInterfaceImplementations.Length: > 0 } or
            IPropertySymbol { ExplicitInterfaceImplementations.Length: > 0 } or
            IEventSymbol { ExplicitInterfaceImplementations.Length: > 0 })
            return true;
        for (var current = symbol; current is not null and not INamespaceSymbol; current = current.ContainingSymbol)
            if (current.DeclaredAccessibility is not (Accessibility.Public or Accessibility.Protected or Accessibility.ProtectedOrInternal))
                return false;
        return true;
    }
}
