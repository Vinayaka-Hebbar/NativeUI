using NativeUI.Core;
using NativeUI.Graphics.Skia;
using NativeUI.Layout;
using NativeUI.Media;
using NativeUI.Styling;
using NativeUI.Tests;

namespace NativeUI.Controls.Tests;

/// <summary>
/// Verifies CSS positioning behaviors including <see cref="Position.Relative"/>, <see cref="Position.Absolute"/>,
/// <see cref="Position.Fixed"/>, offset calculation, sizing to content, and <see cref="Portal"/> default position.
/// </summary>
internal static class PositioningTests
{
    public static void VerifyAnchorPositioning()
    {
        using View root = new() { Width = 400, Height = 300 };
        View trigger = new() { Style = new Style { Width = 100, Height = 40, AnchorName = "--trigger", Margin = new ThicknessValue(20, 0, 0, 30) } };
        View popup = new()
        {
            Style = new Style { Position = Position.Fixed, Width = 80, Height = 30, PositionAnchor = "--trigger", PositionArea = new PositionArea(PositionAreaTrack.End, PositionAreaTrack.SpanEnd) }
        };
        root.Children.Add(trigger);
        root.Children.Add(popup);
        LayoutEngine engine = new(SkiaTextShaper.Shared);
        LayoutBox box = engine.ComputeLayout(root, new Size(400, 300));
        Assert.Equal(new Rect(30, 60, 80, 30), box.Children[1].BorderBox);
        trigger.Height = 70;
        box = engine.ComputeLayout(root, new Size(400, 300));
        Assert.Equal(90.0, box.Children[1].BorderBox.Y);
    }

    public static void VerifyAnchorInsetsAndFallbacks()
    {
        using View root = new() { Width = 300, Height = 100 };
        View trigger = new() { Style = new Style { Width = 100, Height = 60, AnchorName = "--trigger" } };
        View popup = new()
        {
            Style = new Style
            {
                Position = Position.Fixed, Width = 80, Height = 50, PositionAnchor = "--trigger", PositionArea = new PositionArea(PositionAreaTrack.End, PositionAreaTrack.SpanEnd),
                PositionTryFallbacks = [PositionTryTactic.FlipBlock]
            }
        };
        root.Children.Add(trigger);
        root.Children.Add(popup);
        LayoutEngine engine = new(SkiaTextShaper.Shared);
        LayoutBox box = engine.ComputeLayout(root, new Size(300, 100));
        // Neither side fits, so the authored position remains in use.
        Assert.Equal(60.0, box.Children[1].BorderBox.Y);
        trigger.Style = new Style { Width = 100, Height = 20, AnchorName = "--trigger", Margin = new ThicknessValue(60, 0, 0, 20) };
        trigger.Height = 20;
        box = engine.ComputeLayout(root, new Size(300, 100));
        Assert.Equal(10.0, box.Children[1].BorderBox.Y);
        popup.Style = new Style { Position = Position.Fixed, Width = 80, Height = 50, Top = InsetValue.Anchor(AnchorSide.Bottom, "--trigger"), Right = InsetValue.Anchor(AnchorSide.Right, "--trigger") };
        box = engine.ComputeLayout(root, new Size(300, 100));
        Assert.Equal(new Rect(40, 80, 80, 50), box.Children[1].BorderBox);
    }

    public static void VerifyLaterInFlowAnchor()
    {
        using View root = new() { Width = 300, Height = 200 };
        View popup = new() { Style = new Style { Position = Position.Absolute, Width = 50, Height = 20, Top = InsetValue.Anchor(AnchorSide.Bottom, "--later") } };
        View trigger = new() { Style = new Style { Width = 100, Height = 40, AnchorName = "--later" } };
        root.Children.Add(popup);
        root.Children.Add(trigger);
        LayoutBox box = new LayoutEngine(SkiaTextShaper.Shared).ComputeLayout(root, new Size(300, 200));
        LayoutBox popupBox = box.Children.Single(child => ReferenceEquals(child.Element, popup));
        Assert.Equal(40.0, popupBox.BorderBox.Y);
    }

