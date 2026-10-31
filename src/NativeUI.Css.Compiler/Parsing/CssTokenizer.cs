using System.Globalization;

namespace NativeUI.Css.Compiler;

/// <summary>
/// Single-pass lexical scanner that converts CSS source text into a sequence of <see cref="CssToken"/> items
/// while tracking 0-based line and column offsets and skipping comments directly during lexing.
/// </summary>
public static class CssTokenizer
{
    /// <summary>
    /// Tokenizes the specified <paramref name="source"/> CSS text into a list of tokens ending with <see cref="CssTokenKind.EndOfFile"/>.
    /// </summary>
    public static IReadOnlyList<CssToken> Tokenize(string source, List<(string Message, CssSourceSpan Span)>? lexicalErrors = null)
    {
        ArgumentNullException.ThrowIfNull(source);

        List<CssToken> tokens = [];
        int index = 0;
        int line = 0;
        int col = 0;
        int length = source.Length;

        while (index < length)
        {
            bool hadTrivia = SkipWhitespaceAndComments(source, ref index, ref line, ref col, lexicalErrors);
            if (index >= length)
            {
                break;
            }

            int startPos = index;
            int startLine = line;
            int startCol = col;
            char c = source[index];

            switch (c)
            {
                case '{':
                    Advance(source, ref index, ref line, ref col);
                    tokens.Add(MakeSimpleToken(CssTokenKind.OpenBrace, "{", startPos, startLine, startCol, index, line, col, hadTrivia));
                    continue;

                case '}':
                    Advance(source, ref index, ref line, ref col);
                    tokens.Add(MakeSimpleToken(CssTokenKind.CloseBrace, "}", startPos, startLine, startCol, index, line, col, hadTrivia));
                    continue;

                case '(':
                    Advance(source, ref index, ref line, ref col);
                    tokens.Add(MakeSimpleToken(CssTokenKind.OpenParen, "(", startPos, startLine, startCol, index, line, col, hadTrivia));
                    continue;

                case ')':
                    Advance(source, ref index, ref line, ref col);
                    tokens.Add(MakeSimpleToken(CssTokenKind.CloseParen, ")", startPos, startLine, startCol, index, line, col, hadTrivia));
                    continue;

                case '*':
                    Advance(source, ref index, ref line, ref col);
                    tokens.Add(MakeSimpleToken(CssTokenKind.Star, "*", startPos, startLine, startCol, index, line, col, hadTrivia));
                    continue;

                case ':':
                    Advance(source, ref index, ref line, ref col);
                    tokens.Add(MakeSimpleToken(CssTokenKind.Colon, ":", startPos, startLine, startCol, index, line, col, hadTrivia));
                    continue;

                case ';':
                    Advance(source, ref index, ref line, ref col);
                    tokens.Add(MakeSimpleToken(CssTokenKind.Semicolon, ";", startPos, startLine, startCol, index, line, col, hadTrivia));
                    continue;

                case ',':
                    Advance(source, ref index, ref line, ref col);
                    tokens.Add(MakeSimpleToken(CssTokenKind.Comma, ",", startPos, startLine, startCol, index, line, col, hadTrivia));
                    continue;

                case '/':
                    Advance(source, ref index, ref line, ref col);
                    tokens.Add(MakeSimpleToken(CssTokenKind.Slash, "/", startPos, startLine, startCol, index, line, col, hadTrivia));
                    continue;

                case '.':
                    if (index + 1 < length && char.IsDigit(source[index + 1]))
                    {
                        tokens.Add(ScanNumberOrDimension(source, ref index, ref line, ref col, startPos, startLine, startCol, hadTrivia));
                    }
                    else
                    {
                        Advance(source, ref index, ref line, ref col);
                        tokens.Add(MakeSimpleToken(CssTokenKind.Dot, ".", startPos, startLine, startCol, index, line, col, hadTrivia));
                    }
                    continue;

                case '#':
                    Advance(source, ref index, ref line, ref col);
                    while (index < length && IsIdentifierChar(source[index]))
                    {
                        Advance(source, ref index, ref line, ref col);
                    }

                    string hashText = source[startPos..index];
                    tokens.Add(MakeSimpleToken(CssTokenKind.Hash, hashText, startPos, startLine, startCol, index, line, col, hadTrivia));
                    continue;

                case '@':
                    Advance(source, ref index, ref line, ref col);
                    while (index < length && IsIdentifierChar(source[index]))
                    {
                        Advance(source, ref index, ref line, ref col);
                    }

                    string atText = source[startPos..index];
                    tokens.Add(MakeSimpleToken(CssTokenKind.AtKeyword, atText, startPos, startLine, startCol, index, line, col, hadTrivia));
                    continue;

                case '"' or '\'':
                    tokens.Add(ScanString(source, ref index, ref line, ref col, startPos, startLine, startCol, hadTrivia, lexicalErrors));
                    continue;
            }

            if (char.IsDigit(c) || ((c == '+' || c == '-') && index + 1 < length && (char.IsDigit(source[index + 1]) || source[index + 1] == '.')))
            {
                tokens.Add(ScanNumberOrDimension(source, ref index, ref line, ref col, startPos, startLine, startCol, hadTrivia));
                continue;
            }

            if (IsIdentifierStart(source, index))
            {
                while (index < length && IsIdentifierChar(source[index]))
                {
                    Advance(source, ref index, ref line, ref col);
                }

                string ident = source[startPos..index];
                tokens.Add(MakeSimpleToken(CssTokenKind.Identifier, ident, startPos, startLine, startCol, index, line, col, hadTrivia));
                continue;
            }

            Advance(source, ref index, ref line, ref col);
            CssSourceSpan badSpan = new(startPos, index - startPos, startLine, startCol, line, col);
            lexicalErrors?.Add(($"Unexpected character '{c}' in CSS.", badSpan));
            tokens.Add(new CssToken(CssTokenKind.BadToken, c.ToString(), 0, null, badSpan, hadTrivia));
        }

        CssSourceSpan eofSpan = new(index, 0, line, col, line, col);
        tokens.Add(new CssToken(CssTokenKind.EndOfFile, string.Empty, 0, null, eofSpan));
        return tokens;
    }

