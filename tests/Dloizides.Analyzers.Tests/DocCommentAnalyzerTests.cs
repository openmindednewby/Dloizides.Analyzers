using Microsoft.CodeAnalysis;

namespace Dloizides.Analyzers.Tests;

public class DocCommentAnalyzerTests
{
    private const string Dlz0002 = "DLZ0002";
    private const string Dlz0003 = "DLZ0003";
    private const string Summary = "/// <summary>Prices a menu.</summary>\n";

    public static TheoryData<DocumentationMode> Modes => new() { DocumentationMode.Parse, DocumentationMode.None };

    [Theory]
    [MemberData(nameof(Modes))]
    public async Task SummaryOnPublicMember_NoDiagnostic(DocumentationMode mode)
    {
        var ids = await AnalyzerRunner.IdsAsync($"public class A {{\n{Summary}public int Price() => 1;\n}}", mode: mode);

        Assert.Empty(ids);
    }

    [Theory]
    [MemberData(nameof(Modes))]
    public async Task SummaryOnProtectedMember_NoDiagnostic(DocumentationMode mode)
    {
        var ids = await AnalyzerRunner.IdsAsync($"public class A {{\n{Summary}protected internal int Price() => 1;\n}}", mode: mode);

        Assert.Empty(ids);
    }

    [Theory]
    [MemberData(nameof(Modes))]
    public async Task SummaryOnPrivateMethod_ReportsDlz0002(DocumentationMode mode)
    {
        var ids = await AnalyzerRunner.IdsAsync($"public class A {{\n{Summary}private int Price() => 1;\n}}", mode: mode);

        Assert.Equal(new[] { Dlz0002 }, ids);
    }

    [Theory]
    [MemberData(nameof(Modes))]
    public async Task SummaryOnInternalClass_ReportsDlz0002(DocumentationMode mode)
    {
        var ids = await AnalyzerRunner.IdsAsync($"{Summary}internal class A {{ }}", mode: mode);

        Assert.Equal(new[] { Dlz0002 }, ids);
    }

    [Fact]
    public async Task SummaryOnPublicMemberOfInternalClass_ReportsDlz0002()
    {
        var ids = await AnalyzerRunner.IdsAsync($"internal class A {{\n{Summary}public int Price() => 1;\n}}");

        Assert.Equal(new[] { Dlz0002 }, ids);
    }

    [Fact]
    public async Task SummaryOnPrivateFieldWithAttribute_ReportsDlz0002()
    {
        var ids = await AnalyzerRunner.IdsAsync($"public class A {{\n{Summary}[System.Obsolete]\nprivate int price;\n}}");

        Assert.Equal(new[] { Dlz0002 }, ids);
    }

    [Fact]
    public async Task SummaryOnLocalFunction_ReportsDlz0002()
    {
        var ids = await AnalyzerRunner.IdsAsync($"public class A {{ public int M() {{\n{Summary}int Local() => 1;\nreturn Local(); }} }}");

        Assert.Contains(Dlz0002, ids);
    }

    [Fact]
    public async Task InheritDocOnExplicitInterfaceImplementation_NoDiagnostic()
    {
        var source = "public interface I { int P(); }\npublic class A : I {\n/// <inheritdoc />\nint I.P() => 1;\n}";

        var ids = await AnalyzerRunner.IdsAsync(source);

        Assert.Empty(ids);
    }

    [Theory]
    [MemberData(nameof(Modes))]
    public async Task SummaryParamReturnsWithinThreeLines_NoDiagnostic(DocumentationMode mode)
    {
        var source = "public class A {\n/// <summary>\n/// Adds <paramref name=\"a\"/> to a <see cref=\"int\"/>.\n/// </summary>\n/// <param name=\"a\">First.</param>\n/// <returns>The <c>sum</c>.</returns>\npublic int Add(int a) => a;\n}";

        var ids = await AnalyzerRunner.IdsAsync(source, mode: mode);

        Assert.Empty(ids);
    }

    [Theory]
    [MemberData(nameof(Modes))]
    public async Task FourContentLines_ReportsDlz0003(DocumentationMode mode)
    {
        var source = "public class A {\n/// <summary>\n/// one\n/// two\n/// three\n/// four\n/// </summary>\npublic int Add(int a) => a;\n}";

        var ids = await AnalyzerRunner.IdsAsync(source, mode: mode);

        Assert.Equal(new[] { Dlz0003 }, ids);
    }

    [Fact]
    public async Task SummaryParamParamReturns_FourLines_ReportsDlz0003()
    {
        var source = "public class A {\n/// <summary>Adds.</summary>\n/// <param name=\"a\">A.</param>\n/// <param name=\"b\">B.</param>\n/// <returns>Sum.</returns>\npublic int Add(int a, int b) => a + b;\n}";

        var ids = await AnalyzerRunner.IdsAsync(source);

        Assert.Equal(new[] { Dlz0003 }, ids);
    }

    [Theory]
    [InlineData("/// <remarks>Local Tiltfile sets false.</remarks>")]
    [InlineData("/// <typeparam name=\"T\">T.</typeparam>")]
    [InlineData("/// <example>Add(1)</example>")]
    [InlineData("/// <exception cref=\"System.Exception\">Never.</exception>")]
    [InlineData("/// <value>The price.</value>")]
    public async Task BannedElement_ReportsDlz0003(string bannedLine)
    {
        var source = $"public class A {{\n{Summary}{bannedLine}\npublic int Add<T>(int a) => a;\n}}";

        var ids = await AnalyzerRunner.IdsAsync(source);

        Assert.Equal(new[] { Dlz0003 }, ids);
    }

    [Fact]
    public async Task LongDocOnPrivateMember_ReportsBothDiagnostics()
    {
        var source = "public class A {\n/// <summary>Adds.</summary>\n/// <remarks>Ticket EMS-1.</remarks>\nprivate int Add(int a) => a;\n}";

        var ids = await AnalyzerRunner.IdsAsync(source);

        Assert.Equal(new[] { Dlz0002, Dlz0003 }, ids.OrderBy(id => id));
    }

    [Fact]
    public async Task MultiLineDocBlockOverCap_ReportsDlz0003()
    {
        var source = "public class A {\n/**\n * one\n * two\n * three\n * four\n */\npublic int Add(int a) => a;\n}";

        var ids = await AnalyzerRunner.IdsAsync(source);

        Assert.Equal(new[] { Dlz0003 }, ids);
    }
}
