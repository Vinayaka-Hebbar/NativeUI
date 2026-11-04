using System.Globalization;
using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;
using NativeUI.Core;

namespace NativeUI.Controls.Web;

/// <summary>
/// Synchronizes a <see cref="Selector"/> with a custom element that has the ARIA <c>listbox</c> role for a list and the
/// <c>grid</c> role for a table. <c>aria-multiselectable</c> follows <see cref="Selector.SelectionMode"/>, and a
/// <see cref="List"/> writes <see cref="List.ItemSpacing"/> as an inline <c>gap</c> while it is positive.
/// </summary>
[SupportedOSPlatform("browser")]
public sealed class HtmlDomSelector : HtmlDomControl
{
    private readonly Selector _selector;

    internal HtmlDomSelector(Selector selector, JSObject? element, string tagName)
        : base(selector, element, tagName)
    {
        _selector = selector;
    }

    /// <inheritdoc />
    protected override string Role => _selector is TableBase ? "grid" : "listbox";

    /// <inheritdoc />
    protected override void Initialize()
    {
        base.Initialize();
        UpdateSelectionMode();
        UpdateItemSpacing();
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(BindablePropertyChangedEventArgs args)
    {
        base.OnPropertyChanged(args);
        if (ReferenceEquals(args.Property, Selector.SelectionModeProperty))
        {
            UpdateSelectionMode();
        }
        else if (ReferenceEquals(args.Property, List.ItemSpacingProperty))
        {
            UpdateItemSpacing();
        }
    }

    private void UpdateSelectionMode()
    {
        SetAttribute("aria-multiselectable", _selector.SelectionMode == SelectionMode.Multiple ? "true" : null);
    }

    private void UpdateItemSpacing()
    {
        if (_selector is List list)
        {
            SetStyleProperty("gap", list.ItemSpacing > 0 ? list.ItemSpacing.ToString("R", CultureInfo.InvariantCulture) + "px" : null);
        }
    }
}
