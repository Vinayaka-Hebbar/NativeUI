using System.Globalization;
using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;
using NativeUI.Core;
using NativeUI.Layout;

namespace NativeUI.Controls.Web;

/// <summary>
/// Synchronizes a <see cref="Grid"/> with a CSS grid element. Children keep their own height, matching desktop layout.
/// Star columns use <c>minmax(0, 1fr)</c> so that wide content wraps instead of widening its column, also matching desktop.
/// </summary>
[SupportedOSPlatform("browser")]
public sealed class HtmlDomGrid : HtmlDomView
{
    private readonly Grid _grid;

    internal HtmlDomGrid(Grid grid, JSObject? element)
        : base(grid, element)
    {
        _grid = grid;
    }

    /// <inheritdoc />
    protected override string? VisibleDisplay => "grid";

    /// <inheritdoc />
    internal override void ApplyAttachedProperties(HtmlDomElement child)
    {
        UIElement source = child.Source;
        child.SetStyleProperty("grid-row", FormatGridLine(Grid.GetRow(source), Grid.GetRowSpan(source)));
        child.SetStyleProperty("grid-column", FormatGridLine(Grid.GetColumn(source), Grid.GetColumnSpan(source)));
    }

    /// <inheritdoc />
    protected override void Initialize()
    {
        SetStyleProperty("align-items", "start");
        UpdateGrid();
        base.Initialize();
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(BindablePropertyChangedEventArgs args)
    {
        base.OnPropertyChanged(args);
        if (ReferenceEquals(args.Property, Grid.RowsProperty) ||
            ReferenceEquals(args.Property, Grid.ColumnsProperty) ||
            ReferenceEquals(args.Property, Grid.RowSpacingProperty) ||
            ReferenceEquals(args.Property, Grid.ColumnSpacingProperty))
        {
            UpdateGrid();
        }
    }

    private void UpdateGrid()
    {
        SetStyleProperty("grid-template-rows", FormatTracks(_grid.Rows));
        SetStyleProperty("grid-template-columns", FormatTracks(_grid.Columns));
        SetStyleProperty("row-gap", _grid.RowSpacing.ToString("R", CultureInfo.InvariantCulture) + "px");
        SetStyleProperty("column-gap", _grid.ColumnSpacing.ToString("R", CultureInfo.InvariantCulture) + "px");
    }

    private static string FormatGridLine(int start, int span)
    {
        return string.Create(CultureInfo.InvariantCulture, $"{start + 1} / span {span}");
    }

    private static string FormatTracks(IReadOnlyList<GridLength> tracks)
    {
        return tracks.Count == 0 ? "minmax(0, 1fr)" : string.Join(' ', tracks.Select(FormatTrack));
    }

    private static string FormatTrack(GridLength track)
    {
        string value = track.Value.ToString("R", CultureInfo.InvariantCulture);
        if (track.IsAuto)
        {
            return "auto";
        }

        return track.IsStar ? $"minmax(0, {value}fr)" : value + "px";
    }
}
