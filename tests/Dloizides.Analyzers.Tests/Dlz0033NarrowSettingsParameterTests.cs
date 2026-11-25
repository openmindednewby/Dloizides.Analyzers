namespace Dloizides.Analyzers.Tests;

public class Dlz0033NarrowSettingsParameterTests
{
    private const string Dlz0033 = "DLZ0033";
    private const string Settings = "#nullable enable\nnamespace Sample { public sealed class AdmieSettings { public string TimeZoneId { get; set; } = \"\"; public string Url { get; set; } = \"\"; } public sealed class AppSettings { public AdmieSettings Admie { get; set; } = new(); public string Name { get; set; } = \"\"; } } ";
    private const string ForgivenChain = Settings + "namespace Sample { internal static class Locator { public static string Zone(AppSettings? appSettings) => Key(appSettings!.Admie.TimeZoneId); private static string Key(string zone) => zone; } }";

    [Theory]
    [InlineData(ForgivenChain)]
    [InlineData(Settings + "namespace Sample { public sealed class Job { public string Zone(AppSettings settings) => settings.Admie.TimeZoneId + settings.Admie.Url; } }")]
    [InlineData(Settings + "namespace Sample { public sealed class Job { public string? Zone(AppSettings? settings) => settings == null ? null : settings?.Admie.TimeZoneId; } }")]
    [InlineData(Settings + "namespace Sample { public sealed class Job { private readonly AppSettings _settings; public Job(AppSettings settings) { _settings = settings; } public string Zone() => _settings.Admie.Url; } }")]
    [InlineData(Settings + "namespace Sample { public sealed class Job { public string Zone(AdmieSettings admie) => admie.TimeZoneId; } }")]
    public async Task Analyze_WithOnlyOneValueOfSettingsParameterRead_ReportsDlz0033(string source)
    {
        var ids = await HouseRuleRunner.IdsAsync(new NarrowOptionsAnalyzer(), source);

        Assert.Equal(new[] { Dlz0033 }, ids);
    }

    [Theory]
    [InlineData(Settings + "namespace Sample { public sealed class Job { public string Both(AppSettings settings) => settings.Admie.Url + settings.Name; } }")]
    [InlineData(Settings + "namespace Sample { public sealed class Job { public string Zone(AppSettings settings) => Pass(settings); private static string Pass(AppSettings all) => all.Name + all.Admie.Url; } }")]
    [InlineData(Settings + "namespace Sample { public interface IZone { string Zone(AppSettings settings); } public sealed class Job : IZone { public string Zone(AppSettings settings) => settings.Name; } }")]
    [InlineData(Settings + "namespace Sample { public static class Extensions { public static string Zone(this AppSettings settings) => settings.Name; } }")]
    public async Task Analyze_WithWholeSettingsOrContractParameter_ReportsNothing(string source)
    {
        var ids = await HouseRuleRunner.IdsAsync(new NarrowOptionsAnalyzer(), source);

        Assert.Empty(ids);
    }

    [Fact]
    public async Task Analyze_WithForgivenChain_ReportsTheChainAtTheParameter()
    {
        var diagnostics = await HouseRuleRunner.RunAsync(new NarrowOptionsAnalyzer(), ForgivenChain, HouseRuleRunner.SamplePath);

        var diagnostic = Assert.Single(diagnostics);
        var span = diagnostic.Location.SourceSpan;
        Assert.Equal("AppSettings? appSettings", ForgivenChain.Substring(span.Start, span.Length));
        Assert.Contains("'Admie.TimeZoneId'", diagnostic.GetMessage(System.Globalization.CultureInfo.InvariantCulture));
    }
}
