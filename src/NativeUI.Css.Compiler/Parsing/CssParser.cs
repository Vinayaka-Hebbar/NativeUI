namespace NativeUI.Css.Compiler;

/// <summary>
/// Recursive-descent CSS parser that consumes a <see cref="CssToken"/> stream and builds a
/// structured <see cref="CssStylesheetNode"/> abstract syntax tree.
/// </summary>
public sealed class CssParser
{
    private static readonly Dictionary<string, string> s_pseudoClassStates = new(StringComparer.OrdinalIgnoreCase)
    {
        ["hover"] = "Hover",
        ["active"] = "Pressed",
        ["disabled"] = "Disabled",
        ["focus"] = "Focus",
        ["focus-visible"] = "FocusVisible",
        ["focus-within"] = "FocusWithin",
        ["checked"] = "Checked",
        ["unchecked"] = "Unchecked",
        ["indeterminate"] = "Indeterminate",
        ["pressed"] = "On",
        ["unpressed"] = "Off",
        ["first-child"] = "FirstChild",
        ["last-child"] = "LastChild",
        ["selected"] = "Selected",
        ["popover-open"] = "PopoverOpen"
    };

    private readonly IReadOnlyList<CssToken> _tokens;
    private readonly List<(string Message, CssSourceSpan Span)> _errors;
    private int _position;

    private CssParser(IReadOnlyList<CssToken> tokens, List<(string Message, CssSourceSpan Span)> errors)
    {
        _tokens = tokens;
        _errors = errors;
    }

    /// <summary>
    /// Parses the specified <paramref name="cssText"/> from <paramref name="filePath"/> into a <see cref="CssStylesheetNode"/> AST.
    /// </summary>
    public static CssStylesheetNode Parse(string filePath, string cssText)
    {
        ArgumentNullException.ThrowIfNull(filePath);
        ArgumentNullException.ThrowIfNull(cssText);

        List<(string Message, CssSourceSpan Span)> errors = [];
        IReadOnlyList<CssToken> tokens = CssTokenizer.Tokenize(cssText, errors);
        CssParser parser = new(tokens, errors);
        return parser.ParseStylesheet(filePath, cssText);
    }

    private CssToken Current => _position < _tokens.Count ? _tokens[_position] : _tokens[^1];

    private CssStylesheetNode ParseStylesheet(string filePath, string rawCss)
    {
        CssSourceSpan startSpan = Current.Span;
        List<CssStatementNode> statements = [];

        while (Current.Kind != CssTokenKind.EndOfFile)
        {
            if (Current.Kind == CssTokenKind.AtKeyword &&
                string.Equals(Current.Text, "@keyframes", StringComparison.OrdinalIgnoreCase))
            {
                CssKeyframesRuleNode? keyframesRule = ParseKeyframesRule();
                if (keyframesRule is not null)
                {
                    statements.Add(keyframesRule);
                }
            }
            else if (Current.Kind == CssTokenKind.AtKeyword)
            {
                CssMediaRuleNode? mediaRule = ParseMediaRule();
                if (mediaRule is not null)
                {
                    statements.Add(mediaRule);
                }
            }
            else
            {
                CssStyleRuleNode? styleRule = ParseStyleRule();
                if (styleRule is not null)
                {
                    statements.Add(styleRule);
                }
            }
        }

        CssSourceSpan fullSpan = CombineSpans(startSpan, Current.Span);
        return new CssStylesheetNode(filePath, rawCss, statements, _errors, fullSpan);
    }

