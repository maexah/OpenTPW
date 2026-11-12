namespace OpenTPW;

public sealed class StringFile : BaseFormat
{
	public string[] Entries { get; private set; } = [];

	/// <summary>
	/// Each string's parts, text and parameters in the order they stand. <see cref="Entries"/> holds a string's
	/// first text part alone.
	/// </summary>
	public StringPart[][] Parts { get; private set; } = [];

	/// <summary>Per row, whether <see cref="LosesWordsBare"/> holds.</summary>
	private bool[] _losesWordsBare = [];

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

	/// <summary>
	/// Whether row <paramref name="row"/> has text after a parameter, which <see cref="Entries"/>, its first text part
	/// alone, leaves out: such a row is read whole only by filling its parameters. An empty text part after the
	/// parameter loses nothing.
	/// </summary>
	public bool LosesWordsBare( int row ) => row >= 0 && row < _losesWordsBare.Length && _losesWordsBare[row];

	protected override void ReadFromStream( Stream stream )
	{
		var reader = new BFSTReader( stream, _characterTable );
		Entries = reader.ReadFile();
		Parts = reader.ReadParts();
		_losesWordsBare = Parts.Select( parts => parts.SkipWhile( part => part.Text != null ).Any( part => part.Text?.Length > 0 ) ).ToArray();
	}
}
