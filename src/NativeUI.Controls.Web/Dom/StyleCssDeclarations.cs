using System.Collections.Immutable;
using System.Globalization;
using NativeUI.Layout;
using NativeUI.Media;
using NativeUI.Styling;

namespace NativeUI.Controls.Web;

/// <summary>
/// Converts specified style values to browser CSS declarations for inline styles and generated stylesheets.
/// </summary>
internal static class StyleCssDeclarations
{
    /// <summary>
    /// Returns every supported CSS property with its value, or a null value when <paramref name="style"/> leaves it unspecified.
    /// </summary>
    public static (string Property, string? Value)[] Get(IStyle? style)
    {
        return
        [
            ("color", FormatColor(style?.Color)),
            ("background-color", FormatColor(style?.BackgroundColor)),
            ("font-size", FormatLength(style?.FontSize)),
            ("font-weight", style?.FontWeight is FontWeight weight ? ((int)weight).ToString(CultureInfo.InvariantCulture) : null),
            ("font-family", style?.FontFamily),
            ("text-align", FormatTextAlign(style?.TextAlign)),
            ("line-height", FormatLineHeight(style?.LineHeight)),
            ("white-space", style?.WhiteSpace switch { WhiteSpace.NoWrap => "nowrap", WhiteSpace.Normal => "normal", _ => null }),
            ("text-overflow", style?.TextOverflow switch { TextOverflow.Ellipsis => "ellipsis", TextOverflow.Clip => "clip", _ => null }),
            ("align-items", FormatAlignItems(style?.AlignItems)),
            ("padding", FormatThickness(style?.Padding)),
            ("margin", FormatThickness(style?.Margin)),
            ("gap", FormatLength(style?.Gap)),
            ("border-radius", FormatCornerRadius(style?.BorderRadius)),
            ("border-width", FormatLength(style?.BorderWidth)),
            ("border-color", FormatColor(style?.BorderColor)),
            ("box-shadow", FormatBoxShadows(style?.BoxShadows)),
            ("opacity", style?.Opacity is double opacity ? Math.Clamp(opacity, 0, 1).ToString("R", CultureInfo.InvariantCulture) : null),
            ("width", FormatWidth(style?.Width)),
            ("height", FormatHeight(style?.Height)),
            ("min-width", FormatWidth(style?.MinWidth)),
            ("max-width", FormatWidth(style?.MaxWidth)),
            ("min-height", FormatHeight(style?.MinHeight)),
            ("max-height", FormatHeight(style?.MaxHeight)),
            ("cursor", FormatCursor(style?.Cursor)),
            ("overflow-x", FormatOverflow(style?.OverflowX)),
            ("overflow-y", FormatOverflow(style?.OverflowY)),
            ("scrollbar-color", FormatScrollbarColor(style)),
            ("scrollbar-width", FormatScrollbarWidth(style?.ScrollbarWidth)),
            ("transform", FormatTransform(style?.Transform)),
            ("z-index", style?.ZIndex?.ToString(CultureInfo.InvariantCulture)),
            ("position", FormatPosition(style?.Position)),
            ("anchor-name", style?.AnchorName),
            ("position-anchor", style?.PositionAnchor),
            ("position-area", FormatPositionArea(style?.PositionArea)),
            ("position-try-fallbacks", FormatPositionTryFallbacks(style?.PositionTryFallbacks)),
            ("top", FormatInset(style?.Top)),
            ("right", FormatInset(style?.Right)),
            ("bottom", FormatInset(style?.Bottom)),
            ("left", FormatInset(style?.Left))
        ];
    }

    /// <summary>
    /// Formats the CSS property, which requires both colors. An unspecified track is transparent.
    /// </summary>
    private static string? FormatScrollbarColor(IStyle? style)
    {
        string? thumb = FormatColor(style?.ScrollbarThumbColor);
        return thumb is null ? null : $"{thumb} {FormatColor(style?.ScrollbarTrackColor) ?? "transparent"}";
    }

    private static string? FormatScrollbarWidth(ScrollbarWidth? width)
    {
        return width switch
        {
            ScrollbarWidth.Auto => "auto",
            ScrollbarWidth.Thin => "thin",
            ScrollbarWidth.None => "none",
            _ => null
        };
    }

