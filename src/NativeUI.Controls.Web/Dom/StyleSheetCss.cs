using System.Text;
using NativeUI.Core;
using NativeUI.Css;
using NativeUI.Styling;

namespace NativeUI.Controls.Web;

/// <summary>
/// Translates a C# <see cref="StyleSheet"/> to browser CSS, so the browser tracks states such as <c>:hover</c> natively.
/// Selectors are scoped to one <c>native-ui</c> element through <c>:where</c>, so the scope adds no specificity.
/// </summary>
internal static class StyleSheetCss
{
    /// <summary>
    /// The attribute listing the selector types an element matches, for types that an HTML tag cannot identify.
    /// </summary>
    public const string TypeAttribute = "data-nui-type";

    /// <summary>
    /// The attribute identifying the <c>native-ui</c> element that a generated stylesheet applies to.
    /// </summary>
    public const string ScopeAttribute = "data-nui-scope";

    /// <summary>
    /// The attribute identifying the view element that a scoped view stylesheet applies to.
    /// </summary>
    public const string ViewScopeAttribute = "data-nui-view-scope";

    /// <summary>
    /// The attribute marking the element of a <see cref="ScrollView"/>, whose default overflow the base stylesheet sets.
    /// </summary>
    public const string ScrollViewAttribute = "data-nui-scroll-view";

    /// <summary>
    /// The attribute marking the element of a <see cref="ProgressBar"/>, whose default box the base stylesheet sets.
    /// </summary>
    public const string ProgressBarAttribute = "data-nui-progress";

    /// <summary>
    /// The attribute marking the element of a <see cref="Portal"/>, which the base stylesheet takes out of the page flow.
    /// </summary>
    public const string PortalAttribute = "data-nui-portal";

    /// <summary>
    /// The base style shared by all <c>native-ui</c> elements, matching desktop rendering where CSS allows: border-box
    /// sizing; a solid border of zero width, which <c>border-width</c> on an element makes visible; the minimum line height of the desktop text shaper; a scrollbar with the size and colors of the desktop
    /// indicator, which <c>scrollbar-color</c> or <c>scrollbar-width</c> on an element replaces; block display for the root, each <see cref="Text"/>, and
    /// each <see cref="Button"/>, none of which is block-level by default; no browser control appearance on
    /// <see cref="Button"/> and <see cref="Input"/>, matching desktop rendering; scrolling on each <see cref="ScrollView"/>; fixed positioning on each <see cref="Portal"/>, with a modal backdrop of the same color as on desktop; and a
    /// <c>color-scheme</c> following <c>data-theme</c>.
    /// <c>:where</c> keeps every declaration at zero specificity.
    /// </summary>
    public static CssStyleSheet BaseStyleSheet { get; } = new BaseCssStyleSheet();

    /// <summary>
    /// Writes the rules of <paramref name="styleSheet"/> in order, scoped to the <c>native-ui</c> element with <paramref name="scopeId"/>.
    /// </summary>
    public static string Write(StyleSheet styleSheet, int scopeId)
    {
        ArgumentNullException.ThrowIfNull(styleSheet);

        Dictionary<Type, string> typeIds = CreateTypeIds(styleSheet);
        StringBuilder css = new();
        foreach (StyleRule rule in styleSheet.Rules)
        {
            css.Append(WriteSelector(rule.Target, scopeId, typeIds)).Append(" {");
            AppendDeclarations(css, rule.Style);
            css.Append(" }\n");
        }

        return css.ToString();
    }

    /// <summary>
    /// Writes the rules of <paramref name="styleSheet"/> in order, scoped to the view with <paramref name="viewScopeId"/>.
    /// </summary>
    public static string WriteScoped(StyleSheet styleSheet, string viewScopeId)
    {
        ArgumentNullException.ThrowIfNull(styleSheet);
        ArgumentException.ThrowIfNullOrWhiteSpace(viewScopeId);

        Dictionary<Type, string> typeIds = CreateTypeIds(styleSheet);
        StringBuilder css = new();
        foreach (StyleRule rule in styleSheet.Rules)
        {
            css.Append(WriteScopedSelector(rule.Target, viewScopeId, typeIds)).Append(" {");
            AppendDeclarations(css, rule.Style);
            css.Append(" }\n");
        }

        return css.ToString();
    }

    /// <summary>
    /// Returns the value of <see cref="TypeAttribute"/> for <paramref name="element"/>, or null when no rule needs it.
    /// </summary>
    public static string? GetTypeTokens(StyleSheet styleSheet, UIElement element)
    {
        ArgumentNullException.ThrowIfNull(styleSheet);
        ArgumentNullException.ThrowIfNull(element);

        string[] tokens = CreateTypeIds(styleSheet)
            .Where(entry => entry.Key.IsInstanceOfType(element))
            .Select(static entry => entry.Value)
            .ToArray();

        return tokens.Length > 0 ? string.Join(' ', tokens) : null;
    }

    /// <summary>
    /// Returns the value of <see cref="TypeAttribute"/> for <paramref name="element"/>, checking both its resolver and ancestor view stylesheets.
    /// </summary>
    public static string? GetTypeTokens(UIElement element)
    {
        ArgumentNullException.ThrowIfNull(element);

        List<StyleSheet> sheets = [];
        if (element.StyleResolver is StyleSheet rootSheet)
        {
            sheets.Add(rootSheet);
        }

        for (UIElement? current = element; current is not null; current = current.Parent)
        {
            if (current is View { StyleSheet: StyleSheet viewSheet })
            {
                sheets.Add(viewSheet);
            }
        }

        if (sheets.Count == 0)
        {
            return null;
        }

        HashSet<string> tokens = [];
        foreach (StyleSheet sheet in sheets)
        {
            foreach (KeyValuePair<Type, string> entry in CreateTypeIds(sheet))
            {
                if (entry.Key.IsInstanceOfType(element))
                {
                    tokens.Add(entry.Value);
                }
            }
        }

        return tokens.Count > 0 ? string.Join(' ', tokens) : null;
    }

