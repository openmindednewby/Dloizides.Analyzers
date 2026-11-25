using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using Dloizides.Analyzers.Rules;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Dloizides.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class DocContractAnalyzer : DiagnosticAnalyzer
{
    public const string BaselineFileName = "doc-contract-baseline.txt";

    private static readonly Regex SummaryTag = new(@"<\s*summary\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(Descriptors.InterfaceMemberSummary, Descriptors.ImplementationSummary, Descriptors.StaleBaseline, Descriptors.BaselineOverCount);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(start =>
        {
            if (AssemblyKind.IsTest(start.Compilation.AssemblyName))
                return;
            var baseline = ViolationBaseline.From(start.Options.AdditionalFiles, BaselineFileName, Descriptors.InterfaceMemberSummary.Id, start.CancellationToken);
            start.RegisterSemanticModelAction(model => Analyze(model, baseline));
        });
    }

    private static void Analyze(SemanticModelAnalysisContext context, ViolationBaseline baseline)
    {
        var tree = context.SemanticModel.SyntaxTree;
        if (GeneratedPath.IsMatch(tree.FilePath))
            return;
        var violations = tree.GetRoot(context.CancellationToken)
            .DescendantNodes()
            .OfType<MemberDeclarationSyntax>()
            .Select(member => ToDiagnostic(context.SemanticModel, member, context.CancellationToken))
            .OfType<Diagnostic>()
            .ToList();
        baseline.Report(tree, context.ReportDiagnostic, violations);
    }

    private static Diagnostic? ToDiagnostic(SemanticModel model, MemberDeclarationSyntax member, CancellationToken cancellationToken)
    {
        var symbol = DeclaredSymbol(model, member, cancellationToken);
        if (symbol?.ContainingType is not { } owner || symbol.Locations.FirstOrDefault() is not { } location)
            return null;
        if (owner.TypeKind == TypeKind.Interface)
            return IsVisibleContract(symbol) && !HasSummary(member)
                ? Diagnostic.Create(Descriptors.InterfaceMemberSummary, location, symbol.Name)
                : null;
        if (!HasSummary(member))
            return null;
        var contract = ContractMembers(owner, symbol).FirstOrDefault(candidate => HasContractSummary(candidate, cancellationToken));
        return contract is null
            ? null
            : Diagnostic.Create(Descriptors.ImplementationSummary, location, symbol.Name, contract.ContainingType.Name);
    }

    private static ISymbol? DeclaredSymbol(SemanticModel model, MemberDeclarationSyntax member, CancellationToken cancellationToken) =>
        member switch
        {
            MethodDeclarationSyntax or PropertyDeclarationSyntax or IndexerDeclarationSyntax or EventDeclarationSyntax =>
                model.GetDeclaredSymbol(member, cancellationToken),
            EventFieldDeclarationSyntax field => model.GetDeclaredSymbol(field.Declaration.Variables[0], cancellationToken),
            _ => null,
        };

    private static bool IsVisibleContract(ISymbol symbol)
    {
        for (ISymbol? current = symbol; current is not null and not INamespaceSymbol; current = current.ContainingSymbol)
        {
            if (current.DeclaredAccessibility != Accessibility.Public)
                return false;
        }
        return true;
    }

    private static IEnumerable<ISymbol> ContractMembers(INamedTypeSymbol owner, ISymbol symbol) =>
        owner.AllInterfaces
            .SelectMany(contract => contract.GetMembers())
            .Where(candidate => SymbolEqualityComparer.Default.Equals(owner.FindImplementationForInterfaceMember(candidate), symbol));

    private static bool HasContractSummary(ISymbol contract, CancellationToken cancellationToken)
    {
        var definition = contract.OriginalDefinition;
        var declarations = definition.DeclaringSyntaxReferences;
        if (declarations.IsEmpty)
            return SummaryTag.IsMatch(definition.GetDocumentationCommentXml(cancellationToken: cancellationToken) ?? string.Empty);
        return declarations
            .Select(reference => reference.GetSyntax(cancellationToken).FirstAncestorOrSelf<MemberDeclarationSyntax>())
            .Any(declaration => declaration is not null && HasSummary(declaration));
    }

    private static bool HasSummary(MemberDeclarationSyntax member) =>
        member.GetLeadingTrivia().Where(IsDocTrivia).Any(trivia => SummaryTag.IsMatch(trivia.ToFullString()));

    private static bool IsDocTrivia(SyntaxTrivia trivia) =>
        trivia.Kind() switch
        {
            SyntaxKind.SingleLineDocumentationCommentTrivia or SyntaxKind.MultiLineDocumentationCommentTrivia => true,
            SyntaxKind.SingleLineCommentTrivia => trivia.ToString().StartsWith("///", StringComparison.Ordinal),
            SyntaxKind.MultiLineCommentTrivia => trivia.ToString().StartsWith("/**", StringComparison.Ordinal),
            _ => false,
        };
}
