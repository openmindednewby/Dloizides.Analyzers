namespace Dloizides.Analyzers.Tests;

public class CompositionRootAnalyzerTests
{
    private const string Dlz0013 = "DLZ0013";

    private const string Wiring = """
        public interface IServiceCollection { }
        public static class Registration
        {
            public static IServiceCollection AddSingleton(this IServiceCollection services, System.Func<System.IServiceProvider, object> factory) => services;
            public static IServiceCollection AddScoped(this IServiceCollection services, System.Func<object> factory) => services;
        }
        public class OrderService { }
        public class OrderRepository { }
        public class Order { }
        """;

    [Theory]
    [InlineData("public class Checkout { public object Run() => new OrderService(); }")]
    [InlineData("public class Checkout { private readonly OrderRepository _orders = new(); public object Orders => _orders; }")]
    public async Task Analyze_WithServiceConstructedOutsideRoot_ReportsDlz0013(string statement)
    {
        var source = Wiring + statement;

        var ids = await PatternRunner.IdsAsync(source);

        Assert.Equal(new[] { Dlz0013 }, ids);
    }

    [Theory]
    [InlineData("public static class Wiring { public static void Add(IServiceCollection s) => s.AddSingleton(sp => new OrderService()); }")]
    [InlineData("public static class Wiring { public static IServiceCollection Add(this Registry r) => r.Services.AddScoped(() => new OrderService()); } public class Registry { public IServiceCollection Services { get; } = null!; }")]
    [InlineData("public class Checkout { public object Run() => new Order(); }")]
    [InlineData("public class Checkout { public object Run() => new System.Net.Http.HttpClientHandler(); }")]
    [InlineData("public class PricingService { public static PricingService Create() => new PricingService(); }")]
    public async Task Analyze_WithCompositionRootOrNonService_ReportsNothing(string statement)
    {
        var source = Wiring + statement;

        var ids = await PatternRunner.IdsAsync(source);

        Assert.Empty(ids);
    }

    [Fact]
    public async Task Analyze_WithProgramFile_ReportsNothing()
    {
        var source = Wiring + "public static class Bootstrap { public static object Build() => new OrderService(); }";

        var ids = await PatternRunner.IdsAsync(source, "Shop.Api", "src/Shop.Api/Program.cs");

        Assert.Empty(ids);
    }

    [Fact]
    public async Task Analyze_WithTestAssembly_ReportsNothing()
    {
        var source = Wiring + "public class Checkout { public object Run() => new OrderService(); }";

        var ids = await PatternRunner.IdsAsync(source, "Shop.Api.Tests");

        Assert.Empty(ids);
    }
}
