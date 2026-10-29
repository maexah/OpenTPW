namespace OpenTPW;

public sealed class StringFile : BaseFormat
{
	public string[] Entries { get; private set; } = [];

	/// <summary>
	/// Each string's parts, text and parameters in the order they stand. <see cref="Entries"/> holds a string's
	/// first text part alone.
	/// </summary>
	public StringPart[][] Parts { get; private set; } = [];

	/// <summary>The character table the strings are decoded through: the file's own folder's, English for a stream.</summary>
	private readonly string _characterTable = BFSTReader.EnglishCharacterTable;

	public StringFile( string path )
	{
		_characterTable = BFSTReader.CharacterTableFor( path );
		ReadFromFile( path );
	}

	public StringFile( Stream stream )
	{
		ReadFromStream( stream );
	}

	public string this[int val] => Entries[val];

	protected override void ReadFromStream( Stream stream )
	{
		var reader = new BFSTReader( stream, _characterTable );
		Entries = reader.ReadFile();
		Parts = reader.ReadParts();
	}
}
