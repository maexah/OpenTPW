namespace OpenTPW;

/// <summary>
/// An item's <c>Info.Shape</c> picture as the loader keeps it: the box's width and depth, and a cell kind for each
/// character, row nought being the picture's last line. A row shorter than the box has no cell past its end, and
/// reads as kind nought there. <paramref name="Facings"/> holds each character's compass bit beside its kind, row for
/// row (the third column of the executable's character table, <c>0x007396c8</c>).
/// </summary>
public sealed record ItemShape( int Width, int Depth, int[][] Rows, int[][]? Facings = null )
{
	/// <summary>The kind at a column and a row, nought outside the picture.</summary>
	public int KindAt( int column, int row )
		=> row >= 0 && row < Rows.Length && column >= 0 && column < Rows[row].Length ? Rows[row][column] : 0;

	/// <summary>The compass bit at a column and a row, nought outside the picture or where no facing was kept.</summary>
	public int FacingAt( int column, int row )
		=> Facings != null && row >= 0 && row < Facings.Length && column >= 0 && column < Facings[row].Length
			? Facings[row][column]
			: 0;
}
