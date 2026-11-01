using NativeUI.Core;
using NativeUI.Media;

namespace NativeUI.Controls;

/// <summary>
/// Shows how far an operation has progressed, as a fill inside a track. The fill carries the class <c>progress-fill</c>, or
/// <c>progress-indeterminate</c> while <see cref="IsIndeterminate"/> is set, and has no appearance until a stylesheet gives
/// it one, including any <c>transition</c> or <c>animation</c>. Do not change <see cref="View.Children"/>.
/// </summary>
public class ProgressBar : View
{
    private const string FillClass = "progress-fill";
    private const string IndeterminateFillClass = "progress-indeterminate";
    private const double DefaultHeight = 8;
    private const double IndeterminateFillPercent = 33;

    static ProgressBar()
    {
        HeightProperty.OverrideMetadata<ProgressBar>(DefaultHeight);
        OverflowXProperty.OverrideMetadata<ProgressBar>(Overflow.Hidden);
        OverflowYProperty.OverrideMetadata<ProgressBar>(Overflow.Hidden);
        PositionProperty.OverrideMetadata<ProgressBar>(NativeUI.Layout.Position.Relative);
    }

    /// <summary>
    /// Identifies the <see cref="Value"/> bindable property.
    /// </summary>
    public static readonly BindableProperty<double> ValueProperty =
        BindableProperty.Create<ProgressBar, double>(
            nameof(Value),
            defaultValue: 0,
            propertyChanged: static (owner, _, _) => owner.UpdateFill());

    /// <summary>
    /// Identifies the <see cref="Maximum"/> bindable property.
    /// </summary>
    public static readonly BindableProperty<double> MaximumProperty =
        BindableProperty.Create<ProgressBar, double>(
            nameof(Maximum),
            defaultValue: 100,
            propertyChanged: static (owner, _, _) => owner.UpdateFill());

    /// <summary>
    /// Identifies the <see cref="IsIndeterminate"/> bindable property.
    /// </summary>
    public static readonly BindableProperty<bool> IsIndeterminateProperty =
        BindableProperty.Create<ProgressBar, bool>(
            nameof(IsIndeterminate),
            defaultValue: false,
            propertyChanged: static (owner, _, _) => owner.UpdateFill());

    private readonly View _fill = new()
    {
        Class = [FillClass],
        Style = new Style
        {
            Position = NativeUI.Layout.Position.Absolute,
            Top = 0,
            Bottom = 0,
            Left = 0,
            Width = WidthValue.Percent(0)
        }
    };

    /// <summary>
    /// Initializes a new instance of the <see cref="ProgressBar"/> class.
    /// </summary>
    public ProgressBar()
    {
        Children.Add(_fill);
    }

    /// <inheritdoc />
    public override string TagName => "progressbar";

    /// <summary>
    /// Gets or sets the current progress, which the fill limits to the range from zero to <see cref="Maximum"/>.
    /// </summary>
    public double Value
    {
        get => GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    /// <summary>
    /// Gets or sets the value at which the bar is full. Defaults to 100.
    /// </summary>
    public double Maximum
    {
        get => GetValue(MaximumProperty);
        set => SetValue(MaximumProperty, value);
    }

    /// <summary>
    /// Gets or sets a value indicating whether the operation's progress is unknown, so the bar ignores <see cref="Value"/>.
    /// </summary>
    public bool IsIndeterminate
    {
        get => GetValue(IsIndeterminateProperty);
        set => SetValue(IsIndeterminateProperty, value);
    }

    /// <summary>
    /// Gets the filled percentage of the track, in the range [0, 100].
    /// </summary>
    internal double FilledPercent => Maximum > 0 ? Math.Clamp(Value / Maximum, 0, 1) * 100 : 0;

    private void UpdateFill()
    {
        _fill.Class = IsIndeterminate ? [IndeterminateFillClass] : [FillClass];
        _fill.Width = WidthValue.Percent(IsIndeterminate ? IndeterminateFillPercent : FilledPercent);
    }
}
