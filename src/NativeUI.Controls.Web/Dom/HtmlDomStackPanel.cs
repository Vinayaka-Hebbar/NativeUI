using System.Globalization;
using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;
using NativeUI.Core;
using NativeUI.Layout;

namespace NativeUI.Controls.Web;

/// <summary>
/// Synchronizes a <see cref="StackPanel"/> with a flexbox element. A horizontal stack does not stretch its children
/// vertically, matching desktop layout.
/// </summary>
[SupportedOSPlatform("browser")]
public sealed class HtmlDomStackPanel : HtmlDomView
{
    private readonly StackPanel _panel;

    internal HtmlDomStackPanel(StackPanel panel, JSObject? element)
        : base(panel, element)
    {
        _panel = panel;
    }

    /// <inheritdoc />
    protected override string? VisibleDisplay => "flex";

    /// <inheritdoc />
    protected override void Initialize()
    {
        UpdateStack();
        base.Initialize();
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(BindablePropertyChangedEventArgs args)
    {
        base.OnPropertyChanged(args);
        if (ReferenceEquals(args.Property, StackPanel.OrientationProperty) ||
            ReferenceEquals(args.Property, StackPanel.SpacingProperty) ||
            ReferenceEquals(args.Property, StackPanel.HorizontalAlignmentProperty))
        {
            UpdateStack();
        }
    }

    private void UpdateStack()
    {
        bool isHorizontal = _panel.Orientation == Orientation.Horizontal;
        SetStyleProperty("flex-direction", isHorizontal ? "row" : "column");
        SetStyleProperty("align-items", isHorizontal ? "flex-start" : "stretch");
        SetStyleProperty("justify-content", isHorizontal ? GetJustifyContent(_panel.HorizontalAlignment) : null);
        SetStyleProperty("gap", _panel.Spacing.ToString("R", CultureInfo.InvariantCulture) + "px");
    }

    private static string GetJustifyContent(Alignment alignment)
    {
        return alignment switch
        {
            Alignment.End => "flex-end",
            Alignment.Center => "center",
            _ => "flex-start"
        };
    }
}
