namespace OpenTPW.UI;

/// <summary>
/// The buy screen's footprint picture: the item's <c>Info.Shape</c>, a square a cell, in control <c>0x1eb</c> at the
/// lower left of the description panel. The buy screen's fill <c>FUN_004ab1b0</c> paints it and no other window does
/// (<c>docs/exe/park-engine.md</c>, "The buy screen's footprint picture").
/// </summary>
internal sealed class ParkFootprintPicture : UiControl
{
	/// <summary>The grid's side, <c>0x00818800</c>: sixteen columns of sixteen rows.</summary>
	internal const int GridSide = 16;

	/// <summary>How many cells fill the control each way: a cell is an eighth of it (<c>0x004ab386</c>).</summary>
	internal const int CellsAcross = 8;

	/// <summary>What a cell is painted as, the grid's code.</summary>
	internal enum Mark
	{
		None = 0,
		Body = 1,
		Entrance = 2,
		Exit = 3,
		Path = 4
	}

	/// <summary>One painted square, in the control's own pixels from its top left, the right and bottom edges not painted.</summary>
	internal readonly record struct Square( int Left, int Top, int Right, int Bottom, Mark Mark );

	/// <summary>
	/// The strength the squares are painted at over what is behind them. The fill's alpha byte is <c>0x80</c>
	/// (<c>0x004ab403</c>); the original's frame over the panel's black reads 8/15 of each colour, which is what this
	/// is. The surface's pixel format is not decoded.
	/// </summary>
	internal const float Strength = 8f / 15f;

	private static readonly Dictionary<Mark, Texture> _textures = [];

	/// <summary>The item's picture, or null to paint nothing: the land rows and a mystery ride.</summary>
	internal ItemShape? Shape { get; set; }

	/// <summary>
	/// <c>FUN_0052c5b0</c>: the picture as codes, a column then a row, row nought the picture's last line. Kinds 4 and
	/// <c>0x17</c> are the body, 1 a path, 9 the entrance and 10 the exit; every other kind is nothing.
	/// </summary>
	/// <remarks>The original's grid is sixteen each way and no shipped picture passes six by five; a cell past it is left out.</remarks>
	internal static Mark[,] Grid( ItemShape shape )
	{
		var grid = new Mark[GridSide, GridSide];

		for ( var row = 0; row < Math.Min( shape.Depth, GridSide ); ++row )
		{
			for ( var column = 0; column < Math.Min( shape.Width, GridSide ); ++column )
			{
				grid[column, row] = shape.KindAt( column, row ) switch
				{
					1 => Mark.Path,
					4 or 0x17 => Mark.Body,
					ItemDescriptionFile.EntranceKind => Mark.Entrance,
					ItemDescriptionFile.ExitKind => Mark.Exit,
					_ => Mark.None
				};
			}
		}

		return grid;
	}

	/// <summary>
	/// <c>FUN_004ab1b0</c>'s squares on a surface <paramref name="width"/> by <paramref name="height"/> pixels: a cell
	/// an eighth of each, the block growing from the lower left with row nought at the bottom, and the surface's lowest
	/// row of pixels left clear.
	/// </summary>
	internal static List<Square> Squares( ItemShape? shape, int width, int height )
	{
		var squares = new List<Square>();

		if ( shape == null )
			return squares;

		var grid = Grid( shape );
		var across = width / CellsAcross;
		var down = height / CellsAcross;

		for ( var column = 0; column < Math.Min( shape.Width, GridSide ); ++column )
		{
			for ( var row = 0; row < Math.Min( shape.Depth, GridSide ); ++row )
			{
				if ( grid[column, row] == Mark.None )
					continue;

				squares.Add( new Square( across * column, height - down - (down * row) - 1,
					across * (column + 1), height - (down * row) - 1, grid[column, row] ) );
			}
		}

		return squares;
	}

	/// <summary>The colour of a mark: blue <c>1e aa ff</c>, green <c>0f dc 32</c> the entrance, brown <c>dc 64 0f</c> the exit.</summary>
	internal static UiColour ColourOf( Mark mark ) => mark switch
	{
		Mark.Entrance => new UiColour( 0x0f, 0xdc, 0x32 ),
		Mark.Exit => new UiColour( 0xdc, 0x64, 0x0f ),
		_ => new UiColour( 0x1e, 0xaa, 0xff )
	};

	/// <summary>
	/// The surface on the window: the control's rectangle in whole pixels, as the original sizes it
	/// (<c>FUN_0048f420</c>, each side truncated).
	/// </summary>
	internal (int X, int Y, int Width, int Height) Surface
	{
		get
		{
			var pixels = Pixels;
			return ((int)pixels.X, (int)pixels.Y, (int)pixels.Width, (int)pixels.Height);
		}
	}

	protected override void OnDraw()
	{
		base.OnDraw();

		var surface = Surface;

		foreach ( var square in Squares( Shape, surface.Width, surface.Height ) )
		{
			if ( !_textures.TryGetValue( square.Mark, out var texture ) )
			{
				var colour = ColourOf( square.Mark );
				texture = new Texture( [colour.R, colour.G, colour.B, (byte)MathF.Round( Strength * 255f )], 1, 1 );
				_textures[square.Mark] = texture;
			}

			Material.UI.Set( "Color", texture );

			var width = square.Right - square.Left;
			var height = square.Bottom - square.Top;

			using ( _ = new Graphics.Scope( Screen.Size ) )
				Graphics.Quad( new Rectangle( surface.X + square.Left, Screen.Height - (surface.Y + square.Top) - height, width, height ), Material.UI );
		}
	}

	/// <summary>What is painted, for the console: the picture's rows from the top one down, and the block on the window.</summary>
	internal string Census()
	{
		if ( Shape is not { } shape )
			return "nothing";

		var grid = Grid( shape );
		var surface = Surface;
		var squares = Squares( shape, surface.Width, surface.Height );
		var rows = new List<string>();

		for ( var row = Math.Min( shape.Depth, GridSide ) - 1; row >= 0; --row )
			rows.Add( $"row {row} " + string.Join( " ", Enumerable.Range( 0, Math.Min( shape.Width, GridSide ) ).Select( column => (int)grid[column, row] ) ) );

		var block = squares.Count == 0 ? "no square"
			: $"block ({surface.X + squares.Min( square => square.Left )},{surface.Y + squares.Min( square => square.Top )})"
				+ $"-({surface.X + squares.Max( square => square.Right )},{surface.Y + squares.Max( square => square.Bottom )})";

		return $"{shape.Width} by {shape.Depth}, {string.Join( "; ", rows )}; surface ({surface.X},{surface.Y}) {surface.Width} by {surface.Height}, "
			+ $"cell {surface.Width / CellsAcross} by {surface.Height / CellsAcross}, {squares.Count} squares, {block}";
	}
}
