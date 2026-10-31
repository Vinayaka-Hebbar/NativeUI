namespace NativeUI.Css.Compiler;

/// <summary>
/// Represents a 0-based character and line/column span within a CSS source file.
/// </summary>
/// <param name="StartPos">The 0-based character offset of the start of the span.</param>
/// <param name="Length">The character length of the span.</param>
/// <param name="StartLine">The 0-based starting line index.</param>
/// <param name="StartColumn">The 0-based starting column index.</param>
/// <param name="EndLine">The 0-based ending line index.</param>
/// <param name="EndColumn">The 0-based ending column index.</param>
public readonly record struct CssSourceSpan(
    int StartPos,
    int Length,
    int StartLine,
    int StartColumn,
    int EndLine,
    int EndColumn);

/// <summary>
/// Identifies the lexical token category according to CSS Syntax Level 3.
/// </summary>
public enum CssTokenKind
{
    /// <summary>
    /// End of the CSS token stream.
    /// </summary>
    EndOfFile,

    /// <summary>
    /// A CSS identifier (property name, keyword, tag name, or custom property <c>--*</c>).
    /// </summary>
    Identifier,

    /// <summary>
    /// An <c>@</c>-keyword token such as <c>@media</c> or <c>@container</c>.
    /// </summary>
    AtKeyword,

    /// <summary>
    /// A hash token starting with <c>#</c> (such as a hex color literal <c>#2563eb</c>).
    /// </summary>
    Hash,

    /// <summary>
    /// A unitless numeric literal token (such as <c>0.65</c> or <c>700</c>).
    /// </summary>
    Number,

    /// <summary>
    /// A numeric value paired with a unit identifier (such as <c>24px</c>, <c>1.5rem</c>, or <c>250deg</c>).
    /// </summary>
    Dimension,

    /// <summary>
    /// A numeric value followed by <c>%</c> (such as <c>91%</c>).
    /// </summary>
    Percentage,

    /// <summary>
    /// A quoted string literal.
    /// </summary>
    String,

    /// <summary>
    /// Asterisk (<c>*</c>), the universal selector.
    /// </summary>
    Star,

    /// <summary>
    /// Colon (<c>:</c>).
    /// </summary>
    Colon,

    /// <summary>
    /// Semicolon (<c>;</c>).
    /// </summary>
    Semicolon,

    /// <summary>
    /// Comma (<c>,</c>).
    /// </summary>
    Comma,

    /// <summary>
    /// Period / class selector prefix (<c>.</c>).
    /// </summary>
    Dot,

    /// <summary>
    /// Forward slash (<c>/</c>), used in CSS Color 4 alpha separation.
    /// </summary>
    Slash,

    /// <summary>
    /// Opening parenthesis (<c>(</c>).
    /// </summary>
    OpenParen,

    /// <summary>
    /// Closing parenthesis (<c>)</c>).
    /// </summary>
    CloseParen,

    /// <summary>
    /// Opening curly brace (<c>{</c>).
    /// </summary>
    OpenBrace,

    /// <summary>
    /// Closing curly brace (<c>}</c>).
    /// </summary>
    CloseBrace,

    /// <summary>
    /// An unrecognized or malformed character sequence.
    /// </summary>
    BadToken
}

/// <summary>
/// Represents a lexical token produced by <see cref="CssTokenizer"/>.
/// </summary>
/// <param name="Kind">The lexical kind of the token.</param>
/// <param name="Text">The raw text of the token (or unquoted text for strings).</param>
/// <param name="NumericValue">The parsed numeric value for <see cref="CssTokenKind.Number"/>, <see cref="CssTokenKind.Dimension"/>, and <see cref="CssTokenKind.Percentage"/>.</param>
/// <param name="Unit">The unit suffix for <see cref="CssTokenKind.Dimension"/> tokens (for example, <c>"px"</c> or <c>"deg"</c>).</param>
/// <param name="Span">The source location span of the token.</param>
/// <param name="PrecededByWhitespace">Indicates whether whitespace or comments preceded this token.</param>
public readonly record struct CssToken(
    CssTokenKind Kind,
    string Text,
    double NumericValue,
    string? Unit,
    CssSourceSpan Span,
    bool PrecededByWhitespace = false);
