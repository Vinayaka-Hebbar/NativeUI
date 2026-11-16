using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;
using NativeUI.Core;

namespace NativeUI.Controls.Web;

/// <summary>
/// Synchronizes a <see cref="NavigationView"/> with its HTML navigation landmark and state attributes.
/// </summary>
[SupportedOSPlatform("browser")]
public sealed class HtmlDomNavigationView : HtmlDomControl
{
    private readonly NavigationView _navigationView;

    internal HtmlDomNavigationView(NavigationView navigationView, JSObject? element, string tagName)
        : base(navigationView, element, tagName)
    {
        _navigationView = navigationView;
    }

    /// <inheritdoc />
    protected override string Role => "navigation";

    /// <inheritdoc />
    protected override void Initialize()
    {
        base.Initialize();
        SetAttribute("aria-label", "Main");
        UpdateAttributes();
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(BindablePropertyChangedEventArgs args)
    {
        base.OnPropertyChanged(args);
        if (ReferenceEquals(args.Property, NavigationView.IsPaneOpenProperty) ||
            ReferenceEquals(args.Property, NavigationView.PaneDisplayModeProperty) ||
            ReferenceEquals(args.Property, NavigationView.PanePlacementProperty))
        {
            UpdateAttributes();
        }
    }

    private void UpdateAttributes()
    {
        SetAttribute("data-open", _navigationView.GetAttribute("data-open"));
        SetAttribute("data-closed", _navigationView.GetAttribute("data-closed"));
        SetAttribute("data-display-mode", _navigationView.GetAttribute("data-display-mode"));
        SetAttribute("data-pane-placement", _navigationView.GetAttribute("data-pane-placement"));
    }
}