    private static string? FormatOverflow(Overflow? overflow)
    {
        return overflow switch
        {
            Overflow.Visible => "visible",
            Overflow.Hidden => "hidden",
            Overflow.Auto => "auto",
            Overflow.Scroll => "scroll",
            _ => null
        };
    }

    private static string? FormatCursor(Cursor? cursor)
    {
        return cursor switch
        {
            Cursor.Pointer => "pointer",
            Cursor.IBeam => "text",
            Cursor.NotAllowed => "not-allowed",
            Cursor.Crosshair => "crosshair",
            Cursor.ColumnResize => "col-resize",
            Cursor.RowResize => "row-resize",
            Cursor.Wait => "wait",
            Cursor.Arrow or Cursor.Default => "default",
            Cursor.Auto => "auto",
            _ => null
        };
    }

    private static string? FormatPosition(Position? position)
    {
        return position switch
        {
            Position.Relative => "relative",
            Position.Absolute => "absolute",
            Position.Fixed => "fixed",
            Position.Sticky => "sticky",
            Position.Static => "static",
            _ => null
        };
    }

    private static string? FormatColor(ThemeValue<Color>? value)
    {
        if (value is not ThemeValue<Color> color)
        {
            return null;
        }

        if (color.IsThemeKey)
        {
            return color.HasValue
                ? $"var({color.Key}, {color.Value.ToCssString()})"
                : $"var({color.Key})";
        }

        return color.Value.ToCssString();
    }

    private static string? FormatWidth(WidthValue? value)
    {
        return value switch
        {
            null => null,
            { IsAuto: true } => "auto",
            { IsFitContent: true } => "fit-content",
            { IsPercent: true } percent => percent.Value.ToString("R", CultureInfo.InvariantCulture) + "%",
            { } pixels => FormatLength(pixels.Value)
        };
    }

    private static string? FormatInset(InsetValue? value)
    {
        return value switch
        {
            null => null,
            { IsAuto: true } => "auto",
            { IsAnchor: true } anchor => "anchor(" + (anchor.AnchorName is string name ? name + " " : "") + FormatAnchorSide(anchor.AnchorSide) + ")",
            { IsPercent: true } percent => percent.Value.ToString("R", CultureInfo.InvariantCulture) + "%",
            { } pixels => FormatLength(pixels.Value)
        };
    }

    private static string FormatAnchorSide(AnchorSide side)
    {
        return side switch
        {
            AnchorSide.SelfStart => "self-start",
            AnchorSide.SelfEnd => "self-end",
            _ => side.ToString().ToLowerInvariant()
        };
    }

    internal static string? FormatPositionArea(PositionArea? area)
    {
        return area is PositionArea value
            ? value.IsNone ? "none" : FormatPositionTrack(value.Block, true) + " " + FormatPositionTrack(value.Inline, false)
            : null;
    }

    private static string FormatPositionTrack(PositionAreaTrack track, bool block)
    {
        return track switch
        {
            PositionAreaTrack.Start => block ? "top" : "left",
            PositionAreaTrack.End => block ? "bottom" : "right",
            PositionAreaTrack.SpanStart => block ? "span-top" : "span-left",
            PositionAreaTrack.SpanEnd => block ? "span-bottom" : "span-right",
            PositionAreaTrack.Center => "center",
            PositionAreaTrack.SpanAll => "span-all",
            _ => throw new ArgumentOutOfRangeException(nameof(track))
        };
    }

    internal static string? FormatPositionTryFallbacks(ImmutableArray<PositionTryTactic>? fallbacks)
    {
        if (fallbacks is not ImmutableArray<PositionTryTactic> values)
        {
            return null;
        }

        if (values.IsDefaultOrEmpty)
        {
            return "none";
        }

        return string.Join(", ", values.Select(value => string.Join(" ", new[]
        {
            value.HasFlag(PositionTryTactic.FlipBlock) ? "flip-block" : null,
            value.HasFlag(PositionTryTactic.FlipInline) ? "flip-inline" : null,
            value.HasFlag(PositionTryTactic.FlipStart) ? "flip-start" : null,
            value.HasFlag(PositionTryTactic.FlipX) ? "flip-x" : null,
            value.HasFlag(PositionTryTactic.FlipY) ? "flip-y" : null
        }.Where(static keyword => keyword is not null))));
    }

    private static string? FormatHeight(HeightValue? value)
    {
        return value switch
        {
            null => null,
            { IsAuto: true } => "auto",
            { IsPercent: true } percent => percent.Value.ToString("R", CultureInfo.InvariantCulture) + "%",
            { } pixels => FormatLength(pixels.Value)
        };
    }

