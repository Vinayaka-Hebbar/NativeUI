using NativeUI.Media;

namespace NativeUI.Controls;

/// <summary>
/// Represents a view that scrolls its content in both directions by default. A stylesheet or inline style can
/// override its <c>overflow</c>. Give it a height, because a view without one grows to fit its content.
/// </summary>
public class ScrollView : View
{
    static ScrollView()
    {
        OverflowXProperty.OverrideMetadata<ScrollView>(Overflow.Auto);
        OverflowYProperty.OverrideMetadata<ScrollView>(Overflow.Auto);
    }

    /// <summary>
    /// Gets or sets the single child to scroll, replacing any existing children.
    /// </summary>
    public UIElement? Content
    {
        get => Children.Count > 0 ? Children[0] : null;
        set
        {
            Children.Clear();
            if (value is not null)
            {
                Children.Add(value);
            }
        }
    }
}
