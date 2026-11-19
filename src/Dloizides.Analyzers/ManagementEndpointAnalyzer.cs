using System;
using System.Collections.Immutable;
using Dloizides.Analyzers.Rules;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Dloizides.Analyzers;

/// <summary>Reports DLZ0020 for MapMetrics, MapPrometheusScrapingEndpoint or MapHealthChecks outside the owning packages.</summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ManagementEndpointAnalyzer : DiagnosticAnalyzer
{
    private static readonly ImmutableHashSet<string> MappingMethods = ImmutableHashSet.Create(
        StringComparer.Ordinal, "MapMetrics", "MapPrometheusScrapingEndpoint", "MapHealthChecks");

    private static readonly ImmutableHashSet<string> OwnerAssemblies = ImmutableHashSet.Create(
        StringComparer.Ordinal, "Metrics.Client", "ServiceDefaults.HealthChecks", "Dloizides.ServiceDefaults");

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(PatternDescriptors.ManagementEndpoint);

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(start =>
        {
            var assemblyName = start.Compilation.AssemblyName ?? string.Empty;
            if (AssemblyKind.IsTest(assemblyName) || OwnerAssemblies.Contains(assemblyName))
                return;
            start.RegisterOperationAction(AnalyzeInvocation, OperationKind.Invocation);
        });
    }

    private static void AnalyzeInvocation(OperationAnalysisContext context)
    {
        var invocation = (IInvocationOperation)context.Operation;
        var name = invocation.TargetMethod.Name;
        if (MappingMethods.Contains(name))
            context.ReportDiagnostic(Diagnostic.Create(PatternDescriptors.ManagementEndpoint, invocation.Syntax.GetLocation(), name));
    }
}
