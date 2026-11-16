using System.Globalization;
using NativeUI.Core;
using NativeUI.Layout;
using NativeUI.Media;

namespace NativeUI.Controls;

/// <summary>
/// Presents a collapsible or overlay navigation <see cref="Pane"/> alongside primary <see cref="ContentControl.Content"/>.
/// The template exposes <c>layout</c>, <c>pane</c>, <c>content</c>, and <c>backdrop</c> parts.
/// </summary>
public class SplitView : ContentControl
{
    /// <summary>
    /// Identifies the <see cref="Pane"/> bindable property.
    /// </summary>
    public static readonly BindableProperty<object?> PaneProperty =
        BindableProperty.Create<SplitView, object?>(
            nameof(Pane),
            defaultValue: null,
            propertyChanged: static (owner, previous, _) => owner.UpdatePresentedPane(previous));

    /// <summary>
    /// Identifies the <see cref="PaneTemplate"/> bindable property.
    /// </summary>
    public static readonly BindableProperty<DataTemplate?> PaneTemplateProperty =
        BindableProperty.Create<SplitView, DataTemplate?>(
            nameof(PaneTemplate),
            propertyChanged: static (owner, _, _) => owner.UpdatePresentedPane(owner.Pane));

    /// <summary>
    /// Identifies the <see cref="IsPaneOpen"/> bindable property.
    /// </summary>
    public static readonly BindableProperty<bool> IsPaneOpenProperty =
        BindableProperty.Create<SplitView, bool>(
            nameof(IsPaneOpen),
            defaultValue: true,
            propertyChanged: static (owner, _, _) => owner.OnIsPaneOpenChanged());

    /// <summary>
    /// Identifies the <see cref="DisplayMode"/> bindable property.
    /// </summary>
    public static readonly BindableProperty<SplitViewDisplayMode> DisplayModeProperty =
        BindableProperty.Create<SplitView, SplitViewDisplayMode>(
            nameof(DisplayMode),
            defaultValue: SplitViewDisplayMode.Inline,
            propertyChanged: static (owner, _, _) => owner.UpdateLayoutState());

    /// <summary>
    /// Identifies the <see cref="PanePlacement"/> bindable property.
    /// </summary>
    public static readonly BindableProperty<Dock> PanePlacementProperty =
        BindableProperty.Create<SplitView, Dock>(
            nameof(PanePlacement),
            defaultValue: Dock.Left,
            propertyChanged: static (owner, _, _) => owner.UpdateLayoutState());

    /// <summary>
    /// Identifies the <see cref="OpenPaneLength"/> bindable property.
    /// </summary>
    public static readonly BindableProperty<double> OpenPaneLengthProperty =
        BindableProperty.Create<SplitView, double>(
            nameof(OpenPaneLength),
            defaultValue: 240.0,
            propertyChanged: static (owner, _, _) => owner.UpdateLayoutState());

    /// <summary>
    /// Identifies the <see cref="CompactPaneLength"/> bindable property.
    /// </summary>
    public static readonly BindableProperty<double> CompactPaneLengthProperty =
        BindableProperty.Create<SplitView, double>(
            nameof(CompactPaneLength),
            defaultValue: 48.0,
            propertyChanged: static (owner, _, _) => owner.UpdateLayoutState());

    private Grid _layout = null!;
    private View _pane = null!;
    private View _content = null!;
    private View _backdrop = null!;
    private UIElement? _presentedPane;

    /// <summary>
    /// Occurs when <see cref="IsPaneOpen"/> becomes <see langword="true"/>.
    /// </summary>
    public event EventHandler? PaneOpened;

    /// <summary>
    /// Occurs when <see cref="IsPaneOpen"/> becomes <see langword="false"/>.
    /// </summary>
    public event EventHandler? PaneClosed;

    static SplitView()
    {
        WidthProperty.OverrideMetadata<SplitView>(WidthValue.Percent(100));
        HeightProperty.OverrideMetadata<SplitView>(HeightValue.Percent(100));
    }

    /// <inheritdoc />
    public override string TagName => "splitview";

    /// <summary>
    /// Gets or sets the pane content.
    /// </summary>
    public object? Pane
    {
        get => GetValue(PaneProperty);
        set => SetValue(PaneProperty, value);
    }

    /// <summary>
    /// Gets or sets the template used to present non-element <see cref="Pane"/> data.
    /// </summary>
    public DataTemplate? PaneTemplate
    {
        get => GetValue(PaneTemplateProperty);
        set => SetValue(PaneTemplateProperty, value);
    }

