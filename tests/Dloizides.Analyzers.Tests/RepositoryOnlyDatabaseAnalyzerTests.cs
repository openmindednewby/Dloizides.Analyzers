namespace Dloizides.Analyzers.Tests;

public class RepositoryOnlyDatabaseAnalyzerTests
{
    private const string Dlz0015 = "DLZ0015";

    private const string Prelude = """
        namespace Microsoft.EntityFrameworkCore
        {
            public class DbContext { }
            public interface IDbContextFactory<TContext> where TContext : DbContext { TContext CreateDbContext(); }
            public interface IEntityTypeConfiguration<TEntity> where TEntity : class { }
        }
        namespace Microsoft.EntityFrameworkCore.Migrations { public abstract class Migration { } }
        namespace Microsoft.EntityFrameworkCore.Infrastructure { public abstract class ModelSnapshot { } }
        namespace Microsoft.EntityFrameworkCore.Design { public interface IDesignTimeDbContextFactory<TContext> { } }
        namespace Shop
        {
            using Microsoft.EntityFrameworkCore;
            public class ShopDb : DbContext { }
            public sealed class AuditDb : ShopDb { }
            public interface IOrderRepository { }
            public sealed class Order { }

        """;

    private const string Epilogue = "\n}";

    [Theory]
    [InlineData("public sealed class OrderService { public OrderService(ShopDb db) { } }")]
    [InlineData("public sealed class OrderService(ShopDb db) { public bool Ready => db is not null; }")]
    [InlineData("public sealed class OrdersController { public OrdersController(IDbContextFactory<ShopDb> factory) { } }")]
    [InlineData("public sealed class CleanupJob { private ShopDb? db; public void Attach(ShopDb context) { db = context; } public bool Ready => db is not null; }")]
    [InlineData("public sealed class AuditConsumer { public AuditConsumer(AuditDb db) { } }")]
    [InlineData("public sealed class RepositoryHelper { public RepositoryHelper(ShopDb db) { } }")]
    public async Task Analyze_WithDbContextOutsideDataAccessClass_ReportsDlz0015(string source)
    {
        var ids = await PatternRunner.IdsAsync(Prelude + source + Epilogue);

        Assert.Equal(new[] { Dlz0015 }, ids);
    }

    [Theory]
    [InlineData("public sealed class OrderRepository { public OrderRepository(ShopDb db) { } }")]
    [InlineData("public sealed class EfJobStore(IDbContextFactory<ShopDb> factory) { public bool Ready => factory is not null; }")]
    [InlineData("public class GenericRepository<T> { private readonly ShopDb db; public GenericRepository(ShopDb db) { this.db = db; } public bool Ready => db is not null; }")]
    [InlineData("public sealed class ReportDb : DbContext { public ReportDb(ShopDb source) { } }")]
    [InlineData("public sealed class OrderConfiguration : IEntityTypeConfiguration<Order> { public OrderConfiguration(ShopDb db) { } }")]
    [InlineData("public sealed class AddOrders : Microsoft.EntityFrameworkCore.Migrations.Migration { public AddOrders(ShopDb db) { } }")]
    [InlineData("public sealed class ShopSnapshot : Microsoft.EntityFrameworkCore.Infrastructure.ModelSnapshot { public ShopSnapshot(ShopDb db) { } }")]
    [InlineData("public sealed class ShopDbFactory : Microsoft.EntityFrameworkCore.Design.IDesignTimeDbContextFactory<ShopDb> { public ShopDbFactory(ShopDb db) { } }")]
    [InlineData("public sealed class OrderService { public OrderService(IOrderRepository orders) { } }")]
    public async Task Analyze_WithDbContextOnlyInDataAccessClass_ReportsNothing(string source)
    {
        var ids = await PatternRunner.IdsAsync(Prelude + source + Epilogue);

        Assert.Empty(ids);
    }

    [Fact]
    public async Task Analyze_WithConstructorAndFieldHoldingTheContext_ReportsOnce()
    {
        var source = Prelude + "public sealed class OrderService { private readonly ShopDb db; public OrderService(ShopDb db) { this.db = db; } public bool Ready => db is not null; }" + Epilogue;

        var ids = await PatternRunner.IdsAsync(source);

        Assert.Equal(new[] { Dlz0015 }, ids);
    }

    [Fact]
    public async Task Analyze_WithDbContextInTestAssembly_ReportsNothing()
    {
        var source = Prelude + "public sealed class OrderServiceFixture { public OrderServiceFixture(ShopDb db) { } }" + Epilogue;

        var ids = await PatternRunner.IdsAsync(source, "Shop.Api.Tests");

        Assert.Empty(ids);
    }

    [Fact]
    public async Task Analyze_WithoutEntityFrameworkCore_ReportsNothing()
    {
        var source = "public class DbContext { } public sealed class OrderService { public OrderService(DbContext db) { } }";

        var ids = await PatternRunner.IdsAsync(source);

        Assert.Empty(ids);
    }
}
