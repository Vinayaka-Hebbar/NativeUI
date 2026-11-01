using System.Diagnostics;
using System.Globalization;
using NativeUI.Controls;
using NativeUI.Core;
using NativeUI.Graphics;
using NativeUI.Layout;
using NativeUI.Media;

namespace NativeUI.DevTools;

/// <summary>
/// Draws a DevTools button that opens a panel of tools, a frame rate readout, and the bounds of each layout box over a
/// Skia-rendered visual tree. The button and panel keep their size and take pointer input from the visual tree under them.
/// </summary>
public sealed class DevToolsOverlay : IRenderOverlay
{
    private const double OutlineThickness = 1;
    private const double EdgeMargin = 8;
    private const double ButtonHeight = 28;
    private const double ButtonPadding = 10;
    private const double ButtonGap = 7;
    private const double ButtonRadius = 7;
    private const double ButtonOpacity = 0.88;
    private const double PanelGap = 6;
    private const double PanelWidth = 232;
    private const double PanelPadding = 6;
    private const double PanelRadius = 9;
    private const double PanelOpacity = 0.96;
    private const double RowHeight = 32;
    private const double RowPadding = 8;
    private const double RowRadius = 6;
    private const double RowHoverOpacity = 0.12;
    private const double LegendHeight = 22;
    private const double LegendSwatch = 8;
    private const double LegendGap = 12;
    private const double FooterHeight = 30;
    private const double DividerOpacity = 0.18;
    private const double SwitchWidth = 28;
    private const double SwitchHeight = 16;
    private const double SwitchInset = 2;
    private const string LayoutBoundsShortcutKey = "l";

    private static readonly Color s_marginColor = Color.FromSrgb(0.96, 0.62, 0.04, 1);
    private static readonly Color s_borderColor = Color.FromSrgb(0.93, 0.11, 0.55, 1);
    private static readonly Color s_contentColor = Color.FromSrgb(0.05, 0.65, 0.91, 1);
    private static readonly Color s_titleColor = Color.FromSrgb(0.62, 0.77, 1, 1);
    private static readonly Color s_secondaryTextColor = Color.FromSrgb(0.66, 0.70, 0.76, 1);
    private static readonly Color s_switchOnColor = Color.FromSrgb(0.04, 0.42, 0.94, 1);
    private static readonly Color s_switchOffColor = Color.FromSrgb(0.36, 0.39, 0.45, 1);
    private static readonly ComputedStyle s_labelStyle = new() { FontSize = 12 };
    private static readonly ComputedStyle s_captionStyle = new() { FontSize = 11 };
    private static readonly Size s_unconstrained = new(double.PositiveInfinity, double.PositiveInfinity);

    private readonly IRenderHost _host;
    private readonly Queue<long> _frameTimestamps = new();
    private bool _showFps = true;
    private bool _showLayoutBounds;
    private bool _isPanelOpen;
    private long _frameCount;
    private Rect _buttonBounds;
    private Rect _panelBounds;
    private Rect _fpsRowBounds;
    private Rect _layoutBoundsRowBounds;
    private Target _hovered;
    private Target _pressed;

    internal DevToolsOverlay(IRenderHost host)
    {
        _host = host;
    }

    /// <summary>
    /// Gets or sets a value indicating whether the frame rate readout is drawn. Defaults to <see langword="true"/>.
    /// The readout counts the frames rendered in the last second, and hosts render only when something changes,
    /// so a low number on an idle window is expected.
    /// </summary>
    public bool ShowFps
    {
        get => _showFps;
        set
        {
            _showFps = value;
            _host.InvalidateRender();
        }
    }

    /// <summary>
    /// Gets or sets a value indicating whether layout boxes are outlined, with margin, border, and content boxes
    /// in separate colors. Defaults to <see langword="false"/>.
    /// </summary>
    public bool ShowLayoutBounds
    {
        get => _showLayoutBounds;
        set
        {
            _showLayoutBounds = value;
            _host.InvalidateRender();
        }
    }

