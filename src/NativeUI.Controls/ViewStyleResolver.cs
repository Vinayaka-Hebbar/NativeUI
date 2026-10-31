using NativeUI.Core;
using NativeUI.Media;
using NativeUI.Styling;

namespace NativeUI.Controls;

/// <summary>
/// Cascades matching rules from ancestor <see cref="View.StyleSheet"/> declarations.
/// </summary>
internal static class ViewStyleResolver
{
    public static ComputedStyle Resolve(UIElement element, ComputedStyle baseStyle, IStyleResolver? resolver)
    {
        List<(View OwningView, StyleSheet Sheet)>? sheets = null;
        for (UIElement? current = element; current is not null; current = current.Parent)
        {
            if (current is View { StyleSheet: StyleSheet sheet } view && sheet.Rules.Count > 0)
            {
                sheets ??= [];
                sheets.Add((view, sheet));
            }
        }

        if (sheets is null)
        {
            return baseStyle;
        }

        sheets.Reverse();

        ComputedStyle computed = baseStyle;
        List<(StyleRule Rule, SelectorSpecificity Specificity, int SheetIndex, int RuleIndex)>? matched = null;
        for (int s = 0; s < sheets.Count; s++)
        {
            (View container, StyleSheet sheet) = sheets[s];
            if (sheet.Rules.Count == 0)
            {
                computed = sheet.ResolveStyle(element);
                continue;
            }

            for (int r = 0; r < sheet.Rules.Count; r++)
            {
                StyleRule rule = sheet.Rules[r];
                if (rule.Target.Matches(element, container))
                {
                    matched ??= [];
                    matched.Add((rule, rule.Target.Specificity, s, r));
                }
            }
        }

        if (matched is null)
        {
            return computed;
        }

        matched.Sort(static (a, b) =>
        {
            int spec = a.Specificity.CompareTo(b.Specificity);
            if (spec != 0)
            {
                return spec;
            }

            int sheet = a.SheetIndex.CompareTo(b.SheetIndex);
            if (sheet != 0)
            {
                return sheet;
            }

            return a.RuleIndex.CompareTo(b.RuleIndex);
        });

        foreach ((StyleRule rule, _, _, _) in matched)
        {
            computed = computed.ApplyInline(rule.Style, resolver, element);
        }

        return computed;
    }
}
