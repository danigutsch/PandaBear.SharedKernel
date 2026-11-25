using Terminal.Gui.Views;

namespace SharedKernel.Terminal.Gui;

/// <summary>Table of display text for a <see cref="TableView"/>, without reflection or data tables.</summary>
public sealed class RowTableSource : ITableSource
{
    private readonly string[] names;
    private readonly IReadOnlyList<IReadOnlyList<string>> rows;

    /// <summary>Builds a table source for preformatted cells.</summary>
    /// <param name="columnNames">Column headers.</param>
    /// <param name="rows">Rows of cells, each as long as <paramref name="columnNames"/>.</param>
    /// <exception cref="ArgumentNullException">Thrown when either list is <see langword="null"/>.</exception>
    public RowTableSource(IReadOnlyList<string> columnNames, IReadOnlyList<IReadOnlyList<string>> rows)
    {
        ArgumentNullException.ThrowIfNull(columnNames);
        ArgumentNullException.ThrowIfNull(rows);

        names = [.. columnNames];
        this.rows = rows;
    }

    string[] ITableSource.ColumnNames => names;

    int ITableSource.Columns => names.Length;

    int ITableSource.Rows => rows.Count;

    object ITableSource.this[int row, int col] => rows[row][col];
}