    private CssKeyframesRuleNode? ParseKeyframesRule()
    {
        CssToken atToken = Advance();
        if (Current.Kind != CssTokenKind.Identifier)
        {
            _errors.Add(("Expected a name after @keyframes.", Current.Span));
            SkipToNextRuleOrBlockEnd();
            return null;
        }

        string name = Advance().Text;
        if (Current.Kind != CssTokenKind.OpenBrace)
        {
            _errors.Add(("Expected '{' after the @keyframes name.", Current.Span));
            SkipToNextRuleOrBlockEnd();
            return null;
        }

        Advance();
        List<CssKeyframeNode> frames = [];
        while (Current.Kind is not (CssTokenKind.CloseBrace or CssTokenKind.EndOfFile))
        {
            List<double>? offsets = ParseKeyframeOffsets();
            if (offsets is null || Current.Kind != CssTokenKind.OpenBrace)
            {
                _errors.Add(("Expected a keyframe selector of 'from', 'to', or a percentage from 0% to 100%.", Current.Span));
                SkipToNextRuleOrBlockEnd();
                continue;
            }

            CssToken openBrace = Advance();
            List<CssDeclarationNode> declarations = ParseDeclarations();
            if (Current.Kind == CssTokenKind.CloseBrace)
            {
                Advance();
            }
            else
            {
                _errors.Add(("Unclosed '{' in keyframe block.", openBrace.Span));
            }

            frames.Add(new CssKeyframeNode(offsets, declarations));
        }

        if (Current.Kind == CssTokenKind.CloseBrace)
        {
            Advance();
        }
        else
        {
            _errors.Add(("Missing closing '}' for @keyframes block.", atToken.Span));
        }

        return new CssKeyframesRuleNode(name, frames, CombineSpans(atToken.Span, PrevTokenSpan()));
    }

    /// <summary>
    /// Parses the comma-separated selectors of a keyframe block, or returns null when one is invalid.
    /// </summary>
    private List<double>? ParseKeyframeOffsets()
    {
        List<double> offsets = [];
        while (true)
        {
            CssToken token = Current;
            if (token.Kind == CssTokenKind.Percentage && token.NumericValue is >= 0 and <= 100)
            {
                offsets.Add(token.NumericValue / 100.0);
            }
            else if (token.Kind == CssTokenKind.Identifier && string.Equals(token.Text, "from", StringComparison.OrdinalIgnoreCase))
            {
                offsets.Add(0);
            }
            else if (token.Kind == CssTokenKind.Identifier && string.Equals(token.Text, "to", StringComparison.OrdinalIgnoreCase))
            {
                offsets.Add(1);
            }
            else
            {
                return null;
            }

            Advance();
            if (Current.Kind != CssTokenKind.Comma)
            {
                return offsets;
            }

            Advance();
        }
    }

    private CssMediaRuleNode? ParseMediaRule()
    {
        CssToken atToken = Advance();
        CssSourceSpan queryStart = Current.Span;

        if (!string.Equals(atToken.Text, "@media", StringComparison.OrdinalIgnoreCase))
        {
            _errors.Add(($"Unsupported at-rule '{atToken.Text}'.", atToken.Span));
            SkipToNextRuleOrBlockEnd();
            return null;
        }

        string featureName = string.Empty;
        string featureValue = string.Empty;
        bool isSupported = false;
        CssParsedColorScheme scheme = CssParsedColorScheme.Any;

        if (Current.Kind == CssTokenKind.OpenParen)
        {
            Advance();
            if (Current.Kind == CssTokenKind.Identifier)
            {
                featureName = Advance().Text;
            }

            if (Current.Kind == CssTokenKind.Colon)
            {
                Advance();
            }

            if (Current.Kind == CssTokenKind.Identifier)
            {
                featureValue = Advance().Text;
            }

            if (Current.Kind == CssTokenKind.CloseParen)
            {
                Advance();
            }

            if (string.Equals(featureName, "prefers-color-scheme", StringComparison.OrdinalIgnoreCase))
            {
                if (string.Equals(featureValue, "dark", StringComparison.OrdinalIgnoreCase))
                {
                    scheme = CssParsedColorScheme.Dark;
                    isSupported = true;
                }
                else if (string.Equals(featureValue, "light", StringComparison.OrdinalIgnoreCase))
                {
                    scheme = CssParsedColorScheme.Light;
                    isSupported = true;
                }
            }
        }
        else
        {
            while (Current.Kind is not (CssTokenKind.OpenBrace or CssTokenKind.EndOfFile))
            {
                Advance();
            }
        }

        CssSourceSpan querySpan = CombineSpans(atToken.Span, PrevTokenSpan());
        if (Current.Kind != CssTokenKind.OpenBrace)
        {
            _errors.Add(("Expected '{' after @media query condition.", Current.Span));
            return null;
        }

        Advance();
        List<CssStyleRuleNode> nestedRules = [];
        while (Current.Kind is not (CssTokenKind.CloseBrace or CssTokenKind.EndOfFile))
        {
            CssStyleRuleNode? rule = ParseStyleRule();
            if (rule is not null)
            {
                nestedRules.Add(rule);
            }
        }

        if (Current.Kind == CssTokenKind.CloseBrace)
        {
            Advance();
        }
        else
        {
            _errors.Add(("Missing closing '}' for @media block.", atToken.Span));
        }

        CssSourceSpan fullSpan = CombineSpans(atToken.Span, PrevTokenSpan());
        return new CssMediaRuleNode(featureName, featureValue, scheme, isSupported, nestedRules, querySpan, fullSpan);
    }

