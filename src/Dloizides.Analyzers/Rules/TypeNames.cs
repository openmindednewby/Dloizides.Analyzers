using System;
using Microsoft.CodeAnalysis;

namespace Dloizides.Analyzers.Rules;

internal static class TypeNames
{
    public static string FullName(ISymbol? type) =>
        type is null || type.ContainingNamespace is null || type.ContainingNamespace.IsGlobalNamespace
            ? type?.MetadataName ?? string.Empty
            : type.ContainingNamespace.ToDisplayString() + "." + type.MetadataName;

    public static bool Is(ISymbol? type, string fullName) => string.Equals(FullName(type), fullName, StringComparison.Ordinal);
}
