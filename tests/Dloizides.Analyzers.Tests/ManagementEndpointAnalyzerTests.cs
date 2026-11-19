namespace Dloizides.Analyzers.Tests;

public class ManagementEndpointAnalyzerTests
{
    private const string Dlz0020 = "DLZ0020";

    private const string Mapping = """
        public interface IEndpointRouteBuilder { }
        public static class Mapping
        {
            public static IEndpointRouteBuilder MapMetrics(this IEndpointRouteBuilder app, string path = "/metrics") => app;
            public static IEndpointRouteBuilder MapPrometheusScrapingEndpoint(this IEndpointRouteBuilder app) => app;
            public static IEndpointRouteBuilder MapHealthChecks(this IEndpointRouteBuilder app, string path) => app;
            public static IEndpointRouteBuilder MapHealthCheckEndpoints(this IEndpointRouteBuilder app) => app;
        }
        """;

    [Theory]
    [InlineData("app.MapMetrics();")]
    [InlineData("app.MapPrometheusScrapingEndpoint();")]
    [InlineData("app.MapHealthChecks(\"/health/live\");")]
    public async Task Analyze_WithManagementEndpointMappedInService_ReportsDlz0020(string statement)
    {
        var source = Mapping + $"public static class Wiring {{ public static void Map(IEndpointRouteBuilder app) {{ {statement} }} }}";

        var ids = await PatternRunner.IdsAsync(source);

        Assert.Equal(new[] { Dlz0020 }, ids);
    }

    [Fact]
    public async Task Analyze_WithSharedPackageMapping_ReportsNothing()
    {
        var source = Mapping + "public static class Wiring { public static void Map(IEndpointRouteBuilder app) => app.MapHealthCheckEndpoints(); }";

        var ids = await PatternRunner.IdsAsync(source);

        Assert.Empty(ids);
    }

    [Theory]
    [InlineData("Metrics.Client")]
    [InlineData("ServiceDefaults.HealthChecks")]
    [InlineData("Dloizides.ServiceDefaults")]
    [InlineData("Shop.Api.Tests")]
    public async Task Analyze_WithOwnerOrTestAssembly_ReportsNothing(string assemblyName)
    {
        var source = Mapping + "public static class Wiring { public static void Map(IEndpointRouteBuilder app) => app.MapMetrics(); }";

        var ids = await PatternRunner.IdsAsync(source, assemblyName);

        Assert.Empty(ids);
    }
}