    private CssStyleRuleNode? ParseStyleRule()
    {
        CssSourceSpan ruleStart = Current.Span;
        List<CssSelectorNode> selectors = ParseSelectorList();
        if (selectors.Count == 0)
        {
            if (Current.Kind != CssTokenKind.EndOfFile)
            {
                _errors.Add(("Expected CSS selector before '{'.", Current.Span));
                SkipToNextRuleOrBlockEnd();
            }

            return null;
        }

        if (Current.Kind != CssTokenKind.OpenBrace)
        {
            _errors.Add(("Missing opening '{' for CSS style rule.", Current.Span));
            SkipToNextRuleOrBlockEnd();
            return null;
        }

        CssToken openBrace = Advance();
        List<CssDeclarationNode> declarations = ParseDeclarations();

        if (Current.Kind == CssTokenKind.CloseBrace)
        {
            Advance();
        }
        else
        {
            _errors.Add(("Unclosed '{' in CSS style rule.", openBrace.Span));
        }

        CssSourceSpan fullSpan = CombineSpans(ruleStart, PrevTokenSpan());
        return new CssStyleRuleNode(selectors, declarations, fullSpan);
    }

    private List<CssSelectorNode> ParseSelectorList()
    {
        List<CssSelectorNode> selectors = [];
        while (Current.Kind is not (CssTokenKind.OpenBrace or CssTokenKind.CloseBrace or CssTokenKind.EndOfFile))
        {
            CssSelectorNode selector = ParseSingleSelector();
            selectors.Add(selector);

            if (Current.Kind == CssTokenKind.Comma)
            {
                Advance();
            }
            else
            {
                break;
            }
        }

        return selectors;
    }