    /// <summary>
    /// Gets or sets a value indicating whether the panel is open. Defaults to <see langword="false"/>.
    /// </summary>
    public bool IsPanelOpen
    {
        get => _isPanelOpen;
        set
        {
            _isPanelOpen = value;
            _host.InvalidateRender();
        }
    }

    private enum Target
    {
        None,
        Button,
        FpsRow,
        LayoutBoundsRow,
        Panel
    }

    /// <inheritdoc />
    public void Draw(DrawingContext context, RenderPass pass)
    {
        ArgumentNullException.ThrowIfNull(context);

        int framesPerSecond = RecordFrame();
        _frameCount++;

        if (_showLayoutBounds)
        {
            DrawLayoutBounds(context, pass.Root);
        }

        DrawButton(context, pass, framesPerSecond);
        if (_isPanelOpen)
        {
            DrawPanel(context, pass);
        }
    }

    /// <inheritdoc />
    public bool ContainsPoint(Point point)
    {
        return FindTarget(point) != Target.None;
    }

    /// <inheritdoc />
    public bool HandlePointer(OverlayPointerAction action, Point point)
    {
        Target target = action == OverlayPointerAction.Exited ? Target.None : FindTarget(point);
        if (target != _hovered)
        {
            _hovered = target;
            _host.InvalidateRender();
        }

        switch (action)
        {
            case OverlayPointerAction.Pressed:
                _pressed = target;
                return target != Target.None;
            case OverlayPointerAction.Released:
                if (target == _pressed)
                {
                    Activate(target);
                }

                _pressed = Target.None;
                return true;
            case OverlayPointerAction.Exited:
                _pressed = Target.None;
                return false;
            default:
                return false;
        }
    }

