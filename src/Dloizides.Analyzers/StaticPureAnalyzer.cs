using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Dloizides.Analyzers.Rules;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Dloizides.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class StaticPureAnalyzer : DiagnosticAnalyzer
{
    public const string BaselineFileName = "static-pure-baseline.txt";

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(Descriptors.StaticPure, Descriptors.StaleBaseline);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(start =>
        {
            if (AssemblyKind.IsTest(start.Compilation.AssemblyName))
                return;
            var baseline = ViolationBaseline.From(start.Options.AdditionalFiles, BaselineFileName, Descriptors.StaticPure.Id, start.CancellationToken);
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
            .OfType<ClassDeclarationSyntax>()
            .Where(type => type.Modifiers.Any(SyntaxKind.StaticKeyword))
            .SelectMany(type => Violations(context.SemanticModel, type, context.CancellationToken))
            .ToList();
        baseline.Report(tree, context.ReportDiagnostic, violations);
    }

    private static IEnumerable<Diagnostic> Violations(SemanticModel model, ClassDeclarationSyntax type, CancellationToken cancellationToken) =>
        type.Members
            .Where(member => member is not BaseTypeDeclarationSyntax && !StaticPureSymbols.IsHostSetup(model, member, cancellationToken))
            .SelectMany(member => member.DescendantNodes())
            .Where(node => node is SimpleNameSyntax or BaseObjectCreationExpressionSyntax or ElementAccessExpressionSyntax)
            .Select(node => ToDiagnostic(type, node, model.GetSymbolInfo(node, cancellationToken).Symbol))
            .OfType<Diagnostic>();

    private static Diagnostic? ToDiagnostic(ClassDeclarationSyntax type, SyntaxNode node, ISymbol? symbol)
    {
        var effect = StaticPureSymbols.Effect(symbol);
        return effect is null || symbol is null
            ? null
            : Diagnostic.Create(Descriptors.StaticPure, node.GetLocation(), type.Identifier.Text, effect, symbol.ContainingType.Name + "." + symbol.Name);
    }
}
