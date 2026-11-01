namespace NativeUI.Controls.Web;

using System.Collections.Generic;
using System.Text;
using NativeUI.Css;
using NativeUI.Styling;

/// <summary>
/// Defines the base CSS rules and browser stylesheet applied across all <c>native-ui</c> elements on Web.
/// </summary>
internal sealed class BaseCssStyleSheet : CssStyleSheet
{
    /// <summary>
    /// Initializes a new instance of the <see cref="BaseCssStyleSheet"/> class.
    /// </summary>
    public BaseCssStyleSheet()
        : this(CreateRules())
    {
    }

    private BaseCssStyleSheet(List<CssRule> rules)
        : base("NativeUI.Base", BuildCss(rules), rules)
    {
    }

    /// <summary>
    /// Creates a new compiled base <see cref="CssStyleSheet"/> instance.
    /// </summary>
    public static CssStyleSheet CreateBaseStyleSheet()
    {
        return new BaseCssStyleSheet();
    }

    private static string BuildCss(List<CssRule> rules)
    {
        StringBuilder builder = new();

        foreach (CssRule rule in rules)
        {
            if (rule.Media != CssMedia.None)
            {
                builder.Append(rule.Media.ToCssString());
                builder.Append(" { ");
                AppendRuleBody(builder, rule);
                builder.Append(" }\n");
            }
            else
            {
                AppendRuleBody(builder, rule);
                builder.Append('\n');
            }
        }

        return builder.ToString();
    }

    private static void AppendRuleBody(StringBuilder builder, CssRule rule)
    {
        builder.Append(rule.Target.ToCssString());
        builder.Append(" { ");

        foreach ((string property, CssValue value) in rule.Declarations)
        {
            builder.Append(property);
            builder.Append(": ");
            builder.Append(value);
            builder.Append("; ");
        }

        builder.Append('}');
    }

    private static List<CssRule> CreateRules()
    {
        List<CssRule> rules = [];
        AddLayoutRules(rules);
        AddControlResetRules(rules);
        AddComponentRules(rules);
        AddScrollbarRules(rules);
        AddThemeRules(rules);
        AddReducedMotionRules(rules);
        return rules;
    }

    private static void AddLayoutRules(List<CssRule> rules)
    {
        rules.Add(Rule(
            CssSelector.Where(
                CssSelector.Type("native-ui"),
                CssSelector.Descendant(CssSelector.Type("native-ui"), CssSelector.All())),
            new()
            {
                ["box-sizing"] = "border-box",
                ["border"] = "0 solid"
            }));

        rules.Add(Rule(
            CssSelector.Where(CssSelector.Type("native-ui")),
            new()
            {
                ["line-height"] = "1.25"
            }));

        rules.Add(Rule(
            CssSelector.Where(
                CssSelector.Type("native-ui"),
                CssSelector.Descendant(CssSelector.Type("native-ui"), CssSelector.Type("span")),
                CssSelector.Descendant(CssSelector.Type("native-ui"), CssSelector.Type("button"))),
            new()
            {
                ["display"] = "block"
            }));
    }

    private static void AddControlResetRules(List<CssRule> rules)
    {
        rules.Add(Rule(
            CssSelector.Where(CssSelector.Descendant(CssSelector.Type("native-ui"), CssSelector.Type("button"))),
            new()
            {
                ["margin"] = "0",
                ["padding"] = "0",
                ["background"] = "none",
                ["color"] = "inherit",
                ["font"] = "inherit",
                ["user-select"] = "none",
                ["width"] = "fit-content"
            }));

        rules.Add(Rule(
            CssSelector.Where(CssSelector.Descendant(CssSelector.Type("native-ui"), CssSelector.Type("input"))),
            new()
            {
                ["display"] = "block",
                ["width"] = "100%",
                ["margin"] = "0",
                ["padding"] = "0",
                ["outline"] = "none",
                ["background"] = "none",
                ["color"] = "inherit",
                ["font"] = "inherit"
            }));

        rules.Add(Rule(
            CssSelector.Where(CssSelector.Descendant(CssSelector.Type("native-ui"), CssSelector.Type("canvas"))),
            new()
            {
                ["display"] = "block",
                ["width"] = "100%",
                ["height"] = "0"
            }));
    }