    /// <summary>
    /// Toggles layout bounds on Command+Shift+L, or Control+Shift+L on other platforms.
    /// </summary>
    public bool HandleKey(string key, PointerModifiers modifiers)
    {
        bool hasCommand = modifiers.HasFlag(PointerModifiers.Meta) || modifiers.HasFlag(PointerModifiers.Control);
        if (!hasCommand || !modifiers.HasFlag(PointerModifiers.Shift) ||
            !string.Equals(key, LayoutBoundsShortcutKey, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        ShowLayoutBounds = !_showLayoutBounds;
        return true;
    }

    private Target FindTarget(Point point)
    {
        if (_buttonBounds.Contains(point))
        {
            return Target.Button;
        }

        if (!_isPanelOpen || !_panelBounds.Contains(point))
        {
            return Target.None;
        }

        if (_fpsRowBounds.Contains(point))
        {
            return Target.FpsRow;
        }

        return _layoutBoundsRowBounds.Contains(point) ? Target.LayoutBoundsRow : Target.Panel;
    }

    private void Activate(Target target)
    {
        switch (target)
        {
            case Target.Button:
                IsPanelOpen = !_isPanelOpen;
                break;
            case Target.FpsRow:
                ShowFps = !_showFps;
                break;
            case Target.LayoutBoundsRow:
                ShowLayoutBounds = !_showLayoutBounds;
                break;
        }
    }

    /// <summary>
    /// Records the current frame and returns the number of frames rendered in the last second, including it.
    /// </summary>
    private int RecordFrame()
    {
        long now = Stopwatch.GetTimestamp();
        _frameTimestamps.Enqueue(now);
        while (now - _frameTimestamps.Peek() > Stopwatch.Frequency)
        {
            _frameTimestamps.Dequeue();
        }

        return _frameTimestamps.Count;
    }

    /// <summary>
    /// Outlines a box and its descendants, applying the same clips as rendering. A clipping box's content can exceed
    /// its bounds, so its content box and scrolled-out descendants are drawn inside the clip.
    /// </summary>
    private static void DrawLayoutBounds(DrawingContext context, LayoutBox box)
    {
        DrawOuterOutlines(context, box);

        bool clipsChildren = box.Style.ClipsOverflow || box.Element is View { ClipToBounds: true };
        if (clipsChildren)
        {
            context.Save();
            context.ClipRect(box.BorderBox);
        }

        try
        {
            if (box.Style.ContentInsets != Thickness.Zero)
            {
                DrawOutline(context, box.ContentBox, s_contentColor);
            }

            foreach (LayoutBox child in box.Children)
            {
                DrawLayoutBounds(context, child);
            }
        }
        finally
        {
            if (clipsChildren)
            {
                context.Restore();
            }
        }
    }

    private static void DrawOuterOutlines(DrawingContext context, LayoutBox box)
    {
        Thickness margin = box.Style.Margin;
        Rect border = box.BorderBox;

        if (margin != Thickness.Zero)
        {
            DrawOutline(
                context,
                new Rect(border.X - margin.Left, border.Y - margin.Top, border.Width + margin.Horizontal, border.Height + margin.Vertical),
                s_marginColor);
        }

        DrawOutline(context, border, s_borderColor);
    }

    private static void DrawOutline(DrawingContext context, Rect rect, Color color)
    {
        Paint paint = new(color);
        context.DrawRectangle(new Rect(rect.X, rect.Y, rect.Width, OutlineThickness), paint);
        context.DrawRectangle(new Rect(rect.X, rect.Y + rect.Height - OutlineThickness, rect.Width, OutlineThickness), paint);
        context.DrawRectangle(new Rect(rect.X, rect.Y, OutlineThickness, rect.Height), paint);
        context.DrawRectangle(new Rect(rect.X + rect.Width - OutlineThickness, rect.Y, OutlineThickness, rect.Height), paint);
    }

    /// <summary>
    /// Draws the button, which stays visible when the frame rate is hidden so the panel can always be reached.
    /// </summary>
    private void DrawButton(DrawingContext context, RenderPass pass, int framesPerSecond)
    {
        const string Title = "DevTools";
        string fps = string.Create(CultureInfo.InvariantCulture, $"{framesPerSecond} FPS");

        double width = (2 * ButtonPadding) + MeasureWidth(pass, Title, s_labelStyle);
        if (_showFps)
        {
            width += ButtonGap + MeasureWidth(pass, fps, s_labelStyle);
        }

        _buttonBounds = new Rect(pass.Viewport.Width - width - EdgeMargin, EdgeMargin, width, ButtonHeight);
        double opacity = _hovered == Target.Button ? PanelOpacity : ButtonOpacity;
        context.DrawRoundedRectangle(_buttonBounds, ButtonRadius, new Paint(Colors.Black, opacity));

        double x = _buttonBounds.X + ButtonPadding;
        x += DrawCenteredText(context, pass, Title, x, _buttonBounds, s_labelStyle, s_titleColor);
        if (_showFps)
        {
            DrawCenteredText(context, pass, fps, x + ButtonGap, _buttonBounds, s_labelStyle, Colors.White);
        }
    }

    private void DrawPanel(DrawingContext context, RenderPass pass)
    {
        double height = (2 * PanelPadding) + (2 * RowHeight) + FooterHeight + (_showLayoutBounds ? LegendHeight : 0);
        _panelBounds = new Rect(pass.Viewport.Width - PanelWidth - EdgeMargin, _buttonBounds.Y + ButtonHeight + PanelGap, PanelWidth, height);
        context.DrawRoundedRectangle(_panelBounds, PanelRadius, new Paint(Colors.Black, PanelOpacity));

        double rowX = _panelBounds.X + PanelPadding;
        double rowWidth = PanelWidth - (2 * PanelPadding);
        _fpsRowBounds = new Rect(rowX, _panelBounds.Y + PanelPadding, rowWidth, RowHeight);
        _layoutBoundsRowBounds = new Rect(rowX, _fpsRowBounds.Y + RowHeight, rowWidth, RowHeight);

        DrawRow(context, pass, _fpsRowBounds, "FPS readout", null, _showFps, _hovered == Target.FpsRow);
        string shortcut = OperatingSystem.IsMacOS() ? "Cmd+Shift+L" : "Ctrl+Shift+L";
        DrawRow(context, pass, _layoutBoundsRowBounds, "Layout bounds", shortcut, _showLayoutBounds, _hovered == Target.LayoutBoundsRow);

        double y = _layoutBoundsRowBounds.Y + RowHeight;
        if (_showLayoutBounds)
        {
            DrawLegend(context, pass, new Rect(rowX + RowPadding, y, rowWidth - (2 * RowPadding), LegendHeight));
            y += LegendHeight;
        }

        DrawFooter(context, pass, new Rect(rowX, y, rowWidth, FooterHeight));
    }

    private static void DrawRow(DrawingContext context, RenderPass pass, Rect row, string label, string? shortcut, bool isOn, bool isHovered)
    {
        if (isHovered)
        {
            context.DrawRoundedRectangle(row, RowRadius, new Paint(Colors.White, RowHoverOpacity));
        }

        double x = row.X + RowPadding;
        x += DrawCenteredText(context, pass, label, x, row, s_labelStyle, Colors.White);
        if (shortcut is not null)
        {
            DrawCenteredText(context, pass, shortcut, x + ButtonGap, row, s_captionStyle, s_secondaryTextColor);
        }

        Rect track = new(
            row.X + row.Width - RowPadding - SwitchWidth,
            row.Y + ((row.Height - SwitchHeight) / 2),
            SwitchWidth,
            SwitchHeight);
        context.DrawRoundedRectangle(track, SwitchHeight / 2, new Paint(isOn ? s_switchOnColor : s_switchOffColor));

        double knobSize = SwitchHeight - (2 * SwitchInset);
        double knobX = isOn ? track.X + track.Width - SwitchInset - knobSize : track.X + SwitchInset;
        context.DrawRoundedRectangle(new Rect(knobX, track.Y + SwitchInset, knobSize, knobSize), knobSize / 2, new Paint(Colors.White));
    }

    private static void DrawLegend(DrawingContext context, RenderPass pass, Rect area)
    {
        double x = area.X;
        foreach ((string label, Color color) in new[] { ("Margin", s_marginColor), ("Border", s_borderColor), ("Content", s_contentColor) })
        {
            context.DrawRectangle(new Rect(x, area.Y + ((area.Height - LegendSwatch) / 2), LegendSwatch, LegendSwatch), new Paint(color));
            x += LegendSwatch + (LegendGap / 3);
            x += DrawCenteredText(context, pass, label, x, area, s_captionStyle, s_secondaryTextColor) + LegendGap;
        }
    }

    private void DrawFooter(DrawingContext context, RenderPass pass, Rect area)
    {
        context.DrawRectangle(new Rect(area.X, area.Y, area.Width, OutlineThickness), new Paint(Colors.White, DividerOpacity));

        string layout = string.Create(CultureInfo.InvariantCulture, $"Layout {pass.LayoutDuration.TotalMilliseconds:0.0} ms");
        string frame = string.Create(CultureInfo.InvariantCulture, $"Frame {_frameCount}");
        DrawCenteredText(context, pass, layout, area.X + RowPadding, area, s_captionStyle, s_secondaryTextColor);
        double frameX = area.X + area.Width - RowPadding - MeasureWidth(pass, frame, s_captionStyle);
        DrawCenteredText(context, pass, frame, frameX, area, s_captionStyle, s_secondaryTextColor);
    }

    private static double MeasureWidth(RenderPass pass, string text, ComputedStyle style)
    {
        return pass.TextMeasurer.MeasureText(text, s_unconstrained, style).Width;
    }

    /// <summary>
    /// Draws text at <paramref name="x"/>, vertically centered in <paramref name="area"/>, and returns its width.
    /// </summary>
    private static double DrawCenteredText(DrawingContext context, RenderPass pass, string text, double x, Rect area, ComputedStyle style, Color color)
    {
        ITextLayout layout = pass.TextMeasurer.CreateTextLayout(text, s_unconstrained, style);
        try
        {
            context.DrawTextLayout(layout, new Point(x, area.Y + ((area.Height - layout.Size.Height) / 2)), new Paint(color));
            return layout.Size.Width;
        }
        finally
        {
            (layout as IDisposable)?.Dispose();
        }
    }
}
