using System.Globalization;

namespace Dloizides.Analyzers.Tests;

public class Dlz0017MemberOrderTests
{
    private const string Dlz0017 = "DLZ0017";

    [Theory]
    [InlineData("public class Sample { private int a; public int b; }")]
    [InlineData("public class Sample { private void A() { } public void B() { } }")]
    [InlineData("public class Sample { protected void A() { } internal void B() { } }")]
    [InlineData("public class Sample { private Sample(int a) { } public Sample() { } }")]
    [InlineData("public class Sample { private int A { get; set; } protected internal int B { get; set; } }")]
    [InlineData("public class Sample { private protected void A() { } protected void B() { } }")]
    [InlineData("public class Sample { protected internal void A() { } internal void B() { } }")]
    [InlineData("public class Sample { void A() { } public void B() { } }")]
    [InlineData("public class Sample { private const int A = 1; public const int B = 2; }")]
    [InlineData("public class Sample { public int A => 0; private int B => 1; public int C => 2; }")]
    [InlineData("public struct Sample { private void A() { } public void B() { } }")]
    [InlineData("public record Sample { private void A() { } public void B() { } }")]
    [InlineData("public record struct Sample { private void A() { } public void B() { } }")]
    [InlineData("public class Outer { public class Sample { private void A() { } public void B() { } } }")]
    public async Task Analyze_WithLessAccessibleMemberAbove_ReportsDlz0017(string source)
    {
        var ids = await HouseRuleRunner.IdsAsync(new MemberOrderAnalyzer(), source);

        Assert.Equal(new[] { Dlz0017 }, ids);
    }

    [Theory]
    [InlineData("public class Sample { public void A() { } internal void B() { } protected internal void C() { } protected void D() { } private protected void E() { } private void F() { } }")]
    [InlineData("public class Sample { internal protected void A() { } protected internal void B() { } }")]
    [InlineData("public class Sample { private int a; public void B() { a++; } }")]
    [InlineData("public class Sample { private void A() { } public sealed class Inner { public void B() { } } }")]
    [InlineData("public class Sample { static Sample() { } public Sample() { } }")]
    [InlineData("public class Sample : System.IDisposable { void System.IDisposable.Dispose() { } public void B() { } }")]
    [InlineData("public class Sample { private event System.Action A; public event System.Action B; }")]
    [InlineData("public interface ISample { private void A() { } public void B(); }")]
    [InlineData("public enum Sample { A, B }")]
    public async Task Analyze_WithMembersInAccessOrder_ReportsNothing(string source)
    {
        var ids = await HouseRuleRunner.IdsAsync(new MemberOrderAnalyzer(), source);

        Assert.Empty(ids);
    }

    [Fact]
    public async Task Analyze_WithOneMemberAboveTwoMoreAccessible_ReportsOnce()
    {
        var source = "public class Sample { private void A() { } public void B() { } public void C() { } }";

        var ids = await HouseRuleRunner.IdsAsync(new MemberOrderAnalyzer(), source);

        Assert.Equal(new[] { Dlz0017 }, ids);
    }

    [Fact]
    public async Task Analyze_WithTwoMembersAboveOneMoreAccessible_ReportsTwice()
    {
        var source = "public class Sample { private void A() { } private void B() { } public void C() { } }";

        var ids = await HouseRuleRunner.IdsAsync(new MemberOrderAnalyzer(), source);

        Assert.Equal(new[] { Dlz0017, Dlz0017 }, ids);
    }

    [Fact]
    public async Task Analyze_WithMisorderedMethods_NamesBothMembers()
    {
        var source = "public class Sample { private void Load() { } public void Run() { } }";

        var diagnostics = await HouseRuleRunner.RunAsync(new MemberOrderAnalyzer(), source, HouseRuleRunner.SamplePath);

        Assert.Equal(
            "private method 'Load' is above the more accessible public method 'Run': order each kind public, internal, protected internal, protected, private protected, private (MEMBER-ORDER-1)",
            Assert.Single(diagnostics).GetMessage(CultureInfo.InvariantCulture));
    }

    [Fact]
    public async Task Analyze_WithMisorderInGeneratedPath_ReportsNothing()
    {
        var source = "public class Sample { private void A() { } public void B() { } }";

        var ids = await HouseRuleRunner.IdsAsync(new MemberOrderAnalyzer(), source, "/repo/src/obj/Debug/Sample.cs");

        Assert.Empty(ids);
    }
}