    private static string? FormatTransform(StyleTransform? value)
    {
        if (value is not StyleTransform transform)
        {
            return null;
        }

        if (transform.Rotation != 0)
        {
            return string.Create(
                CultureInfo.InvariantCulture,
                $"translate(calc({transform.TranslateX:R}px + {transform.TranslateXPercent:R}%), calc({transform.TranslateY:R}px + {transform.TranslateYPercent:R}%)) rotate({transform.Rotation:R}deg) scale({transform.ScaleX:R}, {transform.ScaleY:R})");
        }

        return string.Create(
            CultureInfo.InvariantCulture,
            $"translate(calc({transform.TranslateX:R}px + {transform.TranslateXPercent:R}%), calc({transform.TranslateY:R}px + {transform.TranslateYPercent:R}%)) scale({transform.ScaleX:R}, {transform.ScaleY:R})");
    }

    private static string? FormatLength(double? value)
    {
        return value is double length ? length.ToString("R", CultureInfo.InvariantCulture) + "px" : null;
    }

    internal static string? FormatThickness(ThicknessValue? value)
    {
        if (value is not ThicknessValue thickness)
        {
            return null;
        }

        return string.Join(
            ' ',
            FormatEdge(thickness.Top),
            FormatEdge(thickness.Right),
            FormatEdge(thickness.Bottom),
            FormatEdge(thickness.Left));
    }

    private static string FormatEdge(EdgeValue edge)
    {
        return FormatLengthOrPercent(edge.Value, edge.IsPercent);
    }

    /// <summary>
    /// Formats uniform corners as one length and other corners as four lengths from top-left.
    /// </summary>
    private static string? FormatCornerRadius(CornerRadiusValue? value)
    {
        if (value is not CornerRadiusValue radius)
        {
            return null;
        }

        if (radius.IsUniform)
        {
            return FormatRadius(radius.TopLeft);
        }

        return string.Join(
            ' ',
            FormatRadius(radius.TopLeft),
            FormatRadius(radius.TopRight),
            FormatRadius(radius.BottomRight),
            FormatRadius(radius.BottomLeft));
    }

    private static string FormatRadius(RadiusValue radius)
    {
        return FormatLengthOrPercent(radius.Value, radius.IsPercent);
    }

    private static string FormatLengthOrPercent(double value, bool isPercent)
    {
        return value.ToString("R", CultureInfo.InvariantCulture) + (isPercent ? "%" : "px");
    }

    /// <summary>
    /// Formats each shadow as its offsets, blur, spread, optional color, and optional <c>inset</c>, or <c>none</c> for an
    /// empty list. A shadow without a color uses <c>currentColor</c>.
    /// </summary>
    private static string? FormatBoxShadows(ImmutableArray<BoxShadow>? value)
    {
        if (value is not ImmutableArray<BoxShadow> shadows)
        {
            return null;
        }

        if (shadows.IsDefaultOrEmpty)
        {
            return "none";
        }

        List<string> parts = [];
        foreach (BoxShadow shadow in shadows)
        {
            string color = shadow.Color?.ToCssString() ?? "currentColor";
            string inset = shadow.IsInset ? " inset" : string.Empty;
            parts.Add(string.Create(CultureInfo.InvariantCulture,
                $"{shadow.OffsetX:R}px {shadow.OffsetY:R}px {shadow.BlurRadius:R}px {shadow.SpreadRadius:R}px {color}{inset}"));
        }

        return string.Join(", ", parts);
    }

    private static string? FormatLineHeight(LineHeight? value)
    {
        return value switch
        {
            null => null,
            { IsNormal: true } => "normal",
            { IsMultiple: true } multiple => multiple.Value.ToString("R", CultureInfo.InvariantCulture),
            { } pixels => FormatLength(pixels.Value)
        };
    }

    private static string? FormatAlignItems(Alignment? value)
    {
        return value switch
        {
            Alignment.Start => "flex-start",
            Alignment.Center => "center",
            Alignment.End => "flex-end",
            _ => null
        };
    }

    private static string? FormatTextAlign(TextAlign? value)
    {
        return value switch
        {
            TextAlign.Left => "left",
            TextAlign.Center => "center",
            TextAlign.Right => "right",
            _ => null
        };
    }
}
