using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Dloizides.Analyzers.Rules;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Dloizides.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class MemberOrderAnalyzer : DiagnosticAnalyzer
{
    public const string BaselineFileName = "member-order-baseline.txt";

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(Descriptors.MemberOrder, Descriptors.StaleBaseline, Descriptors.BaselineOverCount);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(start =>
        {
            var baseline = ViolationBaseline.From(start.Options.AdditionalFiles, BaselineFileName, Descriptors.MemberOrder.Id, start.CancellationToken);
            start.RegisterSyntaxTreeAction(tree => Analyze(tree, baseline));
        });
    }

    private static void Analyze(SyntaxTreeAnalysisContext context, ViolationBaseline baseline)
    {
        if (GeneratedPath.IsMatch(context.Tree.FilePath))
            return;
        var violations = context.Tree.GetRoot(context.CancellationToken)
            .DescendantNodes()
            .OfType<TypeDeclarationSyntax>()
            .Where(type => type is not InterfaceDeclarationSyntax)
            .SelectMany(Misorders)
            .ToList();
        baseline.Report(context, violations);
    }

    private static IEnumerable<Diagnostic> Misorders(TypeDeclarationSyntax type)
    {
        var members = type.Members.Select(OrderedMember.From).OfType<OrderedMember>().ToList();
        for (var index = 0; index < members.Count; index++)
        {
            var above = members[index];
            var below = members.Skip(index + 1).FirstOrDefault(m => string.Equals(m.Kind, above.Kind, StringComparison.Ordinal) && m.Rank < above.Rank);
            if (below is not null)
                yield return Diagnostic.Create(
                    Descriptors.MemberOrder, above.Location, above.Name, above.Access, above.Kind, below.Access, below.Name);
        }
    }
}
