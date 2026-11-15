using NativeUI.Core;
using NativeUI.Xaml;

namespace NativeUI.Controls;

/// <summary>Contributes a bindable string to a text paragraph.</summary>
[ContentProperty(nameof(Value))]
public class Run : Inline
{
    /// <summary>Identifies this run's text.</summary>
    public static readonly BindableProperty<string> ValueProperty =
        BindableProperty.Create<Run, string>(nameof(Value), string.Empty);

    /// <summary>Gets or sets the characters contributed by this run.</summary>
    public string Value
    {
        get => GetValue(ValueProperty) ?? string.Empty;
        set => SetValue(ValueProperty, value);
    }
}
