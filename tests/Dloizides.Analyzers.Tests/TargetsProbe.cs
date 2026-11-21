using System.Diagnostics;
using System.Reflection;
using System.Text.Json;

namespace Dloizides.Analyzers.Tests;

internal static class TargetsProbe
{
    private const int MsBuildTimeoutMs = 120_000;

    public static TargetsProperties Evaluate(string directory, string propertiesXml, string itemsXml = "")
    {
        var project = Path.Combine(directory, "probe.proj");
        File.WriteAllText(project, $"""
            <Project>
              <PropertyGroup>
                {propertiesXml}
              </PropertyGroup>
              <ItemGroup>
                {itemsXml}
              </ItemGroup>
              <Import Project="{TargetsPath()}" />
              <Target Name="CoreCompile" />
            </Project>
            """);
        var start = new ProcessStartInfo("dotnet", ["msbuild", project, "-t:CoreCompile", "-getProperty:WarningsAsErrors", "-getProperty:WarningsNotAsErrors"])
        {
            UseShellExecute = false,
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

    private static string TargetsPath() =>
        Path.Combine(
            typeof(TargetsProbe).Assembly
                .GetCustomAttributes<AssemblyMetadataAttribute>()
                .First(attribute => string.Equals(attribute.Key, "BuildTransitivePath", StringComparison.Ordinal))
                .Value!,
            "Dloizides.Analyzers.targets");
}

internal sealed record TargetsProperties(string WarningsAsErrors, string WarningsNotAsErrors);
