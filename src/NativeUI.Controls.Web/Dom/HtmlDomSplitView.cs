using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;
using NativeUI.Core;

namespace NativeUI.Controls.Web;

/// <summary>
/// Synchronizes a <see cref="SplitView"/> with its custom element state attributes.
/// </summary>
[SupportedOSPlatform("browser")]
public sealed class HtmlDomSplitView : HtmlDomView
{
    private readonly SplitView _splitView;

    internal HtmlDomSplitView(SplitView splitView, JSObject? element, string tagName)
        : base(splitView, element, tagName)
    {
        _splitView = splitView;
    }

    /// <inheritdoc />
    protected override void Initialize()
    {
        base.Initialize();
        UpdateAttributes();
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(BindablePropertyChangedEventArgs args)
    {
        base.OnPropertyChanged(args);
        if (ReferenceEquals(args.Property, SplitView.IsPaneOpenProperty) ||
            ReferenceEquals(args.Property, SplitView.DisplayModeProperty) ||
            ReferenceEquals(args.Property, SplitView.PanePlacementProperty))
        {
            UpdateAttributes();
        }
    }

    private void UpdateAttributes()
    {
        SetAttribute("data-open", _splitView.GetAttribute("data-open"));
        SetAttribute("data-closed", _splitView.GetAttribute("data-closed"));
        SetAttribute("data-side", _splitView.GetAttribute("data-side"));
        SetAttribute("data-display-mode", _splitView.GetAttribute("data-display-mode"));
    }
}