    /// <summary>Verifies that area constraints do not leak into descendant measurement or replace computed styles.</summary>
    public static void VerifyAnchorAreaSizing()
    {
        using View root = new() { Width = 300, Height = 300 };
        View trigger = new()
        {
            Style = new Style
            {
                Width = 60, Height = 60, AnchorName = "--trigger",
                Margin = new ThicknessValue(0, 0, 0, 20)
            }
        };
        View popup = new()
        {
            Style = new Style
            {
                Position = Position.Fixed, Width = WidthValue.Percent(50), Height = HeightValue.Percent(50),
                PositionAnchor = "--trigger", PositionArea = new PositionArea(PositionAreaTrack.End, PositionAreaTrack.SpanEnd)
            }
        };
        View content = new() { Width = WidthValue.Percent(50), Height = HeightValue.Percent(50) };
        popup.Children.Add(content);
        root.Children.Add(trigger);
        root.Children.Add(popup);
        ComputedStyle style = popup.ComputedStyle;

        LayoutBox result = new LayoutEngine(SkiaTextShaper.Shared).ComputeLayout(root, new Size(300, 300));
        LayoutBox popupBox = result.Children.Single(child => ReferenceEquals(child.Element, popup));

        Assert.Equal(new Rect(20, 60, 140, 120), popupBox.BorderBox);
        Assert.Equal(new Size(70, 60), popupBox.Children[0].BorderBox.Size);
        Assert.Same(style, popup.ComputedStyle);
        Assert.Same(style, popupBox.Style);
    }

    /// <summary>Verifies that diagonal fallback swaps size limits without modifying the computed style.</summary>
    public static void VerifyDiagonalAnchorFallback()
    {
        using View root = new() { Width = 300, Height = 200 };
        View trigger = new()
        {
            Style = new Style { Width = 80, Height = 20, AnchorName = "--trigger", Margin = new ThicknessValue(100, 0, 0, 0) }
        };
        View popup = new()
        {
            Style = new Style
            {
                Position = Position.Fixed, Width = 40, Height = 100, MinWidth = 60, MaxHeight = 120,
                PositionAnchor = "--trigger", PositionArea = new PositionArea(PositionAreaTrack.End, PositionAreaTrack.SpanEnd),
                PositionTryFallbacks = [PositionTryTactic.FlipStart]
            }
        };
        root.Children.Add(trigger);
        root.Children.Add(popup);
        ComputedStyle style = popup.ComputedStyle;

        LayoutBox result = new LayoutEngine(SkiaTextShaper.Shared).ComputeLayout(root, new Size(300, 200));
        LayoutBox popupBox = result.Children.Single(child => ReferenceEquals(child.Element, popup));

        Assert.Equal(new Rect(80, 100, 100, 60), popupBox.BorderBox);
        Assert.Same(style, popup.ComputedStyle);
        Assert.Equal(WidthValue.Pixels(40), style.Width);
        Assert.Equal(HeightValue.Pixels(100), style.Height);
        Assert.Equal(WidthValue.Pixels(60), style.MinWidth);
    }

    /// <summary>Verifies fallback retention across anchor movement and reset after target sizing changes.</summary>
    public static void VerifyAnchorFallbackRetention()
    {
        using View root = new() { Width = 300, Height = 200 };
        View trigger = new()
        {
            Style = new Style { Width = 80, Height = 20, AnchorName = "--trigger", Margin = new ThicknessValue(140, 0, 0, 0) }
        };
        Style popupStyle = new()
        {
            Position = Position.Fixed, Width = 80, Height = 60, PositionAnchor = "--trigger",
            PositionArea = new PositionArea(PositionAreaTrack.End, PositionAreaTrack.SpanEnd),
            PositionTryFallbacks = [PositionTryTactic.FlipBlock]
        };
        View popup = new() { Style = popupStyle };
        root.Children.Add(trigger);
        root.Children.Add(popup);
        LayoutEngine engine = new(SkiaTextShaper.Shared);

        LayoutBox initial = engine.ComputeLayout(root, new Size(300, 200));
        Assert.Equal(80.0, initial.Children[1].BorderBox.Y);
        trigger.Style.Margin = new ThicknessValue(80, 0, 0, 0);
        LayoutBox moved = engine.ComputeLayout(root, new Size(300, 200));
        Assert.Equal(20.0, moved.Children[1].BorderBox.Y);
        popupStyle.Height = 50;
        LayoutBox resized = engine.ComputeLayout(root, new Size(300, 200));
        Assert.Equal(100.0, resized.Children[1].BorderBox.Y);
    }

