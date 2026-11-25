using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Dloizides.Analyzers.Rules;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Dloizides.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class SealedRecordAnalyzer : DiagnosticAnalyzer
{
    public const string BaselineFileName = "sealed-record-baseline.txt";

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(Descriptors.SealedRecord, Descriptors.StaleBaseline, Descriptors.BaselineOverCount);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(start =>
        {
            var candidates = new ConcurrentBag<INamedTypeSymbol>();
            var bases = new ConcurrentDictionary<INamedTypeSymbol, bool>(SymbolEqualityComparer.Default);
            start.RegisterSymbolAction(symbol => Collect((INamedTypeSymbol)symbol.Symbol, candidates, bases), SymbolKind.NamedType);
            start.RegisterCompilationEndAction(end => Report(end, candidates, bases));
        });
    }

    private static void Collect(INamedTypeSymbol type, ConcurrentBag<INamedTypeSymbol> candidates, ConcurrentDictionary<INamedTypeSymbol, bool> bases)
    {
        if (!type.IsRecord || type.TypeKind != TypeKind.Class)
            return;
        if (type.BaseType is { IsRecord: true } baseType)
            bases[baseType.OriginalDefinition] = true;
        if (!type.IsSealed && !type.IsAbstract)
            candidates.Add(type);
    }

    private static void Report(CompilationAnalysisContext context, ConcurrentBag<INamedTypeSymbol> candidates, ConcurrentDictionary<INamedTypeSymbol, bool> bases)
    {
        var baseline = ViolationBaseline.From(context.Options.AdditionalFiles, BaselineFileName, Descriptors.SealedRecord.Id, context.CancellationToken);
        var violations = candidates
            .Where(type => !bases.ContainsKey(type.OriginalDefinition))
            .Select(ToDiagnostic)
            .OfType<Diagnostic>()
            .ToLookup(diagnostic => diagnostic.Location.SourceTree!);
        foreach (var tree in context.Compilation.SyntaxTrees.Where(tree => !GeneratedPath.IsMatch(tree.FilePath)))
            baseline.Report(tree, context.ReportDiagnostic, InSourceOrder(violations[tree]));
    }

    private static Diagnostic? ToDiagnostic(INamedTypeSymbol type)
    {
        var location = type.Locations.FirstOrDefault(candidate => candidate.IsInSource && candidate.SourceTree is not null);
        return location is null ? null : Diagnostic.Create(Descriptors.SealedRecord, location, type.Name);
    }

    private static List<Diagnostic> InSourceOrder(IEnumerable<Diagnostic> diagnostics) =>
        diagnostics.OrderBy(diagnostic => diagnostic.Location.SourceSpan.Start).ToList();
}
