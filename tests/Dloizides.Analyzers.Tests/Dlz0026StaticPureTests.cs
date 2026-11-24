namespace Dloizides.Analyzers.Tests;

public class Dlz0026StaticPureTests
{
    private const string Dlz0026 = "DLZ0026";
    private const string Dlz0019 = "DLZ0019";
    private const string Stubs = """
        namespace Microsoft.Extensions.DependencyInjection
        {
            public interface IServiceCollection { }
            public static class ServiceProviderServiceExtensions { public static T GetRequiredService<T>(this System.IServiceProvider provider) => default!; }
        }
        namespace Microsoft.Extensions.Configuration { public interface IConfiguration { string this[string key] { get; } } }
        namespace Microsoft.AspNetCore.Builder { public interface IApplicationBuilder { System.IServiceProvider ApplicationServices { get; } } }
        namespace Sample
        {
            using Microsoft.Extensions.Configuration;
            using Microsoft.Extensions.DependencyInjection;
            using Microsoft.AspNetCore.Builder;

        """;
    private const string Close = "\n}\n";
    private const string FileRead = "public static class Paths { public static string Read(string path) => System.IO.File.ReadAllText(path); }";

    [Theory]
    [InlineData(FileRead)]
    [InlineData("public static class Paths { public static bool Has(string path) => System.IO.Directory.Exists(path); }")]
    [InlineData("public static class Paths { public static object Open(string path) => new System.IO.StreamReader(path); }")]
    [InlineData("public static class Web { public static object Client() => new System.Net.Http.HttpClient(); }")]
    [InlineData("public static class Shell { public static object Run() => System.Diagnostics.Process.Start(\"x\"); }")]
    [InlineData("public static class Clock { public static object Stamp() => System.DateTime.Now; }")]
    [InlineData("public static class Clock { public static object Stamp() => System.DateTime.UtcNow; }")]
    [InlineData("public static class Clock { public static object Stamp() => System.DateTime.Today; }")]
    [InlineData("public static class Clock { public static object Stamp() => System.DateTimeOffset.UtcNow; }")]
    [InlineData("public static class Clock { public static object Stamp() => System.DateTimeOffset.Now; }")]
    [InlineData("public static class Env { public static string Read() => System.Environment.GetEnvironmentVariable(\"X\"); }")]
    [InlineData("public static class Settings { public static string Read(IConfiguration configuration) => configuration[\"X\"]; }")]
    [InlineData("public static class Locator { public static object Find(System.IServiceProvider provider) => provider.GetRequiredService<object>(); }")]
    [InlineData("public static class Locator { public static object Find(System.IServiceProvider provider) => provider.GetService(typeof(object)); }")]
    [InlineData("public static class Text { public static bool IsLate(this string value) => System.DateTime.UtcNow.Hour > value.Length; }")]
    [InlineData("public sealed class Outer { public static class Inner { public static object Stamp() => System.DateTime.UtcNow; } }")]
    public async Task Analyze_WithImpureAccessInStaticClass_ReportsDlz0026(string type)
    {
        var ids = await HouseRuleRunner.IdsAsync(new StaticPureAnalyzer(), Stubs + type + Close);

        Assert.Equal(new[] { Dlz0026 }, ids);
    }

    [Theory]
    [InlineData("public static class Numbers { public static int Twice(int value) => value * 2; }")]
    [InlineData("public static class Keys { public const string Tenant = \"tenant\"; public static readonly int Max = 3; }")]
    [InlineData("public static class Timing { public static object Start() => System.Diagnostics.Stopwatch.StartNew(); }")]
    [InlineData("public static class Streams { public static object Wrap() => new System.IO.StreamReader(new System.IO.MemoryStream()); }")]
    [InlineData("public sealed class Clock { public object Stamp() => System.DateTime.UtcNow; }")]
    [InlineData("public static class Registration { public static IServiceCollection AddShop(this IServiceCollection services, IConfiguration configuration) { _ = configuration[\"X\"]; _ = System.Environment.GetEnvironmentVariable(\"Y\"); return services; } }")]
    [InlineData("public static class Pipeline { public static IApplicationBuilder UseShop(this IApplicationBuilder app) { _ = app.ApplicationServices.GetRequiredService<object>(); return app; } }")]
    public async Task Analyze_WithPureStaticOrHostSetup_ReportsNothing(string type)
    {
        var ids = await HouseRuleRunner.IdsAsync(new StaticPureAnalyzer(), Stubs + type + Close);

        Assert.Empty(ids);
    }

    [Fact]
    public async Task Analyze_WithGeneratedPath_ReportsNothing()
    {
        var ids = await HouseRuleRunner.IdsAsync(new StaticPureAnalyzer(), Stubs + FileRead + Close, "/repo/src/obj/Debug/Sample.cs");

        Assert.Empty(ids);
    }

    [Fact]
    public async Task Analyze_WithViolationsAtBaselineCount_ReportsNothing()
    {
        var baseline = new InMemoryAdditionalText("/repo/static-pure-baseline.txt", "src/Sample.cs 1");

        var ids = await HouseRuleRunner.IdsAsync(new StaticPureAnalyzer(), Stubs + FileRead + Close, HouseRuleRunner.SamplePath, baseline);

        Assert.Empty(ids);
    }

    [Fact]
    public async Task Analyze_WithViolationsBelowBaselineCount_ReportsDlz0019()
    {
        var baseline = new InMemoryAdditionalText("/repo/static-pure-baseline.txt", "src/Sample.cs 2");

        var ids = await HouseRuleRunner.IdsAsync(new StaticPureAnalyzer(), Stubs + FileRead + Close, HouseRuleRunner.SamplePath, baseline);

        Assert.Equal(new[] { Dlz0019 }, ids);
    }
}