    /// <summary>Verifies that moving an anchor does not remeasure an unrelated intrinsic-size sibling.</summary>
    public static void VerifyAnchorMeasurementReuse()
    {
        using View root = new() { Width = 300, Height = 300 };
        View trigger = new() { Style = new Style { Width = 60, Height = 40, AnchorName = "--trigger" } };
        View popup = new()
        {
            Style = new Style
            {
                Position = Position.Fixed, Width = 80, Height = 30, PositionAnchor = "--trigger",
                PositionArea = new PositionArea(PositionAreaTrack.End, PositionAreaTrack.SpanEnd)
            }
        };
        IntrinsicView sibling = new();
        root.Children.Add(trigger);
        root.Children.Add(sibling);
        root.Children.Add(popup);
        LayoutEngine engine = new(SkiaTextShaper.Shared);
        engine.ComputeLayout(root, new Size(300, 300));
        int count = sibling.MeasureCount;
        ComputedStyle style = popup.ComputedStyle;

        engine.ComputeLayout(root, new Size(300, 300));
        trigger.Height = 70;
        LayoutBox moved = engine.ComputeLayout(root, new Size(300, 300));

        Assert.Equal(count, sibling.MeasureCount);
        Assert.Equal(70.0, moved.Children.Single(child => ReferenceEquals(child.Element, popup)).BorderBox.Y);
        Assert.Same(style, popup.ComputedStyle);
    }

    private sealed class IntrinsicView : View, IIntrinsicSize
    {
        public int MeasureCount { get; private set; }

        public Size GetIntrinsicSize(Size? containingSize = null)
        {
            MeasureCount++;
            return new Size(20, 20);
        }
    }

    public static void VerifyRelativePositioningDoesNotShiftSiblings()
    {
        View root = new() { Width = 300 };
        View first = new() { Width = 100, Height = 40 };
        View second = new()
        {
            Position = Position.Relative,
            Top = 15,
            Left = 25,
            Width = 100,
            Height = 50
        };
        View third = new() { Width = 100, Height = 60 };

        root.Children.Add(first);
        root.Children.Add(second);
        root.Children.Add(third);

        LayoutBox layout = new LayoutEngine(SkiaTextShaper.Shared).ComputeLayout(root, new Size(300, 500));
        LayoutBox firstBox = layout.Children[0];
        LayoutBox secondBox = layout.Children[1];
        LayoutBox thirdBox = layout.Children[2];

        // First child is at (0, 0) with height 40
        Assert.Equal(0.0, firstBox.BorderBox.X);
        Assert.Equal(0.0, firstBox.BorderBox.Y);

        // Second child is relatively shifted by (25, 15) from (0, 40) -> (25, 55)
        Assert.Equal(25.0, secondBox.BorderBox.X);
        Assert.Equal(55.0, secondBox.BorderBox.Y);

        // Third child is placed immediately after second's unshifted flow position (0, 40 + 50 = 90)
        Assert.Equal(0.0, thirdBox.BorderBox.X);
        Assert.Equal(90.0, thirdBox.BorderBox.Y);
    }

    public static void VerifyAbsolutePositioningOffsets()
    {
        View card = new()
        {
            Position = Position.Relative,
            Width = 400,
            Height = 200
        };

        View badge = new()
        {
            Position = Position.Absolute,
            Top = 8,
            Right = 12,
            Width = 60,
            Height = 24
        };
        card.Children.Add(badge);

        LayoutBox layout = new LayoutEngine(SkiaTextShaper.Shared).ComputeLayout(card, new Size(600, 600));
        Assert.Equal(1, layout.Children.Count);

        LayoutBox badgeBox = layout.Children[0];
        // Right: 12 on a 400px card -> X = 400 - 12 - 60 = 328
        Assert.Equal(328.0, badgeBox.BorderBox.X);
        Assert.Equal(8.0, badgeBox.BorderBox.Y);
        Assert.Equal(60.0, badgeBox.BorderBox.Width);
        Assert.Equal(24.0, badgeBox.BorderBox.Height);
    }

