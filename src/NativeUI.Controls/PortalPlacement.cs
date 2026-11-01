using NativeUI.Layout;

namespace NativeUI.Controls;

/// <summary>
/// Calculates where a <see cref="Portal"/> opens, so every platform applies the same placement rules.
/// </summary>
internal static class PortalPlacement
{
    /// <summary>
    /// Returns the top-left position of a portal of the given <paramref name="size"/>. All rectangles use one coordinate space.
    /// </summary>
    /// <param name="portal">The portal, which supplies the side, alignment, and offsets.</param>
    /// <param name="anchor">The anchor's bounds, or <see langword="null"/> to center in <paramref name="centerArea"/>.</param>
    /// <param name="size">The portal's size.</param>
    /// <param name="centerArea">The area to center in when there is no anchor.</param>
    /// <param name="limits">The area to stay inside. A portal larger than this area starts at its top-left corner.</param>
    public static Point Compute(Portal portal, Rect? anchor, Size size, Rect centerArea, Rect limits)
    {
        Point position = anchor is Rect anchorBounds
            ? ComputeAnchored(portal, anchorBounds, size)
            : new Point(
                centerArea.X + ((centerArea.Width - size.Width) / 2.0),
                centerArea.Y + ((centerArea.Height - size.Height) / 2.0));

        return new Point(
            Math.Max(limits.X, Math.Min(position.X, limits.Right - size.Width)),
            Math.Max(limits.Y, Math.Min(position.Y, limits.Bottom - size.Height)));
    }

    private static Point ComputeAnchored(Portal portal, Rect anchor, Size size)
    {
        return portal.Side switch
        {
            PortalSide.Top => new Point(
                AlignAlongEdge(portal, anchor.X, anchor.Width, size.Width),
                anchor.Y - size.Height - portal.SideOffset),
            PortalSide.Right => new Point(
                anchor.Right + portal.SideOffset,
                AlignAlongEdge(portal, anchor.Y, anchor.Height, size.Height)),
            PortalSide.Left => new Point(
                anchor.X - size.Width - portal.SideOffset,
                AlignAlongEdge(portal, anchor.Y, anchor.Height, size.Height)),
            _ => new Point(
                AlignAlongEdge(portal, anchor.X, anchor.Width, size.Width),
                anchor.Bottom + portal.SideOffset)
        };
    }

    private static double AlignAlongEdge(Portal portal, double anchorStart, double anchorLength, double length)
    {
        return portal.Align switch
        {
            Alignment.Center => anchorStart + ((anchorLength - length) / 2.0) + portal.AlignOffset,
            Alignment.End => anchorStart + anchorLength - length - portal.AlignOffset,
            _ => anchorStart + portal.AlignOffset
        };
    }
}
