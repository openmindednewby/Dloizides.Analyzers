using System;
using System.Collections.Immutable;
using Dloizides.Analyzers.Rules;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Dloizides.Analyzers;

/// <summary>Reports DLZ0009 for a static ConcurrentDictionary written outside its static initialization.</summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class StaticCacheAnalyzer : DiagnosticAnalyzer
{
    private static readonly ImmutableHashSet<string> WriteMethods = ImmutableHashSet.Create(
        StringComparer.Ordinal, "GetOrAdd", "TryAdd", "AddOrUpdate", "TryUpdate");

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(PatternDescriptors.SharedCache);

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(start =>
        {
            var dictionary = start.Compilation.GetTypeByMetadataName("System.Collections.Concurrent.ConcurrentDictionary`2");
            if (dictionary is null || AssemblyKind.IsTest(start.Compilation.AssemblyName))
                return;
            start.RegisterOperationAction(operation => AnalyzeInvocation(operation, dictionary), OperationKind.Invocation);
            start.RegisterOperationAction(operation => AnalyzeAssignment(operation, dictionary), OperationKind.SimpleAssignment);
        });
    }

    private static void AnalyzeInvocation(OperationAnalysisContext context, INamedTypeSymbol dictionary)
    {
        var invocation = (IInvocationOperation)context.Operation;
        if (!WriteMethods.Contains(invocation.TargetMethod.Name))
            return;
        Report(context, StaticDictionaryField(invocation.Instance, dictionary), invocation);
    }

    private static void AnalyzeAssignment(OperationAnalysisContext context, INamedTypeSymbol dictionary)
    {
        var assignment = (ISimpleAssignmentOperation)context.Operation;
        if (assignment.Target is not IPropertyReferenceOperation { Property.IsIndexer: true } indexer)
            return;
        Report(context, StaticDictionaryField(indexer.Instance, dictionary), assignment);
    }

    private static void Report(OperationAnalysisContext context, IFieldSymbol? field, IOperation write)
    {
        if (field is null || IsStaticInitialization(context.ContainingSymbol))
            return;
        context.ReportDiagnostic(Diagnostic.Create(PatternDescriptors.SharedCache, write.Syntax.GetLocation(), field.Name));
    }

    private static IFieldSymbol? StaticDictionaryField(IOperation? instance, INamedTypeSymbol dictionary) =>
        instance is IFieldReferenceOperation { Field: { IsStatic: true } field }
        && SymbolEqualityComparer.Default.Equals(field.Type.OriginalDefinition, dictionary)
            ? field
            : null;

    private static bool IsStaticInitialization(ISymbol symbol) =>
        symbol is IFieldSymbol or IMethodSymbol { MethodKind: MethodKind.StaticConstructor };
}