    private static void AddComponentRules(List<CssRule> rules)
    {
        rules.Add(Rule(
            CssSelector.Where(CssSelector.Attribute(StyleSheetCss.PortalAttribute)),
            new()
            {
                ["position"] = "fixed",
                ["inset"] = "auto",
                ["margin"] = "0",
                ["padding"] = "0",
                ["width"] = "fit-content",
                ["max-width"] = "none",
                ["max-height"] = "none",
                ["background"] = "none",
                ["color"] = "inherit",
                ["z-index"] = "1000"
            }));

        rules.Add(Rule(
            CssSelector.Where(CssSelector.Attribute(StyleSheetCss.ProgressBarAttribute)),
            new()
            {
                ["position"] = "relative",
                ["overflow"] = "hidden",
                ["height"] = "8px"
            }));

        rules.Add(Rule(
            CssSelector.Where(CssSelector.Descendant(CssSelector.Type("native-ui"), CssSelector.Type("dialog"))).PseudoElement("backdrop"),
            new()
            {
                ["background"] = "rgb(0 0 0 / 0.5)"
            }));

        rules.Add(Rule(
            CssSelector.Where(CssSelector.Attribute(StyleSheetCss.ScrollViewAttribute)),
            new()
            {
                ["overflow"] = "auto"
            }));
    }

    private static void AddScrollbarRules(List<CssRule> rules)
    {
        StyleSelector scrollbarBase = CssSelector.Where(
            CssSelector.Type("native-ui"),
            CssSelector.Descendant(CssSelector.Type("native-ui"), CssSelector.All()));

        rules.Add(Rule(
            scrollbarBase.PseudoElement("-webkit-scrollbar"),
            new()
            {
                ["width"] = "10px",
                ["height"] = "10px"
            }));

        rules.Add(Rule(
            scrollbarBase.PseudoElement("-webkit-scrollbar-track"),
            new()
            {
                ["background"] = "transparent"
            }));

        rules.Add(Rule(
            scrollbarBase.PseudoElement("-webkit-scrollbar-corner"),
            new()
            {
                ["background"] = "transparent"
            }));

        rules.Add(Rule(
            scrollbarBase.PseudoElement("-webkit-scrollbar-thumb"),
            new()
            {
                ["border"] = "2px solid transparent",
                ["border-radius"] = "5px",
                ["background-clip"] = "padding-box"
            }));

        rules.Add(Rule(
            ThemedScrollbarSelector("light").PseudoElement("-webkit-scrollbar-thumb"),
            new()
            {
                ["background-color"] = "rgb(0 0 0 / 0.35)"
            }));

        rules.Add(Rule(
            ThemedScrollbarSelector("dark").PseudoElement("-webkit-scrollbar-thumb"),
            new()
            {
                ["background-color"] = "rgb(255 255 255 / 0.35)"
            }));
    }

    private static StyleSelector ThemedScrollbarSelector(string theme)
    {
        return CssSelector.Where(
            CssSelector.Type("native-ui").Attribute("data-theme", theme),
            CssSelector.Descendant(CssSelector.Type("native-ui").Attribute("data-theme", theme), CssSelector.All()));
    }

    private static void AddThemeRules(List<CssRule> rules)
    {
        rules.Add(Rule(
            CssSelector.Where(CssSelector.Type("native-ui").Attribute("data-theme", "light")),
            new()
            {
                ["color-scheme"] = "light"
            }));

        rules.Add(Rule(
            CssSelector.Where(CssSelector.Type("native-ui").Attribute("data-theme", "dark")),
            new()
            {
                ["color-scheme"] = "dark"
            }));
    }

    private static void AddReducedMotionRules(List<CssRule> rules)
    {
        rules.Add(Rule(
            CssSelector.Where(
                CssSelector.Type("native-ui"),
                CssSelector.Descendant(CssSelector.Type("native-ui"), CssSelector.All())),
            new()
            {
                ["transition-duration"] = "0s !important",
                ["animation-duration"] = "0s !important",
                ["animation-iteration-count"] = "1 !important"
            },
            media: CssMedia.ReducedMotion));
    }

    private static CssRule Rule(
        StyleSelector selector,
        Dictionary<string, string> declarations,
        CssMedia media = default)
    {
        Dictionary<string, CssValue> values = new(declarations.Count, StringComparer.Ordinal);
        foreach ((string property, string value) in declarations)
        {
            values[property] = new CssStringValue(value);
        }

        return new CssRule(selector, values, media: media);
    }
}
