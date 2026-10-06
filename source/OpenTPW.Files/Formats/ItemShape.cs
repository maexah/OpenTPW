namespace OpenTPW;

/// <summary>
/// An item's <c>Info.Shape</c> picture as the loader keeps it: the box's width and depth, and a cell kind for each
/// character, row nought being the picture's last line. A row shorter than the box has no cell past its end, and
/// reads as kind nought there.
/// </summary>
public sealed record ItemShape( int Width, int Depth, int[][] Rows )
{
	/// <summary>The kind at a column and a row, nought outside the picture.</summary>
	public int KindAt( int column, int row )
		=> row >= 0 && row < Rows.Length && column >= 0 && column < Rows[row].Length ? Rows[row][column] : 0;
}