    private CssSelectorNode ParseSingleSelector()
    {
        CssSourceSpan startSpan = Current.Span;
        List<CssToken> selectorTokens = [];

        while (Current.Kind is not (CssTokenKind.Comma or CssTokenKind.OpenBrace or CssTokenKind.CloseBrace or CssTokenKind.EndOfFile))
        {
            selectorTokens.Add(Advance());
        }

        if (selectorTokens.Count == 0)
        {
            return new CssSelectorNode(string.Empty, null, [], [], [], CssParsedPseudoElement.None, [], false, false, startSpan);
        }

        CssSourceSpan fullSpan = CombineSpans(selectorTokens[0].Span, selectorTokens[^1].Span);
        string rawText = string.Concat(selectorTokens.Select((t, idx) => idx > 0 && t.PrecededByWhitespace ? " " + t.Text : t.Text));

        // Check for :root pseudo-class selector.
        if (selectorTokens.Count == 2 &&
            selectorTokens[0].Kind == CssTokenKind.Colon &&
            selectorTokens[1].Kind == CssTokenKind.Identifier &&
            !selectorTokens[1].PrecededByWhitespace &&
            string.Equals(selectorTokens[1].Text, "root", StringComparison.Ordinal))
        {
            return new CssSelectorNode(":root", null, [], [], [], CssParsedPseudoElement.None, [], IsRoot: true, IsValid: true, fullSpan);
        }

        List<CssCompoundSelectorNode> compounds = [];
        List<CssSourceSpan> typeSpans = [];
        List<CssPseudoClassNode> pseudoClasses = [];
        CssParsedPseudoElement pseudoElement = CssParsedPseudoElement.None;
        CssPartSelectorNode? part = null;
        bool isValid = true;
        int i = 0;

        while (i < selectorTokens.Count && isValid)
        {
            if (pseudoElement != CssParsedPseudoElement.None || part is not null)
            {
                // A pseudo-element must terminate the selector.
                isValid = false;
                break;
            }

            isValid = TryParseCompoundSelector(
                selectorTokens,
                ref i,
                typeSpans,
                pseudoClasses,
                ref pseudoElement,
                ref part,
                out CssCompoundSelectorNode compound);
            compounds.Add(compound);
        }

        CssCompoundSelectorNode subject = compounds[^1];
        return new CssSelectorNode(
            rawText,
            subject.ElementType,
            subject.Classes,
            subject.RequiredStates,
            compounds.GetRange(0, compounds.Count - 1),
            pseudoElement,
            typeSpans,
            IsRoot: false,
            isValid,
            fullSpan,
            subject.Attributes,
            pseudoClasses,
            part);
    }

    /// <summary>
    /// Parses one whitespace-delimited compound part, advancing <paramref name="index"/> past the tokens it consumes.
    /// Returns false for an empty part or unsupported syntax, such as an unknown pseudo-selector or a combinator.
    /// </summary>
    private static bool TryParseCompoundSelector(
        List<CssToken> tokens,
        ref int index,
        List<CssSourceSpan> typeSpans,
        List<CssPseudoClassNode> pseudoClasses,
        ref CssParsedPseudoElement pseudoElement,
        ref CssPartSelectorNode? part,
        out CssCompoundSelectorNode compound)
    {
        string? elementType = null;
        List<string> classes = [];
        List<string> requiredStates = [];
        List<CssAttributeSelectorNode> attributes = [];
        List<string> partStates = [];
        bool hasContent = false;
        int start = index;

        if (tokens[index].Kind == CssTokenKind.Identifier)
        {
            elementType = tokens[index].Text.ToLowerInvariant();
            typeSpans.Add(tokens[index].Span);
            hasContent = true;
            index++;
        }
        else if (tokens[index].Kind == CssTokenKind.Star)
        {
            hasContent = true;
            index++;
        }

        bool isValid = true;
        while (index < tokens.Count && (index == start || !tokens[index].PrecededByWhitespace))
        {
            if (IsAttachedIdentifier(tokens, index + 1) && tokens[index].Kind == CssTokenKind.Dot)
            {
                classes.Add(tokens[index + 1].Text);
                hasContent = true;
                index += 2;
            }
            else if (IsAttachedIdentifier(tokens, index + 1) &&
                tokens[index].Kind == CssTokenKind.Colon &&
                s_pseudoClassStates.TryGetValue(tokens[index + 1].Text, out string? state))
            {
                // A pseudo-class after ::part() requires its state on the part element.
                (part is null ? requiredStates : partStates).Add(state);
                pseudoClasses.Add(new CssPseudoClassNode(state, CombineSpans(tokens[index].Span, tokens[index + 1].Span)));
                hasContent = true;
                index += 2;
            }
            else if (tokens[index].Kind == CssTokenKind.OpenBracket &&
                index + 4 < tokens.Count &&
                tokens[index + 1].Kind == CssTokenKind.Identifier &&
                tokens[index + 2].Kind == CssTokenKind.Equals &&
                tokens[index + 3].Kind is CssTokenKind.String or CssTokenKind.Identifier &&
                tokens[index + 4].Kind == CssTokenKind.CloseBracket)
            {
                attributes.Add(new CssAttributeSelectorNode(
                    tokens[index + 1].Text,
                    tokens[index + 3].Text));
                hasContent = true;
                index += 5;
            }
            else if (tokens[index].Kind == CssTokenKind.OpenBracket &&
                index + 2 < tokens.Count &&
                tokens[index + 1].Kind == CssTokenKind.Identifier &&
                tokens[index + 2].Kind == CssTokenKind.CloseBracket)
            {
                attributes.Add(new CssAttributeSelectorNode(tokens[index + 1].Text, null));
                hasContent = true;
                index += 3;
            }
            else if (IsAttachedIdentifier(tokens, index + 2) &&
                tokens[index].Kind == CssTokenKind.Colon &&
                tokens[index + 1].Kind == CssTokenKind.Colon &&
                !tokens[index + 1].PrecededByWhitespace &&
                string.Equals(tokens[index + 2].Text, "selection", StringComparison.OrdinalIgnoreCase))
            {
                pseudoElement = CssParsedPseudoElement.Selection;
                hasContent = true;
                index += 3;
            }
            else if (IsPartPseudoElement(tokens, index))
            {
                part = new CssPartSelectorNode(tokens[index + 4].Text, CombineSpans(tokens[index].Span, tokens[index + 5].Span), []);
                hasContent = true;
                index += 6;
            }
            else
            {
                isValid = false;
                index = tokens.Count;
            }
        }

        if (part is not null && partStates.Count > 0)
        {
            part = part with { States = partStates };
        }

        compound = new CssCompoundSelectorNode(elementType, classes, requiredStates, attributes);
        return isValid && hasContent;
    }

