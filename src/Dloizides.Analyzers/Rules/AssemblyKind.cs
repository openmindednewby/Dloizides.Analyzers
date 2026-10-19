using System;

namespace Dloizides.Analyzers.Rules;

internal static class AssemblyKind
{
    private const string TestAssemblySuffix = "Tests";

    public static bool IsTest(string? assemblyName) =>
        assemblyName is not null && assemblyName.EndsWith(TestAssemblySuffix, StringComparison.Ordinal);
}
