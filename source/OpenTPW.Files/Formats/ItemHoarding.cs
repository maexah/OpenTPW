namespace OpenTPW;

/// <summary>The reversed, zero-padded edge picture from Info.Hoarding; docs/exe/ride-hoardings.md.</summary>
public sealed class ItemHoarding
{
	public int Width { get; }
	public int Depth { get; }
	private readonly byte[] _edges;
	public byte At( int x, int y ) => _edges[y * Width + x];

	private ItemHoarding( int width, int depth, byte[] edges )
	{
		Width = width;
		Depth = depth;
		_edges = edges;
	}

	/// <summary>FUN_00402720, mode 2: literal spaces do not consume a cell; blank lines consume a row.</summary>
	public static ItemHoarding Read( string[] lines, int keyLine )
	{
		var at = keyLine + 1;
		if ( at >= lines.Length || !lines[at].StartsWith( "---" ) )
			throw new InvalidDataException( "Missing hoarding initial marker" );
		var rows = new List<byte[]>();
		for ( ++at; at < lines.Length && !lines[at].StartsWith( "---" ); ++at )
		{
			var row = new List<byte>();
			foreach ( var c in lines[at].TrimEnd( '\r' ) )
			{
				if ( c == ' ' ) continue;
				if ( !Alphabet.TryGetValue( c, out var edge ) )
					throw new InvalidDataException( $"Illegal hoarding character '{c}'" );
				row.Add( edge );
			}
			if ( row.Count > 20 || rows.Count >= 20 )
				throw new InvalidDataException( "Hoarding exceeds 20 by 20 cells" );
			rows.Add( row.ToArray() );
		}
		if ( at == lines.Length ) throw new InvalidDataException( "Missing hoarding closing marker" );
		var width = rows.Count == 0 ? 0 : rows.Max( r => r.Length );
		var edges = new byte[width * rows.Count];
		for ( var y = 0; y < rows.Count; ++y ) rows[rows.Count - 1 - y].CopyTo( edges, y * width );
		return new ItemHoarding( width, rows.Count, edges );
	}

	// Executable mode-2 table 0x007397b0. Bits follow the same compass as Info.Shape.
	private static readonly Dictionary<char, byte> Alphabet = new()
	{
		['.'] = 0, ['^'] = 1, ['_'] = 16, ['['] = 64, [']'] = 4,
		['J'] = 20, ['F'] = 65, ['7'] = 5, ['L'] = 80, ['='] = 17,
		['H'] = 68, ['C'] = 81, ['U'] = 84, ['n'] = 69, ['3'] = 21, ['O'] = 85
	};
}
