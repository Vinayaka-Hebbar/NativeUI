using System.Globalization;
using NativeUI.Core;
using NativeUI.Xaml;

namespace NativeUI.Controls;

/// <summary>
/// Represents a non-interactive category heading within navigation item groups in a <see cref="NavigationView"/>.
/// </summary>
[ContentProperty(nameof(Content))]
public class NavigationViewItemHeader : NavigationViewItemBase
{
    /// <summary>
    /// Identifies the <see cref="Content"/> bindable property.
    /// </summary>
    public static readonly BindableProperty<object?> ContentProperty =
        BindableProperty.Create<NavigationViewItemHeader, object?>(
            nameof(Content),
            defaultValue: null,
            propertyChanged: static (owner, _, _) => owner.UpdatePresentedContent());

    private Text? _presentedText;

    /// <summary>
    /// Initializes a new instance of the <see cref="NavigationViewItemHeader"/> class.
    /// </summary>
    public NavigationViewItemHeader()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="NavigationViewItemHeader"/> class with the specified text content.
    /// </summary>
    /// <param name="content">The header content or label.</param>
    public NavigationViewItemHeader(object? content)
    {
        Content = content;
    }

    /// <inheritdoc />
    public override string TagName => "navigationviewitemheader";

    /// <summary>
    /// Gets or sets the header content or label text.
    /// </summary>
    public object? Content
    {
        get => GetValue(ContentProperty);
        set => SetValue(ContentProperty, value);
    }

    /// <inheritdoc />
    protected override void BuildTemplate()
    {
        _presentedText = DefinePart("text", new Text
        {
            IsTextSelectionEnabled = false
        });
        Children.Add(_presentedText);
        UpdatePresentedContent();
    }

    private void UpdatePresentedContent()
    {
        if (_presentedText is null)
        {
            return;
        }

        _presentedText.Value = Content switch
        {
            null => string.Empty,
            string str => str,
            _ => Convert.ToString(Content, CultureInfo.CurrentCulture) ?? string.Empty
        };
    }
}