    /// <summary>
    /// Gets or sets whether the pane is expanded to its open length. Supports two-way binding.
    /// </summary>
    public bool IsPaneOpen
    {
        get => GetValue(IsPaneOpenProperty);
        set => SetValue(IsPaneOpenProperty, value);
    }

    /// <summary>
    /// Gets or sets how the pane relates to the content area. Defaults to <see cref="SplitViewDisplayMode.Inline"/>.
    /// </summary>
    public SplitViewDisplayMode DisplayMode
    {
        get => GetValue(DisplayModeProperty);
        set => SetValue(DisplayModeProperty, value);
    }

    /// <summary>
    /// Gets or sets which horizontal edge hosts the pane (<see cref="Dock.Left"/> or <see cref="Dock.Right"/>).
    /// Defaults to <see cref="Dock.Left"/>.
    /// </summary>
    public Dock PanePlacement
    {
        get => GetValue(PanePlacementProperty);
        set => SetValue(PanePlacementProperty, value);
    }

    /// <summary>
    /// Gets or sets the width of the pane in logical pixels when <see cref="IsPaneOpen"/> is <see langword="true"/>.
    /// Defaults to <c>240</c>.
    /// </summary>
    public double OpenPaneLength
    {
        get => GetValue(OpenPaneLengthProperty);
        set => SetValue(OpenPaneLengthProperty, value);
    }

    /// <summary>
    /// Gets or sets the width of the pane in logical pixels when closed in <see cref="SplitViewDisplayMode.CompactInline"/>
    /// or <see cref="SplitViewDisplayMode.CompactOverlay"/>. Defaults to <c>48</c>.
    /// </summary>
    public double CompactPaneLength
    {
        get => GetValue(CompactPaneLengthProperty);
        set => SetValue(CompactPaneLengthProperty, value);
    }

    private bool IsOverlayMode => DisplayMode is SplitViewDisplayMode.Overlay or SplitViewDisplayMode.CompactOverlay;

    /// <inheritdoc />
    public override string? GetAttribute(string name)
    {
        return name switch
        {
            "data-open" => IsPaneOpen ? string.Empty : null,
            "data-closed" => !IsPaneOpen ? string.Empty : null,
            "data-side" => PanePlacement == Dock.Right ? "right" : "left",
            "data-display-mode" => DisplayMode switch
            {
                SplitViewDisplayMode.CompactInline => "compact-inline",
                SplitViewDisplayMode.Overlay => "overlay",
                SplitViewDisplayMode.CompactOverlay => "compact-overlay",
                _ => "inline"
            },
            _ => base.GetAttribute(name)
        };
    }

    /// <inheritdoc />
    protected override void BuildTemplate()
    {
        _layout = DefinePart("layout", new Grid
        {
            Rows = [GridLength.Star()],
            Style = new Style
            {
                Position = NativeUI.Layout.Position.Relative,
                Height = HeightValue.Percent(100),
                OverflowX = Overflow.Hidden,
                OverflowY = Overflow.Hidden
            }
        });
        _content = DefinePart(ContentPartName, new View { Height = HeightValue.Percent(100) });
        _backdrop = DefinePart("backdrop", new View
        {
            IsVisible = false,
            Style = new Style
            {
                Position = NativeUI.Layout.Position.Absolute,
                Left = 0,
                Right = 0,
                Top = 0,
                Bottom = 0,
                ZIndex = 1
            }
        });
        _pane = DefinePart("pane", new View
        {
            Style = new Style
            {
                ZIndex = 2,
                Height = HeightValue.Percent(100),
                OverflowX = Overflow.Hidden,
                OverflowY = Overflow.Hidden
            }
        });

        _layout.Children.Add(_content);
        _layout.Children.Add(_backdrop);
        _layout.Children.Add(_pane);
        Children.Add(_layout);
        UpdateLayoutState();
    }

    /// <inheritdoc />
    protected override View GetContentHost()
    {
        return _content;
    }

    /// <inheritdoc />
    protected override void OnKeyDown(KeyEventArgs args)
    {
        base.OnKeyDown(args);
        if (!args.Handled && IsPaneOpen && IsOverlayMode && args.Key == Key.Escape)
        {
            args.Handled = true;
            IsPaneOpen = false;
        }
    }

    /// <inheritdoc />
    protected override void OnPointerPressed(PointerEventArgs args)
    {
        base.OnPointerPressed(args);
        if (!args.Handled && IsPaneOpen && IsOverlayMode &&
            args.Buttons.HasFlag(PointerButtons.Primary) &&
            args.Source is UIElement source && !_pane.IsAncestorOf(source))
        {
            args.Handled = true;
            IsPaneOpen = false;
        }
    }

