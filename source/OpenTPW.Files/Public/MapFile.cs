namespace OpenTPW;

/// <summary>Dead by CODE: nothing constructs it, and its <see cref="ReadFromStream"/> reads nothing.</summary>
public sealed class MapFile : BaseFormat
{
	// Guesswork / placeholders
	// Dead by CODE: nothing calls it.
	public enum TileType
	{
		Ground = 0,
		Wall = 1,
		River = 3,
		NotWalkable = 17,
		BrickPath = 144,
	}

	public MapFile( string path )
	{
		ReadFromFile( path );
	}

	public MapFile( Stream stream )
	{
		ReadFromStream( stream );
	}

	protected override void ReadFromStream( Stream stream )
	{
	}
}
