using System.Globalization;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace NativeUI.Xaml.Compiler;

/// <summary>
/// Converts the literal text of a XAML attribute to the C# expression that creates its value, at compile time. Generated
/// code contains only the resulting expression, so views parse no attribute text at run time.
/// </summary>
internal abstract class XamlValueConverter
{
    /// <summary>
    /// Attempts to convert <paramref name="literal"/>.
    /// </summary>
    /// <param name="literal">The attribute text.</param>
    /// <param name="value">When this method returns <see langword="true"/>, the expression that creates the value.</param>
    /// <returns><see langword="true"/> when <paramref name="literal"/> is valid for this converter.</returns>
    public abstract bool TryConvert(string literal, out ExpressionSyntax? value);

    /// <summary>
    /// Parses a finite number using the invariant culture.
    /// </summary>
    protected static bool TryParseNumber(string text, out double value)
    {
        return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value) && double.IsFinite(value);
    }

    /// <summary>
    /// Splits a comma-separated attribute into trimmed tokens.
    /// </summary>
    protected static string[] SplitList(string literal)
    {
        string[] tokens = literal.Split(',');
        for (int i = 0; i < tokens.Length; i++)
        {
            tokens[i] = tokens[i].Trim();
        }

        return tokens;
    }

    /// <summary>
    /// Creates a <c>Type.Member</c> expression.
    /// </summary>
    protected static ExpressionSyntax MemberOf(string type, string member)
    {
        return ParseExpression($"{type}.{member}");
    }

    /// <summary>
    /// Creates a <c>Type.Method(arguments)</c> expression.
    /// </summary>
    protected static ExpressionSyntax Invoke(string type, string method, params ExpressionSyntax[] arguments)
    {
        ArgumentSyntax[] argumentList = new ArgumentSyntax[arguments.Length];
        for (int i = 0; i < arguments.Length; i++)
        {
            argumentList[i] = Argument(arguments[i]);
        }

        return InvocationExpression(MemberOf(type, method))
            .WithArgumentList(ArgumentList(SeparatedList(argumentList)));
    }

    /// <summary>
    /// Creates a <c>double</c> literal.
    /// </summary>
    protected static ExpressionSyntax Number(double value)
    {
        return LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(value));
    }

    /// <summary>
    /// Creates a string literal.
    /// </summary>
    protected static ExpressionSyntax Text(string value)
    {
        return LiteralExpression(SyntaxKind.StringLiteralExpression, Literal(value));
    }
}
