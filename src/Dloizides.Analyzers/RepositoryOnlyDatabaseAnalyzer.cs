using System.Collections.Immutable;
using System.Linq;
using Dloizides.Analyzers.Rules;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Dloizides.Analyzers;

/// <summary>Reports DLZ0015 for a DbContext taken by a class that is not a repository or store.</summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class RepositoryOnlyDatabaseAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(PatternDescriptors.RepositoryOnlyDatabase);

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(start =>
        {
            var efCore = EfCoreTypes.From(start.Compilation);
            if (efCore is null || AssemblyKind.IsTest(start.Compilation.AssemblyName))
                return;
            start.RegisterSymbolAction(symbol => AnalyzeConstructor(symbol, efCore), SymbolKind.Method);
            start.RegisterSymbolAction(symbol => AnalyzeField(symbol, efCore), SymbolKind.Field);
        });
    }

    private static void AnalyzeConstructor(SymbolAnalysisContext context, EfCoreTypes efCore)
    {
        var method = (IMethodSymbol)context.Symbol;
        if (method.MethodKind != MethodKind.Constructor || efCore.MayTouchDatabase(method.ContainingType))
            return;
        foreach (var parameter in method.Parameters.Where(p => efCore.IsDatabaseHandle(p.Type)))
            context.ReportDiagnostic(Diagnostic.Create(PatternDescriptors.RepositoryOnlyDatabase, parameter.Locations[0], method.ContainingType.Name));
    }

    private static void AnalyzeField(SymbolAnalysisContext context, EfCoreTypes efCore)
    {
        var field = (IFieldSymbol)context.Symbol;
        var type = field.ContainingType;
        if (field.IsImplicitlyDeclared || !efCore.IsDatabaseHandle(field.Type) || efCore.MayTouchDatabase(type))
            return;
        var constructorTakesIt = type.Constructors.Any(c => c.Parameters.Any(p => efCore.IsDatabaseHandle(p.Type)));
        if (!constructorTakesIt)
            context.ReportDiagnostic(Diagnostic.Create(PatternDescriptors.RepositoryOnlyDatabase, field.Locations[0], type.Name));
    }
}
