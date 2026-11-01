using NativeUI.Media;

namespace NativeUI.Layout;

/// <summary>
/// Represents the computed CSS box-model geometry for a single laid-out element and its children.
/// </summary>
public sealed class LayoutBox
{
    /// <summary>
    /// Gets the target UI element associated with this layout box.
    /// </summary>
    public object Element { get; }

    /// <summary>
    /// Gets the resolved <see cref="ComputedStyle"/> used during layout.
    /// </summary>
    public ComputedStyle Style { get; }

    /// <summary>
    /// Gets the outer border/background box in viewport logical coordinates.
    /// </summary>
    public Rect BorderBox { get; }

    /// <summary>
    /// Gets the inner content box (inside padding) in viewport logical coordinates.
    /// </summary>
    public Rect ContentBox { get; }

    /// <summary>
    /// Gets the laid-out child boxes.
    /// </summary>
    public IReadOnlyList<LayoutBox> Children { get; }

    /// <summary>
    /// Gets the shaped <see cref="ITextLayout"/> if this box represents a leaf text element, or <see langword="null"/> otherwise.
    /// </summary>
    public ITextLayout? TextLayout { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="LayoutBox"/> class.
    /// </summary>
    public LayoutBox(
        object element,
        ComputedStyle style,
        Rect borderBox,
        Rect contentBox,
        IReadOnlyList<LayoutBox> children,
        ITextLayout? textLayout = null)
    {
        ArgumentNullException.ThrowIfNull(element);
        ArgumentNullException.ThrowIfNull(style);
        ArgumentNullException.ThrowIfNull(children);

        Element = element;
        Style = style;
        BorderBox = borderBox;
        ContentBox = contentBox;
        Children = children;
        TextLayout = textLayout;
    }

    /// <summary>
    /// Returns a copy of this box and its descendants moved by the given distance, sharing their text layouts.
    /// </summary>
    public LayoutBox Translated(double deltaX, double deltaY)
    {
        return new LayoutBox(
            Element,
            Style,
            BorderBox with { X = BorderBox.X + deltaX, Y = BorderBox.Y + deltaY },
            ContentBox with { X = ContentBox.X + deltaX, Y = ContentBox.Y + deltaY },
            Children.Select(child => child.Translated(deltaX, deltaY)).ToList(),
            TextLayout);
    }
}
