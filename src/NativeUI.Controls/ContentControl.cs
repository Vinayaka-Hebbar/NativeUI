using System.Globalization;
using NativeUI.Core;
using NativeUI.Xaml;

namespace NativeUI.Controls;

/// <summary>
/// Represents a control that presents one piece of <see cref="Content"/>. The template part named <c>content</c> hosts the
/// content when the template has one. Otherwise the control itself hosts the content after its template parts.
/// </summary>
[ContentProperty(nameof(Content))]
public abstract class ContentControl : Control
{
    /// <summary>
    /// The name of the template part that hosts <see cref="Content"/>.
    /// </summary>
    protected const string ContentPartName = "content";

    /// <summary>
    /// Identifies the <see cref="Content"/> bindable property.
    /// </summary>
    public static readonly BindableProperty<object?> ContentProperty =
        BindableProperty.Create<ContentControl, object?>(
            nameof(Content),
            defaultValue: null,
            propertyChanged: static (owner, _, _) => owner.UpdatePresentedContent());

    private UIElement? _presentedContent;

    /// <summary>
    /// Gets or sets the content. A <see cref="UIElement"/> is presented as it is. Any other value is presented as a
    /// <see cref="Text"/> showing its string form in the current culture. The user cannot select that text.
    /// </summary>
    public object? Content
    {
        get => GetValue(ContentProperty);
        set => SetValue(ContentProperty, value);
    }

    private void UpdatePresentedContent()
    {
        View host = GetTemplateChild<View>(ContentPartName) ?? this;
        if (_presentedContent is not null)
        {
            host.Children.Remove(_presentedContent);
        }

        _presentedContent = Content switch
        {
            null => null,
            UIElement element => element,
            object value => new Text
            {
                Value = Convert.ToString(value, CultureInfo.CurrentCulture) ?? string.Empty,
                IsTextSelectionEnabled = false
            }
        };

        if (_presentedContent is not null)
        {
            host.Children.Add(_presentedContent);
        }
    }
}