    public static void VerifyAbsolutePositioningPinsWidthWithLeftAndRight()
    {
        View card = new()
        {
            Position = Position.Relative,
            Width = 400,
            Height = 200
        };

        View banner = new()
        {
            Position = Position.Absolute,
            Left = 20,
            Right = 30,
            Top = 10,
            Height = 40
        };
        card.Children.Add(banner);

        LayoutBox layout = new LayoutEngine(SkiaTextShaper.Shared).ComputeLayout(card, new Size(600, 600));
        LayoutBox bannerBox = layout.Children[0];

        // Left 20, Right 30 -> Width = 400 - 20 - 30 = 350, X = 20
        Assert.Equal(20.0, bannerBox.BorderBox.X);
        Assert.Equal(10.0, bannerBox.BorderBox.Y);
        Assert.Equal(350.0, bannerBox.BorderBox.Width);
        Assert.Equal(40.0, bannerBox.BorderBox.Height);
    }

    public static void VerifyAbsolutePositioningSizesToContentWhenWidthUnset()
    {
        View card = new()
        {
            Position = Position.Relative,
            Width = 500,
            Height = 300
        };

        Text badgeText = new()
        {
            Position = Position.Absolute,
            Top = 10,
            Left = 10,
            Value = "Featured"
        };
        card.Children.Add(badgeText);

        LayoutBox layout = new LayoutEngine(SkiaTextShaper.Shared).ComputeLayout(card, new Size(600, 600));
        LayoutBox badgeBox = layout.Children[0];

        // An absolute element with unset width must shrink to fit content rather than filling the 500px card
        Assert.True(badgeBox.BorderBox.Width < 200, "Unset width on absolute element must shrink to fit content.");
        Assert.Equal(10.0, badgeBox.BorderBox.X);
        Assert.Equal(10.0, badgeBox.BorderBox.Y);
    }

    public static void VerifyPositionedElementsOrderedAfterInFlowSiblings()
    {
        View container = new()
        {
            Position = Position.Relative,
            Width = 300,
            Height = 200
        };

        View inFlowFirst = new() { Width = 100, Height = 40 };
        View absoluteOverlay = new() { Position = Position.Absolute, Top = 0, Left = 0, Width = 50, Height = 50 };
        View inFlowSecond = new() { Width = 100, Height = 40 };

        container.Children.Add(inFlowFirst);
        container.Children.Add(absoluteOverlay);
        container.Children.Add(inFlowSecond);

        LayoutBox layout = new LayoutEngine(SkiaTextShaper.Shared).ComputeLayout(container, new Size(300, 300));

        // In-flow children must come first in child list, followed by positioned children
        Assert.Equal(3, layout.Children.Count);
        Assert.Same(inFlowFirst, layout.Children[0].Element);
        Assert.Same(inFlowSecond, layout.Children[1].Element);
        Assert.Same(absoluteOverlay, layout.Children[2].Element);
    }

    public static void VerifyPortalDefaultsToFixedPositionAndSkipsFlow()
    {
        Portal portal = new();
        Assert.Equal(Position.Fixed, portal.Position);

        View root = new() { Width = 400, Height = 300 };
        View content = new() { Width = 100, Height = 50 };
        root.Children.Add(content);
        root.Children.Add(portal);

        LayoutBox layout = new LayoutEngine(SkiaTextShaper.Shared).ComputeLayout(root, new Size(400, 300));

        // Fixed portal must not participate in in-flow layout box tree
        Assert.Equal(1, layout.Children.Count);
        Assert.Same(content, layout.Children[0].Element);
    }

    public static void VerifyAbsoluteChildExtendsScrollExtentAndScrolls()
    {
        ScrollView scroll = new()
        {
            Position = Position.Relative,
            Width = 200,
            Height = 150
        };

        View inFlow = new() { Width = 100, Height = 50 };
        View farChild = new()
        {
            Position = Position.Absolute,
            Top = 300,
            Left = 10,
            Width = 80,
            Height = 60
        };

        scroll.Children.Add(inFlow);
        scroll.Children.Add(farChild);

        LayoutEngine engine = new(SkiaTextShaper.Shared);
        LayoutBox layout = engine.ComputeLayout(scroll, new Size(200, 150));

        // Extent must encompass the absolute child at top: 300, height: 60 -> 360
        Assert.True(scroll.ExtentHeight >= 360, $"ExtentHeight should be at least 360, got {scroll.ExtentHeight}");

        // Scroll down to 100px
        scroll.ScrollTo(0, 100);
        LayoutBox scrolledLayout = engine.ComputeLayout(scroll, new Size(200, 150));

        // Far child must be translated by -100px: 300 - 100 = 200
        LayoutBox scrolledFarChild = scrolledLayout.Children[1];
        Assert.Equal(200.0, scrolledFarChild.BorderBox.Y);
    }

