using System;
using System.Collections.Immutable;
using Dloizides.Analyzers.Rules;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Dloizides.Analyzers;

/// <summary>Reports DLZ0006 for new HttpClient outside an AddHttpClient registration lambda.</summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class HttpClientFactoryAnalyzer : DiagnosticAnalyzer
{
    private static readonly ImmutableHashSet<string> RegistrationMethods = ImmutableHashSet.Create(
        StringComparer.Ordinal, "AddHttpClient", "ConfigureHttpClient", "ConfigurePrimaryHttpMessageHandler", "AddTypedClient");

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(PatternDescriptors.HttpClientFactory);

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(start =>
        {
            var httpClient = start.Compilation.GetTypeByMetadataName("System.Net.Http.HttpClient");
            if (httpClient is null || AssemblyKind.IsTest(start.Compilation.AssemblyName))
                return;
            start.RegisterOperationAction(operation => AnalyzeCreation(operation, httpClient), OperationKind.ObjectCreation);
        });
    }

    private static void AnalyzeCreation(OperationAnalysisContext context, INamedTypeSymbol httpClient)
    {
        var creation = (IObjectCreationOperation)context.Operation;
        if (!SymbolEqualityComparer.Default.Equals(creation.Type, httpClient) || IsInsideRegistrationLambda(creation))
            return;
        context.ReportDiagnostic(Diagnostic.Create(PatternDescriptors.HttpClientFactory, creation.Syntax.GetLocation()));
    }

    private static bool IsInsideRegistrationLambda(IOperation operation)
    {
        var insideLambda = false;
        for (var current = operation.Parent; current is not null; current = current.Parent)
        {
            if (current is IAnonymousFunctionOperation)
                insideLambda = true;
            else if (insideLambda && current is IInvocationOperation invocation && RegistrationMethods.Contains(invocation.TargetMethod.Name))
                return true;
        }
        return false;
    }
}
