using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace NativeUI.Xaml.Compiler;

/// <summary>Emits URI construction from a validated XAML literal.</summary>
internal sealed class UriConverter : XamlValueConverter
{
    public override bool TryConvert(string literal, out ExpressionSyntax? value)
    {
        value = null;
        if (!Uri.TryCreate(literal, UriKind.RelativeOrAbsolute, out Uri? uri))
        {
            return false;
        }

        value = ObjectCreationExpression(IdentifierName("Uri"))
            .WithArgumentList(ArgumentList(SeparatedList([
                Argument(LiteralExpression(SyntaxKind.StringLiteralExpression, Literal(uri.OriginalString))),
                Argument(MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
                    IdentifierName("UriKind"), IdentifierName("RelativeOrAbsolute")))
            ])));
        return true;
    }
}
