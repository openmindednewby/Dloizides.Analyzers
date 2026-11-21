using System;
using System.Collections.Immutable;
using Dloizides.Analyzers.Rules;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Dloizides.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ServiceDefaultsGuardAnalyzer : DiagnosticAnalyzer
{
    private const string OwnerAssemblyPrefix = "Dloizides.ServiceDefaults";

    private static readonly ImmutableHashSet<string> SetupMethods = ImmutableHashSet.Create(
        StringComparer.Ordinal,
        "AddAuthentication",
        "AddJwtBearer",
        "AddCors",
        "UseSerilog",
        "AddRateLimiter",
        "AddProblemDetails",
        "AddResponseCompression",
        "UseExceptionHandler");

    private static readonly ImmutableHashSet<string> FrameworkRootNamespaces = ImmutableHashSet.Create(
        StringComparer.Ordinal, "Microsoft", "Serilog");

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(PatternDescriptors.ServiceDefaultsGuard);

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(start =>
        {
            var assemblyName = start.Compilation.AssemblyName ?? string.Empty;
            if (AssemblyKind.IsTest(assemblyName) || assemblyName.StartsWith(OwnerAssemblyPrefix, StringComparison.Ordinal))
                return;
            start.RegisterOperationAction(AnalyzeInvocation, OperationKind.Invocation);
        });
    }

    private static void AnalyzeInvocation(OperationAnalysisContext context)
    {
        var invocation = (IInvocationOperation)context.Operation;
        var method = invocation.TargetMethod;
        if (SetupMethods.Contains(method.Name) && FrameworkRootNamespaces.Contains(RootNamespace(method.ContainingNamespace)))
            context.ReportDiagnostic(Diagnostic.Create(PatternDescriptors.ServiceDefaultsGuard, invocation.Syntax.GetLocation(), method.Name));
    }

    private static string RootNamespace(INamespaceSymbol? symbol)
    {
        var root = symbol;
        while (root?.ContainingNamespace is { IsGlobalNamespace: false } parent)
            root = parent;
        return root?.Name ?? string.Empty;
    }
}
