namespace Dloizides.Analyzers.Tests;

public class HttpClientFactoryAnalyzerTests
{
    private const string Dlz0006 = "DLZ0006";

    private const string Registration = """
        using System;
        using System.Net.Http;
        public interface IServiceCollection { }
        public static class Registration
        {
            public static IServiceCollection AddHttpClient(this IServiceCollection services, string name, Action<HttpClient> configure) => services;
            public static IServiceCollection AddTypedClient(this IServiceCollection services, Func<HttpClient, object> factory) => services;
            public static IServiceCollection ConfigurePrimaryHttpMessageHandler(this IServiceCollection services, Func<HttpMessageHandler> handler) => services;
            public static IServiceCollection AddSingleton(this IServiceCollection services, Func<IServiceProvider, object> factory) => services;
        }
        """;

    [Theory]
    [InlineData("var client = new HttpClient();")]
    [InlineData("HttpClient client = new();")]
    [InlineData("using var client = new HttpClient(new HttpClientHandler(), disposeHandler: true);")]
    public async Task Analyze_WithHttpClientConstructedInMethod_ReportsDlz0006(string statement)
    {
        var source = $"using System.Net.Http; public class Pinger {{ public void Ping() {{ {statement} }} }}";

        var ids = await PatternRunner.IdsAsync(source);

        Assert.Equal(new[] { Dlz0006 }, ids);
    }

    [Fact]
    public async Task Analyze_WithHttpClientInNonRegistrationFactoryLambda_ReportsDlz0006()
    {
        var source = Registration + "public static class Wiring { public static void Add(IServiceCollection s) => s.AddSingleton(sp => new HttpClient()); }";

        var ids = await PatternRunner.IdsAsync(source);

        Assert.Equal(new[] { Dlz0006 }, ids);
    }

    [Theory]
    [InlineData("s.AddTypedClient(http => new HttpClient(new HttpClientHandler()));")]
    [InlineData("s.ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler());")]
    [InlineData("s.AddHttpClient(\"k8s\", client => client.Timeout = TimeSpan.FromSeconds(5));")]
    public async Task Analyze_WithRegistrationLambda_ReportsNothing(string statement)
    {
        var source = Registration + $"public static class Wiring {{ public static void Add(IServiceCollection s) {{ {statement} }} }}";

        var ids = await PatternRunner.IdsAsync(source);

        Assert.Empty(ids);
    }

    [Fact]
    public async Task Analyze_WithTestAssembly_ReportsNothing()
    {
        var source = "using System.Net.Http; public class FakeApi { public HttpClient Create() => new HttpClient(); }";

        var ids = await PatternRunner.IdsAsync(source, "Shop.Api.Tests");

        Assert.Empty(ids);
    }
}