    private void OnIsPaneOpenChanged()
    {
        UpdateLayoutState();
        if (IsPaneOpen)
        {
            PaneOpened?.Invoke(this, EventArgs.Empty);
        }
        else
        {
            PaneClosed?.Invoke(this, EventArgs.Empty);
        }
    }

    private void UpdatePresentedPane(object? previousPane)
    {
        if (_presentedPane is not null)
        {
            _pane.Children.Remove(_presentedPane);
            if (!ReferenceEquals(_presentedPane, previousPane))
            {
                _presentedPane.Dispose();
            }
        }

        _presentedPane = Pane switch
        {
            null => null,
            UIElement element => element,
            object value when PaneTemplate is DataTemplate template => template.Build(value),
            object value => new Text
            {
                Value = Convert.ToString(value, CultureInfo.CurrentCulture) ?? string.Empty,
                IsTextSelectionEnabled = false
            }
        };

        if (_presentedPane is not null)
        {
            _pane.Children.Add(_presentedPane);
        }
    }

    private void UpdateLayoutState()
    {
        bool isPaneOpen = IsPaneOpen;
        bool isRight = PanePlacement == Dock.Right;
        double openLength = double.IsFinite(OpenPaneLength) ? Math.Max(0, OpenPaneLength) : 240;
        double compactLength = double.IsFinite(CompactPaneLength) ? Math.Max(0, CompactPaneLength) : 48;

        switch (DisplayMode)
        {
            case SplitViewDisplayMode.Inline:
                _backdrop.IsVisible = false;
                _pane.Position = NativeUI.Layout.Position.Static;
                _pane.Top = null;
                _pane.Bottom = null;
                _pane.Left = null;
                _pane.Right = null;
                if (isPaneOpen)
                {
                    _pane.IsVisible = true;
                    _pane.Width = WidthValue.Pixels(openLength);
                    _layout.Columns = isRight
                        ? [GridLength.Star(), GridLength.Auto]
                        : [GridLength.Auto, GridLength.Star()];
                    Grid.SetColumn(_pane, isRight ? 1 : 0);
                    Grid.SetColumn(_content, isRight ? 0 : 1);
                }
                else
                {
                    _pane.IsVisible = false;
                    _layout.Columns = [GridLength.Star()];
                    Grid.SetColumn(_content, 0);
                }
                break;

            case SplitViewDisplayMode.CompactInline:
            {
                double paneLength = isPaneOpen ? openLength : compactLength;
                _backdrop.IsVisible = false;
                _pane.IsVisible = true;
                _pane.Position = NativeUI.Layout.Position.Static;
                _pane.Top = null;
                _pane.Bottom = null;
                _pane.Left = null;
                _pane.Right = null;
                _pane.Width = WidthValue.Pixels(paneLength);
                _layout.Columns = isRight
                    ? [GridLength.Star(), GridLength.Auto]
                    : [GridLength.Auto, GridLength.Star()];
                Grid.SetColumn(_pane, isRight ? 1 : 0);
                Grid.SetColumn(_content, isRight ? 0 : 1);
                break;
            }

            case SplitViewDisplayMode.Overlay:
                _layout.Columns = [GridLength.Star()];
                Grid.SetColumn(_content, 0);
                _backdrop.IsVisible = isPaneOpen;
                _pane.IsVisible = true;
                _pane.Position = NativeUI.Layout.Position.Absolute;
                _pane.Top = 0;
                _pane.Bottom = 0;
                _pane.Left = isRight ? null : (isPaneOpen ? 0 : -openLength);
                _pane.Right = isRight ? (isPaneOpen ? 0 : -openLength) : null;
                _pane.Width = WidthValue.Pixels(openLength);
                break;

            case SplitViewDisplayMode.CompactOverlay:
                _layout.Columns = isRight
                    ? [GridLength.Star(), GridLength.Pixels(compactLength)]
                    : [GridLength.Pixels(compactLength), GridLength.Star()];
                Grid.SetColumn(_content, isRight ? 0 : 1);
                _backdrop.IsVisible = isPaneOpen;
                _pane.IsVisible = true;
                _pane.Position = NativeUI.Layout.Position.Absolute;
                _pane.Top = 0;
                _pane.Bottom = 0;
                _pane.Left = isRight ? null : 0;
                _pane.Right = isRight ? 0 : null;
                _pane.Width = WidthValue.Pixels(isPaneOpen ? openLength : compactLength);
                break;
        }

        InvalidateStyle();
    }
}
