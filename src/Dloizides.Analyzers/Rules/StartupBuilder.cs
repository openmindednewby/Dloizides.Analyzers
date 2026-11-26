using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace Dloizides.Analyzers.Rules;

internal static class StartupBuilder
{
    private const string BuilderSuffix = "Builder";
    private const string ServicesProperty = "Services";
    private const string ServiceCollection = "Microsoft.Extensions.DependencyInjection.IServiceCollection";

    private static readonly string[] HostNamespaces = { "Microsoft.Extensions.", "Microsoft.AspNetCore." };

    private static readonly HashSet<string> HostTypes = new(StringComparer.Ordinal)
    {
        ServiceCollection,
        "Microsoft.AspNetCore.Builder.IApplicationBuilder",
        "Microsoft.AspNetCore.Builder.WebApplication",
        "Microsoft.Extensions.Hosting.IHostBuilder",
    };

    public static bool IsMatch(ITypeSymbol? type) => type switch
    {
        null => false,
        ITypeParameterSymbol parameter => parameter.ConstraintTypes.Any(IsMatch),
        _ => SelfBasesAndInterfaces(type).Any(IsBuilderType),
    };

    private static bool IsBuilderType(INamespaceOrTypeSymbol type)
    {
        var name = TypeNames.FullName(type);
        return HostTypes.Contains(name)
            || (type.Name.EndsWith(BuilderSuffix, StringComparison.Ordinal) && Array.Exists(HostNamespaces, prefix => name.StartsWith(prefix, StringComparison.Ordinal)))
            || type.GetMembers(ServicesProperty).OfType<IPropertySymbol>().Any(property => TypeNames.Is(property.Type, ServiceCollection));
    }

    private static IEnumerable<ITypeSymbol> SelfBasesAndInterfaces(ITypeSymbol type)
    {
        for (var current = type; current is not null; current = current.BaseType)
            yield return current;
        foreach (var contract in type.AllInterfaces)
            yield return contract;
    }
}
