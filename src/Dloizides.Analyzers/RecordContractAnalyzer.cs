using System;
using System.Collections.Immutable;
using System.Linq;
using Dloizides.Analyzers.Rules;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Dloizides.Analyzers;

/// <summary>Reports DLZ0012 for a class named *Dto, *Command or *Event that could be a record and is not.</summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class RecordContractAnalyzer : DiagnosticAnalyzer
{
    private static readonly string[] ContractSuffixes = { "Dto", "DTO", "Command", "Event" };

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(PatternDescriptors.RecordContract);

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(start =>
        {
            if (AssemblyKind.IsTest(start.Compilation.AssemblyName))
                return;
            start.RegisterSymbolAction(AnalyzeType, SymbolKind.NamedType);
        });
    }

    private static void AnalyzeType(SymbolAnalysisContext context)
    {
        var type = (INamedTypeSymbol)context.Symbol;
        var isPlainClass = type.TypeKind == TypeKind.Class && !type.IsRecord && !type.IsStatic && !type.IsImplicitlyDeclared;
        if (!isPlainClass || !HasContractName(type.Name) || !DerivesFromObject(type))
            return;
        context.ReportDiagnostic(Diagnostic.Create(PatternDescriptors.RecordContract, type.Locations[0], type.Name));
    }

    private static bool HasContractName(string name) =>
        ContractSuffixes.Any(suffix => name.Length > suffix.Length && name.EndsWith(suffix, StringComparison.Ordinal));

    private static bool DerivesFromObject(INamedTypeSymbol type) =>
        type.BaseType is null || type.BaseType.SpecialType == SpecialType.System_Object;
}
