namespace NativeUI.Controls.Web;

using System.Collections.Generic;
using System.Text;
using NativeUI.Css;
using NativeUI.Core;
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
        AddCustomElementRules(rules);
        AddTextRules(rules);
        rules.Add(Rule(
            CssSelector.Where(
                CssSelector.Descendant(CssSelector.Type("native-ui"), CssSelector.Type("nui-tabcontrol")),
                CssSelector.Descendant(CssSelector.Type("native-ui"), CssSelector.Type("nui-tabitem"))),
            new()
            {
                ["display"] = "block"
            }));

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
                CssSelector.Descendant(CssSelector.Type("native-ui"), CssSelector.Type("button")),
                CssSelector.Descendant(CssSelector.Type("native-ui"), CssSelector.Type("img")),
                CssSelector.Descendant(CssSelector.Type("native-ui"), CssSelector.Type("svg"))),
            new()
            {
                ["display"] = "block"
            }));
    }

    private static void AddControlResetRules(List<CssRule> rules)
    {
        rules.Add(Rule(
            CssSelector.Where(CssSelector.Descendant(CssSelector.Type("native-ui"), CssSelector.Type("fieldset")),
                CssSelector.Descendant(CssSelector.Type("native-ui"), CssSelector.Type("legend")),
                CssSelector.Descendant(CssSelector.Type("native-ui"), CssSelector.Type("form"))),
            new() { ["margin"] = "0", ["padding"] = "0", ["min-width"] = "0", ["font"] = "inherit", ["color"] = "inherit" }));

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
            CssSelector.Where(CssSelector.Descendant(CssSelector.Type("native-ui"), CssSelector.Type("nui-sheet"))),
            new()
            {
                ["display"] = "block"
            }));

        rules.Add(Rule(
            CssSelector.Where(CssSelector.Descendant(
                [CssSelector.Type("native-ui"), CssSelector.Type("nui-sheet")],
                CssSelector.Attribute(StyleSheetCss.PartAttribute, "popup"))),
            new()
            {
                ["border"] = "0",
                ["box-sizing"] = "border-box"
            }));

        rules.Add(Rule(
            CssSelector.Where(CssSelector.Descendant(
                [CssSelector.Type("native-ui"), CssSelector.Type("nui-sheet")],
                CssSelector.Attribute(StyleSheetCss.PartAttribute, "popup"))).PseudoElement("backdrop"),
            new()
            {
                ["background"] = "transparent"
            }));

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
                ["overflow"] = "visible",
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

    /// <summary>
    /// Lets text break inside a word that does not fit its line, as the desktop line breaker does.
    /// </summary>
    private static void AddTextRules(List<CssRule> rules)
    {
        rules.Add(Rule(
            CssSelector.Where(CssSelector.Descendant(CssSelector.Type("native-ui"), CssSelector.Type("a"))),
            new()
            {
                ["text-decoration"] = "none"
            }));
        rules.Add(Rule(
            CssSelector.Where(CssSelector.Descendant(CssSelector.Type("native-ui"),
                CssSelector.Type("a").State(ElementStates.Hover))),
            new()
            {
                ["text-decoration"] = "underline"
            }));
        rules.Add(Rule(
            CssSelector.Where(CssSelector.Descendant(CssSelector.Type("native-ui"),
                CssSelector.Type("a").State(ElementStates.FocusVisible))),
            new()
            {
                ["text-decoration"] = "underline"
            }));

        rules.Add(Rule(
            CssSelector.Where(CssSelector.Descendant(CssSelector.Type("native-ui"), CssSelector.Type("span"))),
            new()
            {
                ["overflow-wrap"] = "anywhere",
                ["flex-shrink"] = "0"
            }));
    }

    private static void AddCustomElementRules(List<CssRule> rules)
    {
        rules.Add(Rule(
            CssSelector.Where(
                CssSelector.Descendant(CssSelector.Type("native-ui"), CssSelector.Type("nui-switch")),
                CssSelector.Descendant(CssSelector.Type("native-ui"), CssSelector.Type("nui-toggle"))),
            new()
            {
                ["display"] = "block",
                ["width"] = "fit-content",
                ["cursor"] = "pointer",
                ["user-select"] = "none"
            }));

        rules.Add(Rule(
            CssSelector.Where(CssSelector.Descendant(CssSelector.Type("native-ui"), CssSelector.Type("nui-checkbox"))),
            new()
            {
                ["display"] = "flex",
                ["flex-direction"] = "row",
                ["align-items"] = "flex-start",
                ["width"] = "fit-content",
                ["cursor"] = "pointer",
                ["user-select"] = "none"
            }));

        rules.Add(Rule(
            CssSelector.Where(CssSelector.Descendant(CssSelector.Type("native-ui"), CssSelector.Type("nui-radiobutton"))),
            new()
            {
                ["display"] = "flex",
                ["flex-direction"] = "row",
                ["align-items"] = "flex-start",
                ["width"] = "fit-content",
                ["cursor"] = "pointer",
                ["user-select"] = "none"
            }));

        rules.Add(Rule(
            CssSelector.Where(CssSelector.Descendant(CssSelector.Type("native-ui"), CssSelector.Type("nui-radiogroupitem"))),
            new()
            {
                ["display"] = "flex",
                ["flex-direction"] = "row",
                ["align-items"] = "flex-start",
                ["width"] = "fit-content",
                ["cursor"] = "pointer",
                ["user-select"] = "none"
            }));

        rules.Add(Rule(
            CssSelector.Where(CssSelector.Descendant(CssSelector.Type("native-ui"), CssSelector.Type("nui-togglegroup"))),
            new()
            {
                ["display"] = "flex",
                ["flex-direction"] = "row",
                ["align-items"] = "flex-start",
                ["width"] = "fit-content"
            }));

        rules.Add(Rule(
            CssSelector.Where(CssSelector.Descendant(CssSelector.Type("native-ui"), CssSelector.Attribute("data-orientation", "horizontal"))),
            new()
            {
                ["display"] = "flex",
                ["flex-direction"] = "row",
                ["align-items"] = "flex-start"
            }));

        rules.Add(Rule(
            CssSelector.Where(CssSelector.Descendant(CssSelector.Type("native-ui"), CssSelector.Attribute("data-orientation", "vertical"))),
            new()
            {
                ["display"] = "flex",
                ["flex-direction"] = "column",
                ["align-items"] = "stretch"
            }));

        rules.Add(Rule(
            CssSelector.Where(CssSelector.Descendant(CssSelector.Type("native-ui"), CssSelector.Type("nui-list"))),
            new()
            {
                ["display"] = "flex",
                ["flex-direction"] = "column",
                ["overflow-y"] = "auto"
            }));

        rules.Add(Rule(
            CssSelector.Where(CssSelector.Descendant(CssSelector.Type("native-ui"), CssSelector.Type("nui-virtualizedlist"))),
            new()
            {
                ["display"] = "block",
                ["overflow-y"] = "auto"
            }));

        rules.Add(Rule(
            CssSelector.Where(
                CssSelector.Descendant(CssSelector.Type("native-ui"), CssSelector.Type("nui-table")),
                CssSelector.Descendant(CssSelector.Type("native-ui"), CssSelector.Type("nui-virtualizedtable"))),
            new()
            {
                ["display"] = "block",
                ["overflow"] = "auto"
            }));

        rules.Add(Rule(
            CssSelector.Where(
                CssSelector.Descendant(CssSelector.Type("native-ui"), CssSelector.Type("nui-listitem")),
                CssSelector.Descendant(CssSelector.Type("native-ui"), CssSelector.Type("nui-tablecell"))),
            new()
            {
                ["display"] = "block",
                ["min-width"] = "0"
            }));

        rules.Add(Rule(
            CssSelector.Where(CssSelector.Descendant(CssSelector.Type("native-ui"), CssSelector.Type("nui-tablerow"))),
            new()
            {
                ["position"] = "relative"
            }));

        rules.Add(Rule(
            CssSelector.Where(
                CssSelector.Descendant(CssSelector.Type("native-ui"), CssSelector.Type("nui-separator")),
                CssSelector.Descendant(CssSelector.Type("native-ui"), CssSelector.Type("nui-resizablepanel")),
                CssSelector.Descendant(CssSelector.Type("native-ui"), CssSelector.Type("nui-splitview")),
                CssSelector.Descendant(CssSelector.Type("native-ui"), CssSelector.Type("nav")),
                CssSelector.Descendant(CssSelector.Type("native-ui"), CssSelector.Type("nui-navitem")),
                CssSelector.Descendant(CssSelector.Type("native-ui"), CssSelector.Type("nui-navitem-header")),
                CssSelector.Descendant(CssSelector.Type("native-ui"), CssSelector.Type("nui-menuseparator")),
                CssSelector.Descendant(CssSelector.Type("native-ui"), CssSelector.Type("nui-menugroup"))),
            new()
            {
                ["display"] = "block"
            }));

        rules.Add(Rule(
            CssSelector.Where(
                CssSelector.Descendant(CssSelector.Type("native-ui"), CssSelector.Type("nui-splitview")),
                CssSelector.Descendant(CssSelector.Type("native-ui"), CssSelector.Type("nav"))),
            new()
            {
                ["width"] = "100%",
                ["height"] = "100%"
            }));

        rules.Add(Rule(
            CssSelector.Where(CssSelector.Descendant(CssSelector.Type("native-ui"), CssSelector.Type("nui-navitem"))),
            new()
            {
                ["display"] = "block",
                ["width"] = "100%",
                ["box-sizing"] = "border-box",
                ["cursor"] = "pointer",
                ["user-select"] = "none"
            }));

        rules.Add(Rule(
            CssSelector.Where(CssSelector.Descendant(CssSelector.Type("native-ui"), CssSelector.Type("nui-resizable"))),
            new()
            {
                ["display"] = "flex",
                ["width"] = "100%",
                ["height"] = "100%",
                ["overflow"] = "hidden"
            }));

        rules.Add(Rule(
            CssSelector.Where(CssSelector.Descendant(
                CssSelector.Type("native-ui"),
                CssSelector.Type("nui-resizable").Attribute("data-orientation", "horizontal"))),
            new()
            {
                ["flex-direction"] = "row"
            }));

        rules.Add(Rule(
            CssSelector.Where(CssSelector.Descendant(
                CssSelector.Type("native-ui"),
                CssSelector.Type("nui-resizable").Attribute("data-orientation", "vertical"))),
            new()
            {
                ["flex-direction"] = "column"
            }));

        rules.Add(Rule(
            CssSelector.Where(CssSelector.Descendant(CssSelector.Type("native-ui"), CssSelector.Type("nui-resizablepanel"))),
            new()
            {
                ["display"] = "block",
                ["box-sizing"] = "border-box",
                ["overflow"] = "auto",
                ["position"] = "relative"
            }));

        rules.Add(Rule(
            CssSelector.Where(CssSelector.Descendant(CssSelector.Type("native-ui"), CssSelector.Type("nui-resizablehandle"))),
            new()
            {
                ["display"] = "block",
                ["user-select"] = "none",
                ["touch-action"] = "none",
                ["flex"] = "0 0 auto",
                ["align-self"] = "stretch"
            }));

        rules.Add(Rule(
            CssSelector.Where(CssSelector.Descendant(
                CssSelector.Type("native-ui"),
                CssSelector.Type("nui-resizablehandle").Attribute("data-orientation", "horizontal"))),
            new()
            {
                ["cursor"] = "col-resize",
                ["width"] = "6px"
            }));

        rules.Add(Rule(
            CssSelector.Where(CssSelector.Descendant(
                CssSelector.Type("native-ui"),
                CssSelector.Type("nui-resizablehandle").Attribute("data-orientation", "vertical"))),
            new()
            {
                ["cursor"] = "row-resize",
                ["height"] = "6px"
            }));

        rules.Add(Rule(
            CssSelector.Where(
                CssSelector.Descendant(
                    [CssSelector.Type("native-ui"), CssSelector.Type("nui-splitview")],
                    CssSelector.Attribute(StyleSheetCss.PartAttribute, "layout")),
                CssSelector.Descendant(
                    [CssSelector.Type("native-ui"), CssSelector.Type("nui-splitview")],
                    CssSelector.Attribute(StyleSheetCss.PartAttribute, "pane")),
                CssSelector.Descendant(
                    [CssSelector.Type("native-ui"), CssSelector.Type("nui-splitview")],
                    CssSelector.Attribute(StyleSheetCss.PartAttribute, "content"))),
            new()
            {
                ["height"] = "100%",
                ["min-height"] = "inherit"
            }));

        rules.Add(Rule(
            CssSelector.Where(
                CssSelector.Descendant(CssSelector.Type("native-ui"), CssSelector.Class("navigation-pane-root")),
                CssSelector.Descendant(CssSelector.Type("native-ui"), CssSelector.Class("navigation-pane-grid")),
                CssSelector.Descendant(CssSelector.Type("native-ui"), CssSelector.Class("navigation-content-area")),
                CssSelector.Descendant(CssSelector.Type("native-ui"), CssSelector.Class("navigation-content-host"))),
            new()
            {
                ["height"] = "100%",
                ["box-sizing"] = "border-box"
            }));

        rules.Add(Rule(
            CssSelector.Where(CssSelector.Descendant(CssSelector.Type("native-ui"), CssSelector.Type("nui-badge"))),
            new()
            {
                ["display"] = "block",
                ["width"] = "fit-content"
            }));

        rules.Add(Rule(
            CssSelector.Where(CssSelector.Descendant(CssSelector.Type("native-ui"), CssSelector.Type("nui-avatar"))),
            new()
            {
                ["display"] = "block",
                ["position"] = "relative",
                ["overflow"] = "hidden"
            }));

        rules.Add(Rule(
            CssSelector.Where(
                CssSelector.Descendant(CssSelector.Type("native-ui"), CssSelector.Type("nui-breadcrumb")),
                CssSelector.Descendant(CssSelector.Type("native-ui"), CssSelector.Type("nui-breadcrumbitem")),
                CssSelector.Descendant(CssSelector.Type("native-ui"), CssSelector.Type("nui-buttongroup"))),
            new()
            {
                ["display"] = "flex",
                ["flex-direction"] = "row",
                ["align-items"] = "flex-start",
                ["width"] = "fit-content"
            }));

        rules.Add(Rule(
            CssSelector.Where(CssSelector.Descendant(CssSelector.Type("native-ui"), CssSelector.Type("nui-breadcrumbitem"))),
            new()
            {
                ["user-select"] = "none"
            }));

        rules.Add(Rule(
            CssSelector.Where(
                CssSelector.Descendant(CssSelector.Type("native-ui"), CssSelector.Type("nui-select")),
                CssSelector.Descendant(CssSelector.Type("native-ui"), CssSelector.Type("nui-popover")),
                CssSelector.Descendant(CssSelector.Type("native-ui"), CssSelector.Type("nui-tooltip")),
                CssSelector.Descendant(CssSelector.Type("native-ui"), CssSelector.Type("nui-dropdownmenu")),
                CssSelector.Descendant(CssSelector.Type("native-ui"), CssSelector.Type("nui-contextmenu"))),
            new()
            {
                ["display"] = "block",
                ["width"] = "fit-content"
            }));

        rules.Add(Rule(
            CssSelector.Where(CssSelector.Descendant(CssSelector.Type("native-ui"), CssSelector.Type("nui-menuitem"))),
            new()
            {
                ["display"] = "flex",
                ["flex-direction"] = "row",
                ["align-items"] = "center",
                ["width"] = "100%",
                ["box-sizing"] = "border-box",
                ["cursor"] = "pointer",
                ["user-select"] = "none"
            }));

        rules.Add(Rule(
            CssSelector.Where(CssSelector.Descendant(
                [CssSelector.Type("native-ui"), CssSelector.Type("nui-menuitem")],
                CssSelector.Attribute(StyleSheetCss.PartAttribute, "icon"))),
            new()
            {
                ["display"] = "inline-flex",
                ["align-items"] = "center",
                ["justify-content"] = "center",
                ["margin-right"] = "8px",
                ["flex-shrink"] = "0"
            }));

        rules.Add(Rule(
            CssSelector.Where(CssSelector.Descendant(
                [CssSelector.Type("native-ui"), CssSelector.Type("nui-menuitem")],
                CssSelector.Attribute(StyleSheetCss.PartAttribute, "label"))),
            new()
            {
                ["flex"] = "1 1 auto"
            }));

        rules.Add(Rule(
            CssSelector.Where(CssSelector.Descendant(
                [CssSelector.Type("native-ui"), CssSelector.Type("nui-menuitem")],
                CssSelector.Attribute(StyleSheetCss.PartAttribute, "shortcut"))),
            new()
            {
                ["margin-left"] = "auto"
            }));

        // The user agent stylesheet makes a popover element scroll its content.
        rules.Add(Rule(
            CssSelector.Where(CssSelector.Attribute(StyleSheetCss.PortalAttribute).Attribute("popover")),
            new()
            {
                ["overflow"] = "visible"
            }));
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
