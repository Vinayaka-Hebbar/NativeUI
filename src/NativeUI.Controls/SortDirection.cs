namespace NativeUI.Controls;

/// <summary>
/// Specifies the order in which a table column sorts its rows.
/// </summary>
public enum SortDirection
{
    /// <summary>
    /// The column does not sort the rows.
    /// </summary>
    None,

    /// <summary>
    /// The rows are in ascending order of the column's values.
    /// </summary>
    Ascending,

    /// <summary>
    /// The rows are in descending order of the column's values.
    /// </summary>
    Descending
}
