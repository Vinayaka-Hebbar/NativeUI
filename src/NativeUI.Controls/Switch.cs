using NativeUI.Core;
using NativeUI.Media;

namespace NativeUI.Controls;

/// <summary>
/// Represents a switch that the user turns on and off by clicking it. The template has a <c>track</c> part that contains a
/// <c>thumb</c> part, and neither part has an appearance until a stylesheet gives it one. The switch is in the
/// <see cref="ElementStates.Checked"/> state, selected by <c>:checked</c>, or the <see cref="ElementStates.Unchecked"/>
/// state, selected by <c>:unchecked</c>.
/// </summary>
public class Switch : Control
{
    private const string TrackPartName = "track";
    private const string ThumbPartName = "thumb";

    static Switch()
    {
        WidthProperty.OverrideMetadata<Switch>(WidthValue.FitContent);
        CursorProperty.OverrideMetadata<Switch>(Cursor.Pointer);
        FocusableProperty.OverrideMetadata<Switch>(true);
    }

    /// <summary>
    /// Identifies the <see cref="IsChecked"/> bindable property.
    /// </summary>
    public static readonly BindableProperty<bool> IsCheckedProperty =
        BindableProperty.Create<Switch, bool>(
            nameof(IsChecked),
            defaultValue: false,
            propertyChanged: static (owner, _, _) => owner.UpdateCheckedState());

    /// <summary>
    /// Initializes a new instance of the <see cref="Switch"/> class.
    /// </summary>
    public Switch()
    {
        ClickGesture.Attach(this, () => IsChecked = !IsChecked);
        UpdateCheckedState();
    }

    /// <inheritdoc />
    public override string TagName => "switch";

    /// <summary>
    /// Gets or sets a value indicating whether the switch is on.
    /// </summary>
    public bool IsChecked
    {
        get => GetValue(IsCheckedProperty);
        set => SetValue(IsCheckedProperty, value);
    }

    /// <inheritdoc />
    protected override void BuildTemplate()
    {
        View track = DefinePart(new View(), TrackPartName);
        track.Children.Add(DefinePart(new View(), ThumbPartName));
        Children.Add(track);
    }

    private void UpdateCheckedState()
    {
        SetState(ElementStates.Checked, IsChecked);
        SetState(ElementStates.Unchecked, !IsChecked);
        InvalidateStyle();
        InvalidateVisual();
    }
}
