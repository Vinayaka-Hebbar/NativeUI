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
            propertyChanged: static (owner, previous, _) => owner.UpdatePresentedContent(previous));

    /// <summary>
    /// Identifies the template used to present data content.
    /// </summary>
    public static readonly BindableProperty<DataTemplate?> ContentTemplateProperty =
        BindableProperty.Create<ContentControl, DataTemplate?>(
            nameof(ContentTemplate),
            propertyChanged: static (owner, _, _) => owner.UpdatePresentedContent(owner.Content));

    private UIElement? _presentedContent;

    /// <summary>
    /// Gets or sets the template used to present non-element content.
    /// </summary>
    public DataTemplate? ContentTemplate
    {
        get => GetValue(ContentTemplateProperty);
        set => SetValue(ContentTemplateProperty, value);
    }

    /// <summary>
    /// Gets or sets the content. A <see cref="UIElement"/> is presented as it is. Any other value is presented as a
    /// <see cref="Text"/> showing its string form in the current culture. The user cannot select that text.
    /// </summary>
    public object? Content
    {
        get => GetValue(ContentProperty);
        set => SetValue(ContentProperty, value);
    }

    /// <summary>
    /// Returns the element that hosts <see cref="Content"/>, by default the template part named <see cref="ContentPartName"/>
    /// or the control itself when no such part exists.
    /// </summary>
    protected virtual View GetContentHost()
    {
        return GetTemplateChild<View>(ContentPartName) ?? this;
    }

    /// <summary>
    /// Returns the effective template used to present content data.
    /// </summary>
    protected virtual DataTemplate? GetContentTemplate()
    {
        return ContentTemplate;
    }

    /// <summary>
    /// Refreshes the content host after content or its effective template changes.
    /// </summary>
    /// <param name="previousContent">The content presented before this update.</param>
    protected void UpdatePresentedContent(object? previousContent)
    {
        View host = GetContentHost();
        if (_presentedContent is not null)
        {
            host.Children.Remove(_presentedContent);
            if (!ReferenceEquals(_presentedContent, previousContent))
            {
                _presentedContent.Dispose();
            }
        }

        _presentedContent = Content switch
        {
            null => null,
            UIElement element => element,
            object value when GetContentTemplate() is DataTemplate template => template.Build(value),
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
