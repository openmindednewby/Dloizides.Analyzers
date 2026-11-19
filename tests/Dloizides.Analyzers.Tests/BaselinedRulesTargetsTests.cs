using System.Diagnostics;
using System.Reflection;
using System.Text.Json;

namespace Dloizides.Analyzers.Tests;

public sealed class BaselinedRulesTargetsTests : IDisposable
{
    private const string BaselineFile = "member-order-baseline.txt";
    private const int MsBuildTimeoutMs = 120_000;

    private readonly string _directory = Directory.CreateTempSubdirectory("dlz-targets-").FullName;

    [Fact]
    public void BaselinedRulesAsErrors_WithOptInAndBaselineFile_EscalatesDlz0016To0019()
    {
        File.WriteAllText(Path.Combine(_directory, BaselineFile), string.Empty);

        var properties = Evaluate("true");

        Assert.Contains("DLZ0016;DLZ0017;DLZ0018;DLZ0019", properties.WarningsAsErrors, StringComparison.Ordinal);
        Assert.DoesNotContain("DLZ0017", properties.WarningsNotAsErrors, StringComparison.Ordinal);
    }

    [Fact]
    public void BaselinedRulesAsErrors_WithOptInAndMissingBaselineFile_KeepsWarnings()
    {
        var properties = Evaluate("true");

        Assert.DoesNotContain("DLZ0017", properties.WarningsAsErrors, StringComparison.Ordinal);
        Assert.Contains("DLZ0017", properties.WarningsNotAsErrors, StringComparison.Ordinal);
    }

    [Fact]
    public void BaselinedRulesAsErrors_WithoutOptIn_KeepsWarnings()
    {
        File.WriteAllText(Path.Combine(_directory, BaselineFile), string.Empty);

        var properties = Evaluate("false");

        Assert.DoesNotContain("DLZ0017", properties.WarningsAsErrors, StringComparison.Ordinal);
        Assert.Contains("DLZ0017", properties.WarningsNotAsErrors, StringComparison.Ordinal);
    }

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    private static string TargetsPath() =>
        Path.Combine(
            typeof(BaselinedRulesTargetsTests).Assembly
                .GetCustomAttributes<AssemblyMetadataAttribute>()
                .First(attribute => attribute.Key == "BuildTransitivePath")
                .Value!,
            "Dloizides.Analyzers.targets");

    private TargetsProperties Evaluate(string optIn)
    {
        var project = Path.Combine(_directory, "probe.proj");
        File.WriteAllText(project, $"""
            <Project>
              <PropertyGroup>
                <DloizidesBaselinedRulesAsErrors>{optIn}</DloizidesBaselinedRulesAsErrors>
              </PropertyGroup>
              <ItemGroup>
                <AdditionalFiles Include="{Path.Combine(_directory, BaselineFile)}" />
              </ItemGroup>
              <Import Project="{TargetsPath()}" />
              <Target Name="CoreCompile" />
            </Project>
            """);
        var start = new ProcessStartInfo("dotnet", ["msbuild", project, "-t:CoreCompile", "-getProperty:WarningsAsErrors", "-getProperty:WarningsNotAsErrors"])
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        using var process = Process.Start(start)!;
        var output = process.StandardOutput.ReadToEnd();
        Assert.True(process.WaitForExit(MsBuildTimeoutMs), "dotnet msbuild did not finish");
        Assert.True(process.ExitCode == 0, output);
        var values = JsonDocument.Parse(output).RootElement.GetProperty("Properties");
        return new TargetsProperties(
            values.GetProperty("WarningsAsErrors").GetString() ?? string.Empty,
            values.GetProperty("WarningsNotAsErrors").GetString() ?? string.Empty);
    }

    private sealed record TargetsProperties(string WarningsAsErrors, string WarningsNotAsErrors);
}
