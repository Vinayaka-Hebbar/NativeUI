using System.Collections.Specialized;
using NativeUI.Core;
using NativeUI.Layout;
using NativeUI.Media;

namespace NativeUI.Controls;

/// <summary>
/// Represents a row of <see cref="Toggle"/> items whose pressed states follow <see cref="SelectionMode"/> and
/// <see cref="IsRequired"/>. The CSS <c>gap</c> property sets the spacing between the toggles. Descendant selectors such as
/// <c>togglegroup toggle:first-child</c> style the toggles. Items that are not toggles take no part in the selection.
/// While the group is disabled, clicks do not change its toggles.
/// </summary>
public class ToggleGroup : ItemsControl, IStackLayoutNode
{
    static ToggleGroup()
    {
        WidthProperty.OverrideMetadata<ToggleGroup>(WidthValue.FitContent);
    }

    /// <summary>
    /// Identifies the <see cref="SelectionMode"/> bindable property.
    /// </summary>
    public static readonly BindableProperty<ToggleSelectionMode> SelectionModeProperty =
        BindableProperty.Create<ToggleGroup, ToggleSelectionMode>(
            nameof(SelectionMode),
            defaultValue: ToggleSelectionMode.Single,
            propertyChanged: static (owner, _, _) => owner.CoerceSelection(null));

    /// <summary>
    /// Identifies the <see cref="IsRequired"/> bindable property.
    /// </summary>
    public static readonly BindableProperty<bool> IsRequiredProperty =
        BindableProperty.Create<ToggleGroup, bool>(nameof(IsRequired), defaultValue: false);

    /// <summary>
    /// Identifies the <see cref="SelectedValue"/> bindable property.
    /// </summary>
    public static readonly BindableProperty<object?> SelectedValueProperty =
        BindableProperty.Create<ToggleGroup, object?>(nameof(SelectedValue), defaultValue: null);

    /// <summary>
    /// Identifies the <see cref="SelectedValues"/> bindable property.
    /// </summary>
    public static readonly BindableProperty<IReadOnlyList<object?>> SelectedValuesProperty =
        BindableProperty.Create<ToggleGroup, IReadOnlyList<object?>>(nameof(SelectedValues), defaultValue: []);

    private bool _isCoercing;

    /// <inheritdoc />
    public override string TagName => "togglegroup";

    /// <summary>
    /// Gets or sets how many toggles can be pressed at the same time. Defaults to <see cref="ToggleSelectionMode.Single"/>.
    /// Changing the mode to <see cref="ToggleSelectionMode.Single"/> keeps only the first pressed toggle pressed.
    /// </summary>
    public ToggleSelectionMode SelectionMode
    {
        get => GetValue(SelectionModeProperty);
        set => SetValue(SelectionModeProperty, value);
    }

    /// <summary>
    /// Gets or sets a value indicating whether at least one toggle stays pressed once a selection exists. When it is
    /// <see langword="true"/>, a click does not release the last pressed toggle. Code can still release it.
    /// </summary>
    public bool IsRequired
    {
        get => GetValue(IsRequiredProperty);
        set => SetValue(IsRequiredProperty, value);
    }

    /// <summary>
    /// Gets the <see cref="Toggle.Value"/> of the first pressed toggle in item order, or <see langword="null"/> when no
    /// toggle is pressed.
    /// </summary>
    public object? SelectedValue
    {
        get => GetValue(SelectedValueProperty);
        private set => SetValue(SelectedValueProperty, value);
    }

    /// <summary>
    /// Gets the <see cref="Toggle.Value"/> of each pressed toggle in item order.
    /// </summary>
    public IReadOnlyList<object?> SelectedValues
    {
        get => GetValue(SelectedValuesProperty);
        private set => SetValue(SelectedValuesProperty, value);
    }

    Orientation IStackLayoutNode.Orientation => Orientation.Horizontal;

    double IStackLayoutNode.Spacing => 0;

    Alignment IStackLayoutNode.HorizontalAlignment => Alignment.Start;

    /// <summary>
    /// Returns whether a click can change the pressed state of <paramref name="toggle"/>.
    /// </summary>
    internal bool CanToggle(Toggle toggle)
    {
        if (!IsEnabled)
        {
            return false;
        }

        if (!toggle.IsPressed || !IsRequired)
        {
            return true;
        }

        foreach (UIElement item in Items)
        {
            if (item is Toggle { IsPressed: true } other && !ReferenceEquals(other, toggle))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Applies <see cref="SelectionMode"/> after the pressed state or value of <paramref name="toggle"/> changes.
    /// </summary>
    internal void OnToggleChanged(Toggle toggle)
    {
        if (!_isCoercing)
        {
            CoerceSelection(toggle.IsPressed ? toggle : null);
        }
    }

    /// <inheritdoc />
    protected override void OnItemsChanged(NotifyCollectionChangedEventArgs args)
    {
        CoerceSelection(null);
    }

    /// <summary>
    /// Releases every pressed toggle except one while <see cref="SelectionMode"/> is <see cref="ToggleSelectionMode.Single"/>,
    /// then updates <see cref="SelectedValue"/> and <see cref="SelectedValues"/>. The toggle that stays pressed is
    /// <paramref name="pressed"/>, or the first pressed toggle in item order when <paramref name="pressed"/> is <see langword="null"/>.
    /// </summary>
    private void CoerceSelection(Toggle? pressed)
    {
        _isCoercing = true;
        try
        {
            if (SelectionMode == ToggleSelectionMode.Single)
            {
                Toggle? kept = pressed;
                foreach (UIElement item in Items)
                {
                    if (item is not Toggle { IsPressed: true } toggle)
                    {
                        continue;
                    }

                    if (kept is null)
                    {
                        kept = toggle;
                    }
                    else if (!ReferenceEquals(toggle, kept))
                    {
                        toggle.IsPressed = false;
                    }
                }
            }
        }
        finally
        {
            _isCoercing = false;
        }

        UpdateSelectedValues();
    }

    private void UpdateSelectedValues()
    {
        List<object?> values = [];
        foreach (UIElement item in Items)
        {
            if (item is Toggle { IsPressed: true } toggle)
            {
                values.Add(toggle.Value);
            }
        }

        SelectedValue = values.Count > 0 ? values[0] : null;
        if (!values.SequenceEqual(SelectedValues))
        {
            SelectedValues = values;
        }
    }
}