    /// <summary>
    /// Returns whether the tokens at <paramref name="index"/> form <c>::part(name)</c> with no whitespace inside it.
    /// </summary>
    private static bool IsPartPseudoElement(List<CssToken> tokens, int index)
    {
        return index + 5 < tokens.Count &&
            tokens[index].Kind == CssTokenKind.Colon &&
            tokens[index + 1].Kind == CssTokenKind.Colon &&
            !tokens[index + 1].PrecededByWhitespace &&
            IsAttachedIdentifier(tokens, index + 2) &&
            string.Equals(tokens[index + 2].Text, "part", StringComparison.OrdinalIgnoreCase) &&
            tokens[index + 3].Kind == CssTokenKind.OpenParen &&
            !tokens[index + 3].PrecededByWhitespace &&
            IsAttachedIdentifier(tokens, index + 4) &&
            tokens[index + 5].Kind == CssTokenKind.CloseParen &&
            !tokens[index + 5].PrecededByWhitespace;
    }

    private static bool IsAttachedIdentifier(List<CssToken> tokens, int index)
    {
        return index < tokens.Count &&
            tokens[index].Kind == CssTokenKind.Identifier &&
            !tokens[index].PrecededByWhitespace;
    }

    private List<CssDeclarationNode> ParseDeclarations()
    {
        List<CssDeclarationNode> declarations = [];

        while (Current.Kind is not (CssTokenKind.CloseBrace or CssTokenKind.EndOfFile))
        {
            if (Current.Kind == CssTokenKind.Semicolon)
            {
                Advance();
                continue;
            }

            if (Current.Kind != CssTokenKind.Identifier)
            {
                _errors.Add(($"Expected CSS property name, found '{Current.Text}'.", Current.Span));
                RecoverToNextDeclaration();
                continue;
            }

            CssToken propToken = Advance();
            if (Current.Kind != CssTokenKind.Colon)
            {
                _errors.Add(($"Expected ':' after CSS property '{propToken.Text}'.", propToken.Span));
                RecoverToNextDeclaration();
                continue;
            }

            Advance(); // Consume ':'
            CssValueSyntaxNode? valueNode = ParseValue(isFunctionArgument: false);
            if (valueNode is null)
            {
                _errors.Add(($"Missing value for CSS property '{propToken.Text}'.", propToken.Span));
                RecoverToNextDeclaration();
                continue;
            }

            if (Current.Kind == CssTokenKind.Semicolon)
            {
                Advance();
            }

            CssSourceSpan fullSpan = CombineSpans(propToken.Span, valueNode.Span);
            declarations.Add(new CssDeclarationNode(propToken.Text, propToken.Span, valueNode, fullSpan));
        }

        return declarations;
    }

