using System;
using System.Collections.Immutable;
using System.Linq;
using Dloizides.Analyzers.Rules;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Dloizides.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class MemberOrderAnalyzer : DiagnosticAnalyzer
{
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptors.MemberOrder);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(
            Analyze,
            SyntaxKind.ClassDeclaration,
            SyntaxKind.StructDeclaration,
            SyntaxKind.RecordDeclaration,
            SyntaxKind.RecordStructDeclaration);
    }

    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var type = (TypeDeclarationSyntax)context.Node;
        if (GeneratedPath.IsMatch(type.SyntaxTree.FilePath))
            return;
        var members = type.Members.Select(OrderedMember.From).OfType<OrderedMember>().ToList();
        for (var index = 0; index < members.Count; index++)
        {
            var above = members[index];
            var below = members.Skip(index + 1).FirstOrDefault(m => string.Equals(m.Kind, above.Kind, StringComparison.Ordinal) && m.Rank < above.Rank);
            if (below is not null)
                context.ReportDiagnostic(Diagnostic.Create(
                    Descriptors.MemberOrder, above.Location, above.Name, above.Access, above.Kind, below.Access, below.Name));
        }
    }
}
