using System.Globalization;
using NativeUI.Core;
using NativeUI.Layout;
using NativeUI.Media;

namespace NativeUI.Controls;

/// <summary>
/// Provides a customizable tab header and a retained page of content.
/// The header is the tab item itself; its page is hosted separately by TabControl.
/// </summary>
public class TabItem : ContentControl
{
    /// <summary>Identifies the header content property.</summary>
    public static readonly BindableProperty<object?> HeaderProperty =
        BindableProperty.Create<TabItem, object?>(
            nameof(Header),
            propertyChanged: static (owner, previous, _) => owner.UpdateHeader(previous));

    /// <summary>Identifies the header template property.</summary>
    public static readonly BindableProperty<DataTemplate?> HeaderTemplateProperty =
        BindableProperty.Create<TabItem, DataTemplate?>(
            nameof(HeaderTemplate),
            propertyChanged: static (owner, _, _) => owner.UpdateHeader(owner.Header));

    /// <summary>Identifies whether this tab is selected.</summary>
    public static readonly BindableProperty<bool> IsSelectedProperty =
        BindableProperty.Create<TabItem, bool>(
            nameof(IsSelected),
            propertyChanged: static (owner, _, selected) => owner.OnIsSelectedChanged(selected));

    internal static readonly BindableProperty<bool> IsTabStopProperty =
        KeyboardNavigation.IsTabStopProperty.AddOwner<TabItem>();

    private View? _page;

    static TabItem()
    {
        IsTabStopProperty.OverrideMetadata<TabItem>(false);
        FocusableProperty.OverrideMetadata<TabItem>(true);
        WidthProperty.OverrideMetadata<TabItem>(WidthValue.FitContent);
    }

    /// <inheritdoc />
    public override string TagName => "tabitem";

    /// <summary>Gets or sets text, data, or an element displayed in the tab header.</summary>
    public object? Header
    {
        get => GetValue(HeaderProperty);
        set => SetValue(HeaderProperty, value);
    }

    /// <summary>Gets or sets the template that presents header data.</summary>
    public DataTemplate? HeaderTemplate
    {
        get => GetValue(HeaderTemplateProperty);
        set => SetValue(HeaderTemplateProperty, value);
    }

    /// <summary>Gets whether the containing tab control has selected this tab.</summary>
    public bool IsSelected
    {
        get => GetValue(IsSelectedProperty);
        internal set => SetValue(IsSelectedProperty, value);
    }

    internal bool IsTabStop
    {
        get => GetValue(IsTabStopProperty);
        set => SetValue(IsTabStopProperty, value);
    }

    internal TabControl? Owner { get; set; }

    internal void UpdateContentTemplate()
    {
        UpdatePresentedContent(Content);
    }

    /// <inheritdoc />
    protected override DataTemplate? GetContentTemplate()
    {
        return ContentTemplate ?? Owner?.ContentTemplate;
    }

    internal View Page => _page ??= new View();

    /// <inheritdoc />
    protected override void BuildTemplate()
    {
        Children.Add(DefinePart(new View(), "header"));
    }

    /// <inheritdoc />
    protected override View GetContentHost()
    {
        return Page;
    }

    /// <inheritdoc />
    protected override void OnDataContextChanged(DataContextChangedEventArgs args)
    {
        base.OnDataContextChanged(args);
        Page.DataContext = DataContext;
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _page?.Dispose();
        }

        base.Dispose(disposing);
    }

    private void UpdateHeader(object? previousHeader)
    {
        View? host = GetTemplateChild<View>("header");
        if (host is null)
        {
            return;
        }

        UIElement? previous = host.Children.FirstOrDefault();
        host.Children.Clear();
        if (previous is not null && !ReferenceEquals(previous, previousHeader))
        {
            previous.Dispose();
        }

        UIElement? header = Header switch
        {
            UIElement element => element,
            object data when HeaderTemplate is DataTemplate template => template.Build(data),
            object data => new Text
            {
                Value = Convert.ToString(data, CultureInfo.CurrentCulture) ?? string.Empty,
                IsTextSelectionEnabled = false
            },
            _ => null
        };

        if (header is not null)
        {
            host.Children.Add(header);
        }
    }

    private void OnIsSelectedChanged(bool selected)
    {
        SetState(ElementStates.Selected, selected);
        InvalidateStyle();
    }
}