    private static bool SkipWhitespaceAndComments(
        string source,
        ref int index,
        ref int line,
        ref int col,
        List<(string Message, CssSourceSpan Span)>? lexicalErrors)
    {
        bool skippedAny = false;
        int length = source.Length;

        while (index < length)
        {
            char c = source[index];
            if (char.IsWhiteSpace(c))
            {
                skippedAny = true;
                Advance(source, ref index, ref line, ref col);
                continue;
            }

            if (c == '/' && index + 1 < length && source[index + 1] == '*')
            {
                skippedAny = true;
                int commentStartPos = index;
                int commentStartLine = line;
                int commentStartCol = col;

                Advance(source, ref index, ref line, ref col);
                Advance(source, ref index, ref line, ref col);

                bool closed = false;
                while (index < length)
                {
                    if (source[index] == '*' && index + 1 < length && source[index + 1] == '/')
                    {
                        Advance(source, ref index, ref line, ref col);
                        Advance(source, ref index, ref line, ref col);
                        closed = true;
                        break;
                    }

                    Advance(source, ref index, ref line, ref col);
                }

                if (!closed)
                {
                    CssSourceSpan span = new(commentStartPos, index - commentStartPos, commentStartLine, commentStartCol, line, col);
                    lexicalErrors?.Add(("Unterminated CSS comment '/*'.", span));
                }

                continue;
            }

            break;
        }

        return skippedAny;
    }

