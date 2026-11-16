using NativeUI.Media;

namespace NativeUI.Layout;

/// <summary>
/// Compares <see cref="ComputedStyle"/> values by the properties that determine the size and position of an element and
/// of its descendants. Properties that only change painting are ignored.
/// </summary>
/// <remarks>
/// A property added to <see cref="ComputedStyle"/> that affects layout must be added to both
/// <see cref="Equals(ComputedStyle?, ComputedStyle?)"/> and <see cref="GetHashCode(ComputedStyle)"/>.
/// </remarks>
internal sealed class LayoutEqualityComparer : IEqualityComparer<ComputedStyle>
{
    /// <summary>
    /// Gets the shared comparer instance.
    /// </summary>
    public static LayoutEqualityComparer Instance { get; } = new();

    /// <inheritdoc />
    public bool Equals(ComputedStyle? x, ComputedStyle? y)
    {
        if (ReferenceEquals(x, y))
        {
            return true;
        }

        if (x is null || y is null)
        {
            return false;
        }

        return x.FontSize == y.FontSize
            && x.FontWeight == y.FontWeight
            && x.FontFamily == y.FontFamily
            && x.TextAlign == y.TextAlign
            && x.LineHeight == y.LineHeight
            && x.AlignItems == y.AlignItems
            && x.Padding == y.Padding
            && x.Margin == y.Margin
            && x.Gap == y.Gap
            && x.BorderWidth == y.BorderWidth
            && x.Width == y.Width
            && x.Height == y.Height
            && x.MinWidth == y.MinWidth
            && x.MaxWidth == y.MaxWidth
            && x.MinHeight == y.MinHeight
            && x.MaxHeight == y.MaxHeight
            && x.OverflowX == y.OverflowX
            && x.OverflowY == y.OverflowY
            && x.ScrollbarWidth == y.ScrollbarWidth
            && x.Position == y.Position
            && x.Top == y.Top
            && x.Right == y.Right
            && x.Bottom == y.Bottom
            && x.Left == y.Left
            && x.ZIndex == y.ZIndex;
    }

    /// <inheritdoc />
    public int GetHashCode(ComputedStyle obj)
    {
        ArgumentNullException.ThrowIfNull(obj);

        HashCode hash = new();
        hash.Add(obj.FontSize);
        hash.Add(obj.FontWeight);
        hash.Add(obj.FontFamily);
        hash.Add(obj.TextAlign);
        hash.Add(obj.LineHeight);
        hash.Add(obj.AlignItems);
        hash.Add(obj.Padding);
        hash.Add(obj.Margin);
        hash.Add(obj.Gap);
        hash.Add(obj.BorderWidth);
        hash.Add(obj.Width);
        hash.Add(obj.Height);
        hash.Add(obj.MinWidth);
        hash.Add(obj.MaxWidth);
        hash.Add(obj.MinHeight);
        hash.Add(obj.MaxHeight);
        hash.Add(obj.OverflowX);
        hash.Add(obj.OverflowY);
        hash.Add(obj.ScrollbarWidth);
        hash.Add(obj.Position);
        hash.Add(obj.Top);
        hash.Add(obj.Right);
        hash.Add(obj.Bottom);
        hash.Add(obj.Left);
        hash.Add(obj.ZIndex);
        return hash.ToHashCode();
    }
}
