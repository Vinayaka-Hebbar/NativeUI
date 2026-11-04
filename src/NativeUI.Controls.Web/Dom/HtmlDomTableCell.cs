using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;
using NativeUI.Core;

namespace NativeUI.Controls.Web;

/// <summary>
/// Synchronizes a <see cref="TableCell"/> with a custom element that has the ARIA <c>gridcell</c> role, or the
/// <c>columnheader</c> role as a header cell. A sorted header cell has <c>data-sort</c> for stylesheets and
/// <c>aria-sort</c> for assistive technologies.
/// </summary>
[SupportedOSPlatform("browser")]
public sealed class HtmlDomTableCell : HtmlDomView
{
    private readonly TableCell _cell;

    internal HtmlDomTableCell(TableCell cell, JSObject? element, string tagName)
        : base(cell, element, tagName)
    {
        _cell = cell;
    }

    /// <inheritdoc />
    protected override void Initialize()
    {
        base.Initialize();
        bool isHeader = string.Equals(_cell.PartName, "header-cell", StringComparison.Ordinal);
        SetAttribute("role", isHeader ? "columnheader" : "gridcell");
        UpdateSort();
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(BindablePropertyChangedEventArgs args)
    {
        base.OnPropertyChanged(args);
        if (ReferenceEquals(args.Property, TableCell.SortDirectionProperty))
        {
            UpdateSort();
        }
    }

    private void UpdateSort()
    {
        string? sort = _cell.GetAttribute("data-sort");
        SetAttribute("data-sort", sort);
        SetAttribute("aria-sort", sort);
    }
}