    /// <summary>
    /// Assigns each selector type a unique identifier in rule order. Every <c>span</c> created by a DOM root represents
    /// a <see cref="Text"/>, so <see cref="Text"/> itself needs none.
    /// </summary>
    private static Dictionary<Type, string> CreateTypeIds(StyleSheet styleSheet)
    {
        Dictionary<Type, string> typeIds = [];
        foreach (StyleRule rule in styleSheet.Rules)
        {
            foreach (CompoundSelector part in rule.Target.Ancestors.Append(rule.Target.Subject))
            {
                if (part.ClrType is Type type && type != typeof(Text))
                {
                    typeIds.TryAdd(type, $"t{typeIds.Count}");
                }
            }
        }

        return typeIds;
    }

    private static void AppendDeclarations(StringBuilder css, IStyle style)
    {
        foreach ((string property, string? value) in StyleCssDeclarations.Get(style))
        {
            if (value is not null)
            {
                css.Append(' ').Append(property).Append(": ").Append(value).Append(';');
            }
        }
    }

    private static string WriteSelector(StyleSelector selector, int scopeId, Dictionary<Type, string> typeIds)
    {
        StringBuilder css = new($":where(native-ui[{ScopeAttribute}=\"{scopeId}\"])");
        foreach (CompoundSelector ancestor in selector.Ancestors)
        {
            css.Append(' ').Append(WriteCompound(ancestor, typeIds));
        }

        return css.Append(' ').Append(WriteCompound(selector.Subject, typeIds)).ToString();
    }

    private static string WriteScopedSelector(StyleSelector selector, string viewScopeId, Dictionary<Type, string> typeIds)
    {
        // The selector matching from the view places the scope after a compound, because CSS requires a type selector first.
        string scope = $":where([{ViewScopeAttribute}=\"{viewScopeId}\"])";
        string subject = WriteCompound(selector.Subject, typeIds);

        if (selector.Ancestors.Length == 0)
        {
            return $"{scope} {subject}, {subject}{scope}";
        }

        string firstAncestor = WriteCompound(selector.Ancestors[0], typeIds);
        StringBuilder otherAncestors = new();
        foreach (CompoundSelector ancestor in selector.Ancestors.Skip(1))
        {
            otherAncestors.Append(WriteCompound(ancestor, typeIds)).Append(' ');
        }

        return $"{scope} {firstAncestor} {otherAncestors}{subject}, {firstAncestor}{scope} {otherAncestors}{subject}";
    }

    private static string WriteCompound(CompoundSelector part, Dictionary<Type, string> typeIds)
    {
        StringBuilder css = new(WriteType(part, typeIds));
        foreach (string className in part.Classes)
        {
            css.Append('.').Append(EscapeIdentifier(className));
        }

        css.Append(WriteStates(part.RequiredStates));
        return css.Length > 0 ? css.ToString() : "*";
    }

    /// <summary>
    /// Writes a selector with the specificity of one type, or nothing for an unconstrained part. An HTML tag supplies
    /// the specificity, and <c>:where</c> narrows it to the CLR type without adding any.
    /// </summary>
    /// <exception cref="NotSupportedException">The part constrains both an element type name and a CLR type.</exception>
    private static string WriteType(CompoundSelector part, Dictionary<Type, string> typeIds)
    {
        if (part.ElementType is not null && part.ClrType is not null)
        {
            throw new NotSupportedException("A selector part with both an element type name and a CLR type has no browser CSS translation.");
        }

        if (part.ElementType is not null)
        {
            return HtmlTagMapping.GetTagSelector(part.ElementType);
        }

        if (part.ClrType is not Type type)
        {
            return string.Empty;
        }

        string tag = HtmlTagMapping.GetTagSelector(type);
        return typeIds.TryGetValue(type, out string? typeId)
            ? $"{tag}:where([{TypeAttribute}~=\"{typeId}\"])"
            : tag;
    }

    private static string WriteStates(ElementStates states)
    {
        StringBuilder builder = new();
        if (states.HasFlag(ElementStates.Hover))
        {
            builder.Append(":hover");
        }

        if (states.HasFlag(ElementStates.Pressed))
        {
            builder.Append(":active");
        }

        if (states.HasFlag(ElementStates.Disabled))
        {
            builder.Append(":disabled");
        }

        if (states.HasFlag(ElementStates.Focus))
        {
            builder.Append(":focus");
        }

        return builder.ToString();
    }

    private static string EscapeIdentifier(string value)
    {
        StringBuilder escaped = new();
        for (int i = 0; i < value.Length; i++)
        {
            char c = value[i];
            bool isLeadingDigit = char.IsAsciiDigit(c) && (i == 0 || (i == 1 && value[0] == '-'));
            bool isLoneHyphen = c == '-' && value.Length == 1;
            bool isIdentifierChar = char.IsAsciiLetterOrDigit(c) || c is '-' or '_' || c >= 0x80;
            if (isIdentifierChar && !isLeadingDigit && !isLoneHyphen)
            {
                escaped.Append(c);
            }
            else
            {
                escaped.Append('\\').Append(((int)c).ToString("x")).Append(' ');
            }
        }

        return escaped.ToString();
    }
}
