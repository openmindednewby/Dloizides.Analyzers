namespace Dloizides.Analyzers.Tests;

public class ServiceDefaultsGuardAnalyzerTests
{
    private const string Dlz0021 = "DLZ0021";

    private const string FrameworkSetup = """
        namespace Microsoft.Extensions.DependencyInjection
        {
            public interface IServiceShape { }
            public static class SetupExtensions
            {
                public static IServiceShape AddAuthentication(this IServiceShape services) => services;
                public static IServiceShape AddJwtBearer(this IServiceShape services) => services;
                public static IServiceShape AddCors(this IServiceShape services) => services;
                public static IServiceShape AddRateLimiter(this IServiceShape services) => services;
                public static IServiceShape AddProblemDetails(this IServiceShape services) => services;
                public static IServiceShape AddResponseCompression(this IServiceShape services) => services;
            }
        }
        namespace Microsoft.AspNetCore.Builder
        {
            public interface IAppShape { }
            public static class PipelineExtensions
            {
                public static IAppShape UseExceptionHandler(this IAppShape app) => app;
            }
        }
        namespace Serilog
        {
            public interface IHostShape { }
            public static class HostExtensions
            {
                public static IHostShape UseSerilog(this IHostShape host) => host;
            }
        }
        namespace Shop.Setup
        {
            public static class LocalExtensions
            {
                public static Microsoft.Extensions.DependencyInjection.IServiceShape AddCors(this Microsoft.Extensions.DependencyInjection.IServiceShape services, int origins) => services;
                public static Serilog.IHostShape UseSerilog(this Serilog.IHostShape host, string sink) => host;
            }
        }
        """;

    private const string Usings = "using Microsoft.Extensions.DependencyInjection; using Microsoft.AspNetCore.Builder; using Serilog;\n";

    [Theory]
    [InlineData("services.AddAuthentication();")]
    [InlineData("services.AddJwtBearer();")]
    [InlineData("services.AddCors();")]
    [InlineData("services.AddRateLimiter();")]
    [InlineData("services.AddProblemDetails();")]
    [InlineData("services.AddResponseCompression();")]
    [InlineData("app.UseExceptionHandler();")]
    [InlineData("host.UseSerilog();")]
    public async Task Analyze_WithCrossCuttingSetupInService_ReportsDlz0021(string statement)
    {
        var ids = await PatternRunner.IdsAsync(Wiring(Usings, statement));

        Assert.Equal(new[] { Dlz0021 }, ids);
    }

    [Theory]
    [InlineData("Dloizides.ServiceDefaults")]
    [InlineData("Dloizides.ServiceDefaults.Security")]
    [InlineData("Shop.Api.Tests")]
    public async Task Analyze_WithServiceDefaultsOrTestAssembly_ReportsNothing(string assemblyName)
    {
        var ids = await PatternRunner.IdsAsync(Wiring(Usings, "services.AddJwtBearer(); app.UseExceptionHandler(); host.UseSerilog();"), assemblyName);

        Assert.Empty(ids);
    }

    [Theory]
    [InlineData("services.AddCors(3);")]
    [InlineData("host.UseSerilog(\"console\");")]
    public async Task Analyze_WithSameNamedProductMethod_ReportsNothing(string statement)
    {
        var ids = await PatternRunner.IdsAsync(Wiring("using Shop.Setup;\n", statement));

        Assert.Empty(ids);
    }

    private static string Wiring(string usings, string statement) =>
        usings + FrameworkSetup + "\npublic static class Wiring { public static void Configure(Microsoft.Extensions.DependencyInjection.IServiceShape services, Microsoft.AspNetCore.Builder.IAppShape app, Serilog.IHostShape host) { " + statement + " } }";
}
