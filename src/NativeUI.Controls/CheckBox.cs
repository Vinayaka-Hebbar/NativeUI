using NativeUI.Core;
using NativeUI.Layout;
using NativeUI.Media;
using Path = NativeUI.Controls.Shapes.Path;

namespace NativeUI.Controls;

/// <summary>
/// Represents a check box that the user checks and unchecks by clicking it, followed by its <see cref="ContentControl.Content"/>
/// in a row. The template has an <c>indicator</c> part that contains an <c>icon</c> part. The icon draws a check mark while
/// the check box is checked, a dash while it is indeterminate, and nothing while it is unchecked, in the CSS <c>color</c>
/// of the icon. Neither part has an appearance until a stylesheet gives it one. The check box is in exactly one of the
/// <see cref="ElementStates.Checked"/>, <see cref="ElementStates.Unchecked"/>, and <see cref="ElementStates.Indeterminate"/>
/// states, selected by <c>:checked</c>, <c>:unchecked</c>, and <c>:indeterminate</c>.
/// </summary>
public class CheckBox : ContentControl, IStackLayoutNode
{
    private const string IndicatorPartName = "indicator";
    private const string IconPartName = "icon";

    private static readonly PathGeometry s_checkMark = Geometry.Parse("M6.2 11.6 L2.6 8 L3.8 6.8 L6.2 9.2 L12.2 3.2 L13.4 4.4 Z");
    private static readonly PathGeometry s_dash = Geometry.Parse("M3 7 L13 7 L13 9 L3 9 Z");

    static CheckBox()
    {
        WidthProperty.OverrideMetadata<CheckBox>(WidthValue.FitContent);
        CursorProperty.OverrideMetadata<CheckBox>(Cursor.Pointer);
    }

    /// <summary>
    /// Identifies the <see cref="IsChecked"/> bindable property.
    /// </summary>
    public static readonly BindableProperty<bool?> IsCheckedProperty =
        BindableProperty.Create<CheckBox, bool?>(
            nameof(IsChecked),
            defaultValue: false,
            propertyChanged: static (owner, _, _) => owner.UpdateCheckedState());

    /// <summary>
    /// Identifies the <see cref="IsThreeState"/> bindable property.
    /// </summary>
    public static readonly BindableProperty<bool> IsThreeStateProperty =
        BindableProperty.Create<CheckBox, bool>(nameof(IsThreeState), defaultValue: false);

    private readonly Path? _icon;

    /// <summary>
    /// Initializes a new instance of the <see cref="CheckBox"/> class.
    /// </summary>
    public CheckBox()
    {
        _icon = GetTemplateChild<Path>(IconPartName);
        ClickGesture.Attach(this, Advance);
        UpdateCheckedState();
    }

    /// <inheritdoc />
    public override string TagName => "checkbox";

    /// <summary>
    /// Gets or sets a value indicating whether the check box is checked, or <see langword="null"/> when it is indeterminate.
    /// </summary>
    public bool? IsChecked
    {
        get => GetValue(IsCheckedProperty);
        set => SetValue(IsCheckedProperty, value);
    }

    /// <summary>
    /// Gets or sets a value indicating whether a click moves a checked check box to the indeterminate state instead of
    /// unchecking it. Code can set <see cref="IsChecked"/> to <see langword="null"/> whatever this value is.
    /// </summary>
    public bool IsThreeState
    {
        get => GetValue(IsThreeStateProperty);
        set => SetValue(IsThreeStateProperty, value);
    }

    Orientation IStackLayoutNode.Orientation => Orientation.Horizontal;

    double IStackLayoutNode.Spacing => 0;

    Alignment IStackLayoutNode.HorizontalAlignment => Alignment.Start;

    /// <inheritdoc />
    protected override void BuildTemplate()
    {
        View indicator = DefinePart(new View(), IndicatorPartName);
        indicator.Children.Add(DefinePart(new Path { Fill = SvgCurrentColorBrush.Instance }, IconPartName));
        Children.Add(indicator);
    }

    /// <summary>
    /// Moves an unchecked check box to checked. Moves a checked check box to indeterminate when <see cref="IsThreeState"/>
    /// is <see langword="true"/>, and to unchecked otherwise. Moves an indeterminate check box to unchecked.
    /// </summary>
    private void Advance()
    {
        IsChecked = IsChecked switch
        {
            false => true,
            true when IsThreeState => null,
            _ => false
        };
    }

    private void UpdateCheckedState()
    {
        bool? isChecked = IsChecked;
        SetState(ElementStates.Checked, isChecked == true);
        SetState(ElementStates.Unchecked, isChecked == false);
        SetState(ElementStates.Indeterminate, isChecked is null);
        if (_icon is not null)
        {
            _icon.Data = isChecked switch
            {
                true => s_checkMark,
                null => s_dash,
                false => null
            };
        }

        InvalidateStyle();
        InvalidateVisual();
    }
}
