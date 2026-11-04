using System.Globalization;
using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;
using NativeUI.Core;
using NativeUI.Layout;

namespace NativeUI.Controls.Web;

/// <summary>
/// Synchronizes a <see cref="TableRow"/> with a CSS grid element that has the ARIA <c>row</c> role. Its columns are the
/// table's column widths, and each cell is placed after the columns of the cells before it, matching desktop layout.
/// <c>aria-selected</c> carries <see cref="TableRow.IsSelected"/>.
/// </summary>
[SupportedOSPlatform("browser")]
public sealed class HtmlDomTableRow : HtmlDomView
{
    private readonly TableRow _row;

    internal HtmlDomTableRow(TableRow row, JSObject? element, string tagName)
        : base(row, element, tagName)
    {
        _row = row;
    }

    /// <inheritdoc />
    protected override string? VisibleDisplay => "grid";

    /// <inheritdoc />
    internal override void ApplyAttachedProperties(HtmlDomElement child)
    {
        if (child.Source is not TableCell)
        {
            return;
        }

        GridPlacement placement = _row.GetPlacement(child.Source);
        child.SetStyleProperty(
            "grid-column",
            string.Create(CultureInfo.InvariantCulture, $"{placement.Column + 1} / span {placement.ColumnSpan}"));
    }

    /// <inheritdoc />
    protected override void Initialize()
    {
        SetStyleProperty("align-items", "start");
        UpdateColumns();
        base.Initialize();
        SetAttribute("role", "row");
        UpdateSelected();
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(BindablePropertyChangedEventArgs args)
    {
        base.OnPropertyChanged(args);
        if (ReferenceEquals(args.Property, TableRow.ColumnWidthsProperty))
        {
            UpdateColumns();
        }
        else if (ReferenceEquals(args.Property, TableRow.IsSelectedProperty))
        {
            UpdateSelected();
        }
    }

    private void UpdateColumns()
    {
        SetStyleProperty("grid-template-columns", HtmlDomGrid.FormatTracks(_row.ColumnWidths));
    }

    private void UpdateSelected()
    {
        bool isHeader = string.Equals(_row.PartName, "column-header", StringComparison.Ordinal);
        SetAttribute("aria-selected", isHeader ? null : _row.IsSelected ? "true" : "false");
    }
}
