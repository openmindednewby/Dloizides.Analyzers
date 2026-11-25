using Microsoft.CodeAnalysis;

namespace Dloizides.Analyzers.Tests;

public class ArgumentExceptionPackTests
{
    [Fact]
    public void ShippedSeverities_WithArgumentExceptionRule_IsError()
    {
        var severities = PackRuleFiles.ShippedSeverities();

        var severity = severities["CA2208"];

        Assert.Equal(ReportDiagnostic.Error, severity);
    }
}
