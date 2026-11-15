using System.Globalization;
using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;
using NativeUI.Core;
using NativeUI.Layout;

namespace NativeUI.Controls.Web;

/// <summary>
/// Synchronizes a <see cref="StackPanel"/> with a flexbox element. A horizontal stack does not stretch its children
/// vertically, matching desktop layout. A positive <see cref="StackPanel.Spacing"/> is written as an inline <c>gap</c>. A
/// <see cref="StackPanel.Spacing"/> of zero leaves <c>gap</c> to stylesheets, matching desktop layout.
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
        if (_panel is RadioGroup)
        {
            SetAttribute("role", "radiogroup");
        }
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(BindablePropertyChangedEventArgs args)
    {
        base.OnPropertyChanged(args);
        if (ReferenceEquals(args.Property, StackPanel.OrientationProperty) ||
            ReferenceEquals(args.Property, StackPanel.SpacingProperty) ||
            ReferenceEquals(args.Property, StackPanel.HorizontalAlignmentProperty) ||
            ReferenceEquals(args.Property, RadioGroup.RequiredProperty))
        {
            UpdateStack();
        }
    }

    private void UpdateStack()
    {
        bool isHorizontal = _panel.Orientation == Orientation.Horizontal;
        if (_panel is RadioGroup group)
        {
            SetAttribute("aria-orientation", isHorizontal ? "horizontal" : "vertical");
            SetAttribute("aria-required", group.Required ? "true" : null);
        }

        SetStyleProperty("flex-direction", isHorizontal ? "row" : "column");
        SetStyleProperty("align-items", isHorizontal ? "flex-start" : "stretch");
        SetStyleProperty("justify-content", isHorizontal ? GetJustifyContent(_panel.HorizontalAlignment) : null);
        SetStyleProperty("gap", _panel.Spacing > 0 ? _panel.Spacing.ToString("R", CultureInfo.InvariantCulture) + "px" : null);
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
