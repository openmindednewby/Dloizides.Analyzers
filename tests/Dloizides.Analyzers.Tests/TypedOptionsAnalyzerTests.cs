namespace Dloizides.Analyzers.Tests;

public class TypedOptionsAnalyzerTests
{
    private const string Dlz0007 = "DLZ0007";

    private const string Configuration = """
        using Microsoft.Extensions.Configuration;
        namespace Microsoft.Extensions.Configuration
        {
            public interface IConfiguration { string? this[string key] { get; } IConfigurationSection GetSection(string key); }
            public interface IConfigurationSection : IConfiguration { }
        }
        public interface IServiceCollection { }
        public interface IOptions<T> { T Value { get; } }
        public sealed class MailOptions { public string Host { get; set; } = ""; }
        public static class OptionsRegistration
        {
            public static IServiceCollection Configure<T>(this IServiceCollection services, IConfiguration section) => services;
        }

        """;

    [Theory]
    [InlineData("public class Mailer { private readonly IConfiguration _configuration; public Mailer(IConfiguration configuration) { _configuration = configuration; } }")]
    [InlineData("public class Mailer(IConfiguration configuration) { public string? Host => configuration[\"Mail:Host\"]; }")]
    [InlineData("public class Mailer { public Mailer(IConfigurationSection section) { } }")]
    [InlineData("public class Mailer { private static IConfiguration? _current; }")]
    public async Task Analyze_WithConfigurationInjectedIntoType_ReportsDlz0007Once(string type)
    {
        var ids = await PatternRunner.IdsAsync(Configuration + type);

        Assert.Equal(new[] { Dlz0007 }, ids);
    }

    [Theory]
    [InlineData("public static class MailRegistration { public static IServiceCollection AddMail(this IServiceCollection services, IConfiguration configuration) => services.Configure<MailOptions>(configuration.GetSection(\"Mail\")); }")]
    [InlineData("public class Mailer(IOptions<MailOptions> options) { public string Host => options.Value.Host; }")]
    [InlineData("public class Startup { public Startup(IConfiguration configuration) { } }")]
    [InlineData("public class Mailer { public string Read(IConfiguration configuration) => configuration[\"Mail:Host\"] ?? \"\"; }")]
    public async Task Analyze_WithOptionsOrRegistrationShape_ReportsNothing(string type)
    {
        var ids = await PatternRunner.IdsAsync(Configuration + type);

        Assert.Empty(ids);
    }

    [Fact]
    public async Task Analyze_WithTestAssembly_ReportsNothing()
    {
        var source = Configuration + "public class Fixture { public Fixture(IConfiguration configuration) { } }";

        var ids = await PatternRunner.IdsAsync(source, "Shop.Api.Tests");

        Assert.Empty(ids);
    }
}