    public static void VerifyAbsolutePositioningAppliesMarginOnce()
    {
        View card = new()
        {
            Position = Position.Relative,
            Width = 300,
            Height = 200
        };

        View badge = new()
        {
            Style = new Style
            {
                Position = Position.Absolute,
                Left = 20,
                Top = 15,
                Margin = new Thickness(5, 0, 0, 10),
                Width = 80,
                Height = 40
            }
        };
        card.Children.Add(badge);

        Assert.Equal(new Thickness(5, 0, 0, 10), badge.ComputedStyle.Margin);
        LayoutBox layout = new LayoutEngine(SkiaTextShaper.Shared).ComputeLayout(card, new Size(300, 200));
        LayoutBox badgeBox = layout.Children[0];

        Assert.Equal(30.0, badgeBox.BorderBox.X);
        Assert.Equal(20.0, badgeBox.BorderBox.Y);
        Assert.Equal(80.0, badgeBox.BorderBox.Width);
        Assert.Equal(40.0, badgeBox.BorderBox.Height);
    }

    public static void VerifyAbsolutePositioningUsesNearestPositionedAncestor()
    {
        View outer = new()
        {
            Position = Position.Relative,
            Width = 400,
            Height = 300
        };

        StackPanel staticPanel = new()
        {
            Style = new Style
            {
                Position = Position.Static,
                Margin = new Thickness(50, 40, 0, 0),
                Width = 200,
                Height = 200
            }
        };

        Text absText = new()
        {
            Position = Position.Absolute,
            Left = 20,
            Top = 10,
            Value = "Pinned"
        };

        staticPanel.Children.Add(absText);
        outer.Children.Add(staticPanel);

        LayoutBox layout = new LayoutEngine(SkiaTextShaper.Shared).ComputeLayout(outer, new Size(400, 300));
        LayoutBox panelBox = layout.Children[0];
        LayoutBox textBox = panelBox.Children[0];

        Assert.Equal(20.0, textBox.BorderBox.X);
        Assert.Equal(10.0, textBox.BorderBox.Y);
    }

    public static void VerifyFixedPositioningAgainstViewportAndScrollImmunity()
    {
        ScrollView scroll = new()
        {
            Width = 200,
            Height = 200
        };

        View tallContent = new()
        {
            Width = 100,
            Height = 500
        };

        View fixedOverlay = new()
        {
            Position = Position.Fixed,
            Top = 15,
            Left = 25,
            Width = 50,
            Height = 30
        };

        scroll.Children.Add(tallContent);
        scroll.Children.Add(fixedOverlay);

        LayoutEngine engine = new(SkiaTextShaper.Shared);
        LayoutBox layout = engine.ComputeLayout(scroll, new Size(300, 400));

        Assert.Equal(2, layout.Children.Count);
        LayoutBox fixedBox = layout.Children[1];
        Assert.Equal(25.0, fixedBox.BorderBox.X);
        Assert.Equal(15.0, fixedBox.BorderBox.Y);

        scroll.ScrollTo(0, 100);
        LayoutBox scrolledLayout = engine.ComputeLayout(scroll, new Size(300, 400));
        LayoutBox scrolledFixedBox = scrolledLayout.Children[1];

        Assert.Equal(25.0, scrolledFixedBox.BorderBox.X);
        Assert.Equal(15.0, scrolledFixedBox.BorderBox.Y);
    }

    public static void VerifyTextMeasurementObeysFitContent()
    {
        View root = new() { Width = 500, Height = 200 };
        Text fitText = new()
        {
            Width = WidthValue.FitContent,
            Value = "Short"
        };
        root.Children.Add(fitText);

        LayoutBox layout = new LayoutEngine(SkiaTextShaper.Shared).ComputeLayout(root, new Size(500, 200));
        LayoutBox textBox = layout.Children[0];

        Assert.True(textBox.BorderBox.Width < 150, $"Expected fit-content width < 150, got {textBox.BorderBox.Width}");
    }
}
