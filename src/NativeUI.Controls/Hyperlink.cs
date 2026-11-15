using NativeUI.Core;
using NativeUI.Media;

namespace NativeUI.Controls;

/// <summary>Groups inline text that can activate a navigation URI.</summary>
public class Hyperlink : Span, IUIElement
{
    /// <summary>Identifies the bubbling hyperlink activation event.</summary>
    public static readonly RoutedEvent<RoutedEventArgs> ClickEvent =
        RoutedEvent.Register<Hyperlink, RoutedEventArgs>(nameof(Click), RoutingStrategy.Bubble);

    internal static readonly RoutedEvent<RoutedEventArgs> RequestNavigateEvent =
        RoutedEvent.Register<Hyperlink, RoutedEventArgs>("RequestNavigate", RoutingStrategy.Bubble);

    /// <summary>Identifies the optional navigation URI.</summary>
    public static readonly BindableProperty<Uri?> NavigateUriProperty =
        BindableProperty.Create<Hyperlink, Uri?>(nameof(NavigateUri));

    /// <summary>Gets or sets the URI requested when the link is activated.</summary>
    public Uri? NavigateUri
    {
        get => GetValue(NavigateUriProperty);
        set => SetValue(NavigateUriProperty, value);
    }

    Cursor? IUIElement.Cursor => Style?.Cursor ?? Cursor.Pointer;


    /// <summary>Occurs on activation. Handling the event suppresses default navigation.</summary>
    public event EventHandler<RoutedEventArgs>? Click;

    internal bool PerformClick()
    {
        Text? owner = TextOwner;
        if (IsDisposed || owner is null || !owner.IsEffectivelyEnabled)
        {
            return true;
        }

        RoutedEventArgs args = new();
        args.BeginRoute(ClickEvent, this);
        Click?.Invoke(this, args);
        if (!IsDisposed && ReferenceEquals(owner, TextOwner))
        {
            owner.RaiseEvent(ClickEvent, args);
        }

        if (!args.Handled && !IsDisposed && ReferenceEquals(owner, TextOwner) && NavigateUri is not null)
        {
            RoutedEventArgs navigation = new();
            navigation.BeginRoute(RequestNavigateEvent, this);
            owner.RaiseEvent(RequestNavigateEvent, navigation);
            return navigation.Handled;
        }

        return args.Handled;
    }
}
