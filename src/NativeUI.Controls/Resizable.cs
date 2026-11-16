using NativeUI.Core;
using NativeUI.Layout;

namespace NativeUI.Controls;

/// <summary>
/// Arranges child <see cref="ResizablePanel"/> and <see cref="ResizableHandle"/> elements along an orientation,
/// distributing space based on percentage or proportional shares while supporting interactive handle dragging.
/// </summary>
public class Resizable : View, IResizableLayoutNode
{
    /// <summary>
    /// Identifies the <see cref="Orientation"/> bindable property.
    /// </summary>
    public static readonly BindableProperty<Orientation> OrientationProperty =
        BindableProperty.Create<Resizable, Orientation>(
            nameof(Orientation),
            defaultValue: Orientation.Horizontal,
            propertyChanged: static (owner, _, _) => owner.OnOrientationChanged());

    /// <summary>
    /// Initializes a new instance of the <see cref="Resizable"/> class.
    /// </summary>
    public Resizable()
    {
        SizeChanged += static (_, _) => { };
    }

    /// <inheritdoc />
    public override string TagName => "resizable";

    /// <summary>
    /// Gets or sets the direction in which panels and handles are arranged. Defaults to <see cref="Orientation.Horizontal"/>.
    /// </summary>
    public Orientation Orientation
    {
        get => GetValue(OrientationProperty);
        set => SetValue(OrientationProperty, value);
    }

    /// <summary>
    /// Returns the total available length along the primary orientation axis.
    /// </summary>
    public double GetTotalMainLength()
    {
        double length = Orientation == Orientation.Horizontal ? Bounds.Width : Bounds.Height;
        if (length <= 0)
        {
            length = Orientation == Orientation.Horizontal ? ActualWidth : ActualHeight;
        }

        return length > 0 ? length : 100.0;
    }

    /// <summary>
    /// Finds the visible <see cref="ResizablePanel"/> elements immediately preceding and succeeding <paramref name="handle"/>.
    /// </summary>
    /// <param name="handle">The handle separating the panels.</param>
    /// <param name="previous">The preceding panel, or <see langword="null"/> when none is found.</param>
    /// <param name="next">The succeeding panel, or <see langword="null"/> when none is found.</param>
    internal void GetAdjacentPanels(ResizableHandle handle, out ResizablePanel? previous, out ResizablePanel? next)
    {
        previous = null;
        next = null;

        int handleIndex = -1;
        for (int i = 0; i < Children.Count; i++)
        {
            if (ReferenceEquals(Children[i], handle))
            {
                handleIndex = i;
                break;
            }
        }

        if (handleIndex < 0)
        {
            return;
        }

        for (int i = handleIndex - 1; i >= 0; i--)
        {
            if (Children[i] is ResizablePanel panel && panel.IsVisible)
            {
                previous = panel;
                break;
            }
        }

        for (int i = handleIndex + 1; i < Children.Count; i++)
        {
            if (Children[i] is ResizablePanel panel && panel.IsVisible)
            {
                next = panel;
                break;
            }
        }
    }

    /// <inheritdoc />
    public override string? GetAttribute(string name)
    {
        if (string.Equals(name, "data-orientation", StringComparison.Ordinal))
        {
            return Orientation == Orientation.Vertical ? "vertical" : "horizontal";
        }

        return base.GetAttribute(name);
    }

    private void OnOrientationChanged()
    {
        for (int i = 0; i < Children.Count; i++)
        {
            if (Children[i] is ResizableHandle handle)
            {
                handle.UpdateOrientation();
            }
        }

        InvalidateVisual();
    }
}
