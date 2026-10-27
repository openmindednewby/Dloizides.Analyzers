using System;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace Dloizides.Analyzers.Rules;

internal sealed class EfCoreTypes
{
    private static readonly string[] DataAccessSuffixes = ["Repository", "Store"];

    private readonly INamedTypeSymbol dbContext;
    private readonly INamedTypeSymbol? contextFactory;
    private readonly INamedTypeSymbol? designTimeFactory;
    private readonly INamedTypeSymbol? entityConfiguration;
    private readonly INamedTypeSymbol? migration;
    private readonly INamedTypeSymbol? modelSnapshot;

    private EfCoreTypes(Compilation compilation, INamedTypeSymbol dbContext)
    {
        this.dbContext = dbContext;
        contextFactory = compilation.GetTypeByMetadataName("Microsoft.EntityFrameworkCore.IDbContextFactory`1");
        designTimeFactory = compilation.GetTypeByMetadataName("Microsoft.EntityFrameworkCore.Design.IDesignTimeDbContextFactory`1");
        entityConfiguration = compilation.GetTypeByMetadataName("Microsoft.EntityFrameworkCore.IEntityTypeConfiguration`1");
        migration = compilation.GetTypeByMetadataName("Microsoft.EntityFrameworkCore.Migrations.Migration");
        modelSnapshot = compilation.GetTypeByMetadataName("Microsoft.EntityFrameworkCore.Infrastructure.ModelSnapshot");
    }

    public static EfCoreTypes? From(Compilation compilation)
    {
        var contextType = compilation.GetTypeByMetadataName("Microsoft.EntityFrameworkCore.DbContext");
        return contextType is null ? null : new EfCoreTypes(compilation, contextType);
    }

    public bool IsDatabaseHandle(ITypeSymbol type) =>
        DerivesFrom(type, dbContext)
        || (type is INamedTypeSymbol named && SymbolEqualityComparer.Default.Equals(named.OriginalDefinition, contextFactory));

    public bool MayTouchDatabase(INamedTypeSymbol type) =>
        Array.Exists(DataAccessSuffixes, suffix => type.Name.EndsWith(suffix, StringComparison.Ordinal))
        || DerivesFrom(type, dbContext)
        || DerivesFrom(type, migration)
        || DerivesFrom(type, modelSnapshot)
        || Implements(type, entityConfiguration)
        || Implements(type, designTimeFactory);

    private static bool DerivesFrom(ITypeSymbol type, INamedTypeSymbol? baseType)
    {
        for (var current = type; current is not null && baseType is not null; current = current.BaseType)
        {
            if (SymbolEqualityComparer.Default.Equals(current.OriginalDefinition, baseType))
                return true;
        }
        return false;
    }

    private static bool Implements(ITypeSymbol type, INamedTypeSymbol? contract) =>
        contract is not null && type.AllInterfaces.Any(i => SymbolEqualityComparer.Default.Equals(i.OriginalDefinition, contract));
}
