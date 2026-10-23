using Terminal.Gui.Views;

namespace PandaBear.VaultManager.Tool.Review;

/// <summary>Table of display text for a <see cref="TableView"/>, without reflection or data tables.</summary>
/// <param name="columnNames">Column headers.</param>
/// <param name="rows">Rows of cells, each as long as <paramref name="columnNames"/>.</param>
internal sealed class RowTableSource(IReadOnlyList<string> columnNames, IReadOnlyList<IReadOnlyList<string>> rows) : ITableSource
{
    private readonly string[] names = [.. columnNames];

    /// <inheritdoc/>
    public string[] ColumnNames => names;

    /// <inheritdoc/>
    public int Columns => names.Length;

    /// <inheritdoc/>
    public int Rows => rows.Count;

    /// <inheritdoc/>
    public object this[int row, int col] => rows[row][col];
}