    private static CssToken ScanNumberOrDimension(
        string source,
        ref int index,
        ref int line,
        ref int col,
        int startPos,
        int startLine,
        int startCol,
        bool hadTrivia)
    {
        int length = source.Length;
        if (source[index] is '+' or '-')
        {
            Advance(source, ref index, ref line, ref col);
        }

        while (index < length && char.IsDigit(source[index]))
        {
            Advance(source, ref index, ref line, ref col);
        }

        if (index < length && source[index] == '.' && index + 1 < length && char.IsDigit(source[index + 1]))
        {
            Advance(source, ref index, ref line, ref col);
            while (index < length && char.IsDigit(source[index]))
            {
                Advance(source, ref index, ref line, ref col);
            }
        }

        string numSlice = source[startPos..index];
        _ = double.TryParse(numSlice, NumberStyles.Float, CultureInfo.InvariantCulture, out double numValue);

        if (index < length && source[index] == '%')
        {
            Advance(source, ref index, ref line, ref col);
            CssSourceSpan pctSpan = new(startPos, index - startPos, startLine, startCol, line, col);
            return new CssToken(CssTokenKind.Percentage, source[startPos..index], numValue, "%", pctSpan, hadTrivia);
        }

        if (index < length && (char.IsLetter(source[index]) || source[index] == '_'))
        {
            int unitStart = index;
            while (index < length && IsIdentifierChar(source[index]))
            {
                Advance(source, ref index, ref line, ref col);
            }

            string unit = source[unitStart..index];
            CssSourceSpan dimSpan = new(startPos, index - startPos, startLine, startCol, line, col);
            return new CssToken(CssTokenKind.Dimension, source[startPos..index], numValue, unit, dimSpan, hadTrivia);
        }

        CssSourceSpan numSpan = new(startPos, index - startPos, startLine, startCol, line, col);
        return new CssToken(CssTokenKind.Number, numSlice, numValue, null, numSpan, hadTrivia);
    }

    private static CssToken ScanString(
        string source,
        ref int index,
        ref int line,
        ref int col,
        int startPos,
        int startLine,
        int startCol,
        bool hadTrivia,
        List<(string Message, CssSourceSpan Span)>? lexicalErrors)
    {
        char quote = source[index];
        Advance(source, ref index, ref line, ref col);
        int contentStart = index;
        bool closed = false;

        while (index < source.Length)
        {
            char c = source[index];
            if (c == quote)
            {
                string content = source[contentStart..index];
                Advance(source, ref index, ref line, ref col);
                closed = true;
                CssSourceSpan strSpan = new(startPos, index - startPos, startLine, startCol, line, col);
                return new CssToken(CssTokenKind.String, content, 0, null, strSpan, hadTrivia);
            }

            if (c == '\n' || c == '\r')
            {
                break;
            }

            Advance(source, ref index, ref line, ref col);
        }

        if (!closed)
        {
            CssSourceSpan errSpan = new(startPos, index - startPos, startLine, startCol, line, col);
            lexicalErrors?.Add(("Unterminated CSS string literal.", errSpan));
        }

        string unclosed = source[contentStart..index];
        CssSourceSpan fallbackSpan = new(startPos, index - startPos, startLine, startCol, line, col);
        return new CssToken(CssTokenKind.String, unclosed, 0, null, fallbackSpan, hadTrivia);
    }

    private static bool IsIdentifierStart(string source, int index)
    {
        char c = source[index];
        if (char.IsLetter(c) || c == '_')
        {
            return true;
        }

        if (c == '-' && index + 1 < source.Length)
        {
            char next = source[index + 1];
            return char.IsLetter(next) || next == '-' || next == '_';
        }

        return false;
    }

    private static bool IsIdentifierChar(char c)
    {
        return char.IsLetterOrDigit(c) || c == '-' || c == '_';
    }

    private static void Advance(string source, ref int index, ref int line, ref int col)
    {
        if (source[index] == '\n')
        {
            line++;
            col = 0;
        }
        else if (source[index] != '\r')
        {
            col++;
        }

        index++;
    }

    private static CssToken MakeSimpleToken(
        CssTokenKind kind,
        string text,
        int startPos,
        int startLine,
        int startCol,
        int endPos,
        int endLine,
        int endCol,
        bool hadTrivia)
    {
        return new CssToken(
            kind,
            text,
            0,
            null,
            new CssSourceSpan(startPos, endPos - startPos, startLine, startCol, endLine, endCol),
            hadTrivia);
    }
}
