using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Dloizides.Analyzers.CodeFixes;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(SealedRecordCodeFixProvider))]
[Shared]
public sealed class SealedRecordCodeFixProvider : CodeFixProvider
{
    public const string SealedRecordId = "DLZ0029";
    private const string Title = "Seal the record";

    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create(SealedRecordId);

    public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        var declaration = root?.FindToken(context.Span.Start).Parent?.AncestorsAndSelf().OfType<RecordDeclarationSyntax>().FirstOrDefault();
        if (root is null || declaration is null)
            return;
        context.RegisterCodeFix(
            CodeAction.Create(Title, cancellationToken => SealAsync(context.Document, root, declaration, cancellationToken), Title),
            context.Diagnostics);
    }

    private static Task<Document> SealAsync(Document document, SyntaxNode root, RecordDeclarationSyntax declaration, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(document.WithSyntaxRoot(root.ReplaceNode(declaration, Sealed(declaration))));
    }

    private static RecordDeclarationSyntax Sealed(RecordDeclarationSyntax declaration)
    {
        var sealedKeyword = SyntaxFactory.Token(SyntaxKind.SealedKeyword).WithTrailingTrivia(SyntaxFactory.Space);
        var modifiers = declaration.Modifiers;
        var partialIndex = modifiers.IndexOf(SyntaxKind.PartialKeyword);
        var index = partialIndex < 0 ? modifiers.Count : partialIndex;
        if (index == 0)
            return MovedLeadingTrivia(declaration, sealedKeyword);
        return declaration.WithModifiers(modifiers.Insert(index, sealedKeyword));
    }

    private static RecordDeclarationSyntax MovedLeadingTrivia(RecordDeclarationSyntax declaration, SyntaxToken sealedKeyword)
    {
        var modifiers = declaration.Modifiers;
        if (modifiers.Count == 0)
            return declaration
                .WithKeyword(declaration.Keyword.WithLeadingTrivia())
                .WithModifiers(SyntaxFactory.TokenList(sealedKeyword.WithLeadingTrivia(declaration.Keyword.LeadingTrivia)));
        var first = modifiers[0];
        return declaration.WithModifiers(
            modifiers.Replace(first, first.WithLeadingTrivia()).Insert(0, sealedKeyword.WithLeadingTrivia(first.LeadingTrivia)));
    }
}
