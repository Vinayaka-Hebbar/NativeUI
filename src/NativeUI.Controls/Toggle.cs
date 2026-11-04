using NativeUI.Core;
using NativeUI.Media;

namespace NativeUI.Controls;

/// <summary>
/// Represents a button that stays pressed after a click until the next click releases it. The template has a
/// <c>content</c> part that hosts <see cref="ContentControl.Content"/>. The toggle is in the <see cref="ElementStates.On"/>
/// state, selected by <c>:pressed</c>, or the <see cref="ElementStates.Off"/> state, selected by <c>:unpressed</c>.
/// Inside a <see cref="ToggleGroup"/>, the group decides which other toggles a click releases.
/// </summary>
public class Toggle : ContentControl
{
    static Toggle()
    {
        WidthProperty.OverrideMetadata<Toggle>(WidthValue.FitContent);
        CursorProperty.OverrideMetadata<Toggle>(Cursor.Pointer);
    }

    /// <summary>
    /// Identifies the <see cref="IsPressed"/> bindable property.
    /// </summary>
    public static readonly BindableProperty<bool> IsPressedProperty =
        BindableProperty.Create<Toggle, bool>(
            nameof(IsPressed),
            defaultValue: false,
            propertyChanged: static (owner, _, _) => owner.OnIsPressedChanged());

    /// <summary>
    /// Identifies the <see cref="Value"/> bindable property.
    /// </summary>
    public static readonly BindableProperty<object?> ValueProperty =
        BindableProperty.Create<Toggle, object?>(
            nameof(Value),
            defaultValue: null,
            propertyChanged: static (owner, _, _) => owner.Group?.OnToggleChanged(owner));

    /// <summary>
    /// Initializes a new instance of the <see cref="Toggle"/> class.
    /// </summary>
    public Toggle()
    {
        ClickGesture.Attach(this, OnClicked);
        UpdatePressedState();
    }

    /// <inheritdoc />
    public override string TagName => "toggle";

    /// <summary>
    /// Gets or sets a value indicating whether the toggle is pressed.
    /// </summary>
    public bool IsPressed
    {
        get => GetValue(IsPressedProperty);
        set => SetValue(IsPressedProperty, value);
    }

    /// <summary>
    /// Gets or sets the value that identifies this toggle in <see cref="ToggleGroup.SelectedValue"/> and
    /// <see cref="ToggleGroup.SelectedValues"/>.
    /// </summary>
    public object? Value
    {
        get => GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    /// <summary>
    /// Gets the group that presents this toggle as one of its items, or <see langword="null"/> when the toggle is not in a group.
    /// </summary>
    private ToggleGroup? Group => Parent as ToggleGroup ?? Parent?.TemplatedParent as ToggleGroup;

    /// <inheritdoc />
    protected override void BuildTemplate()
    {
        Children.Add(DefinePart(new View(), ContentPartName));
    }

    private void OnClicked()
    {
        ToggleGroup? group = Group;
        if (group is not null && !group.CanToggle(this))
        {
            return;
        }

        IsPressed = !IsPressed;
    }

    private void OnIsPressedChanged()
    {
        UpdatePressedState();
        Group?.OnToggleChanged(this);
    }

    private void UpdatePressedState()
    {
        SetState(ElementStates.On, IsPressed);
        SetState(ElementStates.Off, !IsPressed);
        InvalidateStyle();
        InvalidateVisual();
    }
}
