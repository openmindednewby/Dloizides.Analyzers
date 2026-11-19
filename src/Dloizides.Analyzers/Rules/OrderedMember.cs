using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Dloizides.Analyzers.Rules;

internal sealed class OrderedMember
{
    private const int PublicRank = 0;
    private const int InternalRank = 1;
    private const int ProtectedInternalRank = 2;
    private const int ProtectedRank = 3;
    private const int PrivateProtectedRank = 4;
    private const int PrivateRank = 5;

    private static readonly string[] AccessNames =
        { "public", "internal", "protected internal", "protected", "private protected", "private" };

    private OrderedMember(string kind, SyntaxToken identifier, SyntaxTokenList modifiers)
    {
        Kind = kind;
        Name = identifier.ValueText;
        Rank = RankOf(modifiers);
        Location = identifier.GetLocation();
    }

    public string Kind { get; }

    public string Name { get; }

    public int Rank { get; }

    public Location Location { get; }

    public string Access => AccessNames[Rank];

    public static OrderedMember? From(MemberDeclarationSyntax member) => member switch
    {
        FieldDeclarationSyntax field => new OrderedMember("field", field.Declaration.Variables[0].Identifier, field.Modifiers),
        PropertyDeclarationSyntax property when property.ExplicitInterfaceSpecifier is null =>
            new OrderedMember("property", property.Identifier, property.Modifiers),
        MethodDeclarationSyntax method when method.ExplicitInterfaceSpecifier is null =>
            new OrderedMember("method", method.Identifier, method.Modifiers),
        ConstructorDeclarationSyntax constructor when !constructor.Modifiers.Any(SyntaxKind.StaticKeyword) =>
            new OrderedMember("constructor", constructor.Identifier, constructor.Modifiers),
        _ => null,
    };

    private static int RankOf(SyntaxTokenList modifiers)
    {
        var isProtected = modifiers.Any(SyntaxKind.ProtectedKeyword);
        var isInternal = modifiers.Any(SyntaxKind.InternalKeyword);
        if (modifiers.Any(SyntaxKind.PublicKeyword))
            return PublicRank;
        if (isProtected && isInternal)
            return ProtectedInternalRank;
        if (isProtected && modifiers.Any(SyntaxKind.PrivateKeyword))
            return PrivateProtectedRank;
        if (isInternal)
            return InternalRank;
        return isProtected ? ProtectedRank : PrivateRank;
    }
}