    /// <summary>
    /// Parses a declaration value, or a <c>var()</c> fallback when <paramref name="isFunctionArgument"/> is
    /// <see langword="true"/>. Comma-separated values become a <see cref="CssValueSeparator.Comma"/> list whose items
    /// are the individual values.
    /// </summary>
    private CssValueSyntaxNode? ParseValue(bool isFunctionArgument)
    {
        List<CssValueSyntaxNode> values = [];
        List<CssValueSyntaxNode> components = [];
        while (!IsValueEnd(isFunctionArgument))
        {
            if (Current.Kind == CssTokenKind.Comma)
            {
                if (components.Count == 0)
                {
                    _errors.Add(("Expected a value before ','.", Current.Span));
                }
                else
                {
                    values.Add(CreateValue(components));
                    components.Clear();
                }

                Advance();
                continue;
            }

            CssValueSyntaxNode? component = ParseSingleValueComponent();
            if (component is null)
            {
                Advance();
                continue;
            }

            components.Add(component);
        }

        if (components.Count > 0)
        {
            values.Add(CreateValue(components));
        }

        if (values.Count == 0)
        {
            return null;
        }

        if (values.Count == 1)
        {
            return values[0];
        }

        CssSourceSpan span = CombineSpans(values[0].Span, values[^1].Span);
        return new CssValueListNode(values, span, CssValueSeparator.Comma);
    }

    /// <summary>
    /// Determines whether the current token ends a value. A function argument also ends at the end of its declaration,
    /// which leaves an unclosed function to be reported.
    /// </summary>
    private bool IsValueEnd(bool isFunctionArgument)
    {
        return Current.Kind is CssTokenKind.Semicolon or CssTokenKind.CloseBrace or CssTokenKind.EndOfFile ||
            (isFunctionArgument && Current.Kind == CssTokenKind.CloseParen);
    }

    private static CssValueSyntaxNode CreateValue(List<CssValueSyntaxNode> components)
    {
        if (components.Count == 1)
        {
            return components[0];
        }

        CssSourceSpan span = CombineSpans(components[0].Span, components[^1].Span);
        return new CssValueListNode([.. components], span);
    }

    private CssValueSyntaxNode? ParseSingleValueComponent()
    {
        CssToken token = Current;
        switch (token.Kind)
        {
            case CssTokenKind.Hash:
                Advance();
                return new CssHexColorValueNode(token.Text, token.Span);

            case CssTokenKind.Number:
                Advance();
                return new CssNumberValueNode(token.NumericValue, token.Span);

            case CssTokenKind.Dimension:
                Advance();
                return new CssDimensionValueNode(token.NumericValue, token.Unit ?? "px", token.Span);

            case CssTokenKind.Percentage:
                Advance();
                return new CssPercentageValueNode(token.NumericValue, token.Span);

            case CssTokenKind.String:
                Advance();
                return new CssStringValueNode(token.Text, token.Span);

            case CssTokenKind.Identifier:
                Advance();
                if (Current.Kind == CssTokenKind.OpenParen && !Current.PrecededByWhitespace)
                {
                    return ParseFunctionValue(token);
                }

                return new CssKeywordValueNode(token.Text, token.Span);

            default:
                return null;
        }
    }

    private CssFunctionValueNode ParseFunctionValue(CssToken nameToken)
    {
        Advance(); // Consume '('
        CssValueSyntaxNode? slashArg = null;
        List<CssValueSyntaxNode> args = string.Equals(nameToken.Text, "var", StringComparison.OrdinalIgnoreCase)
            ? ParseVarArguments()
            : ParseFunctionArguments(out slashArg);

        CssSourceSpan endSpan = Current.Span;
        if (Current.Kind == CssTokenKind.CloseParen)
        {
            endSpan = Advance().Span;
        }
        else
        {
            _errors.Add(($"Missing closing ')' for function '{nameToken.Text}'.", nameToken.Span));
        }

        return new CssFunctionValueNode(nameToken.Text, args, slashArg, CombineSpans(nameToken.Span, endSpan));
    }

    /// <summary>
    /// Parses comma-, space-, and slash-separated function arguments into one flat list, with the component after a
    /// slash returned separately.
    /// </summary>
    private List<CssValueSyntaxNode> ParseFunctionArguments(out CssValueSyntaxNode? slashArg)
    {
        List<CssValueSyntaxNode> args = [];
        slashArg = null;
        bool afterSlash = false;

        while (Current.Kind is not (CssTokenKind.CloseParen or CssTokenKind.EndOfFile))
        {
            if (Current.Kind == CssTokenKind.Comma)
            {
                Advance();
                continue;
            }

            if (Current.Kind == CssTokenKind.Slash)
            {
                Advance();
                afterSlash = true;
                continue;
            }

            CssValueSyntaxNode? component = ParseSingleValueComponent();
            if (component is null)
            {
                Advance();
                continue;
            }

            if (afterSlash)
            {
                slashArg = component;
            }
            else
            {
                args.Add(component);
            }
        }

        return args;
    }

    /// <summary>
    /// Parses the arguments of <c>var()</c>: the custom-property name and an optional fallback. Everything after the
    /// first comma is one fallback value, which can contain spaces and further commas, as in
    /// <c>var(--font, Arial, sans-serif)</c>.
    /// </summary>
    private List<CssValueSyntaxNode> ParseVarArguments()
    {
        List<CssValueSyntaxNode> args = [];
        if (ParseSingleValueComponent() is CssValueSyntaxNode name)
        {
            args.Add(name);
        }

        if (Current.Kind == CssTokenKind.Comma)
        {
            Advance();
            if (ParseValue(isFunctionArgument: true) is CssValueSyntaxNode fallback)
            {
                args.Add(fallback);
            }
        }

        if (!IsValueEnd(isFunctionArgument: true))
        {
            _errors.Add(("Expected ',' or ')' after the custom-property name in var().", Current.Span));
            while (!IsValueEnd(isFunctionArgument: true))
            {
                Advance();
            }
        }

        return args;
    }

    private void RecoverToNextDeclaration()
    {
        while (Current.Kind is not (CssTokenKind.Semicolon or CssTokenKind.CloseBrace or CssTokenKind.EndOfFile))
        {
            Advance();
        }

        if (Current.Kind == CssTokenKind.Semicolon)
        {
            Advance();
        }
    }

    private void SkipToNextRuleOrBlockEnd()
    {
        while (Current.Kind is not (CssTokenKind.CloseBrace or CssTokenKind.EndOfFile))
        {
            Advance();
        }

        if (Current.Kind == CssTokenKind.CloseBrace)
        {
            Advance();
        }
    }

    private CssToken Advance()
    {
        CssToken current = Current;
        if (_position < _tokens.Count - 1)
        {
            _position++;
        }

        return current;
    }

    private CssSourceSpan PrevTokenSpan()
    {
        int idx = Math.Max(0, _position - 1);
        return _tokens[idx].Span;
    }

    private static CssSourceSpan CombineSpans(CssSourceSpan first, CssSourceSpan last)
    {
        int length = Math.Max(0, (last.StartPos + last.Length) - first.StartPos);
        return new CssSourceSpan(
            first.StartPos,
            length,
            first.StartLine,
            first.StartColumn,
            last.EndLine,
            last.EndColumn);
    }
}
