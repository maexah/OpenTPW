using System.Text;

namespace OpenTPW;
public sealed class BFMUReader : BaseFormat
{
	private readonly ExpandedMemoryStream memoryStream;
	public readonly byte[] buffer;

	public BFMUReader( string path ) : this( ReadAll( path ) )
	{
	}

	public BFMUReader( Stream stream ) : this( ReadAll( stream ) )
	{
	}

	public void Dispose()
	{
		memoryStream.Dispose();
	}

	private BFMUReader( byte[] bytes )
	{
		buffer = bytes;
		memoryStream = new ExpandedMemoryStream( buffer );

		var magicNumber = memoryStream.ReadString( 4 );

		if ( magicNumber != "BFMU" )
			throw new Exception( $"Magic number did not match: {magicNumber}" );

		//ReadFile();
		//CharacterArray();
	}

	public List<char> CharacterArray()
	{
		List<char> allCharacters = new List<char>();

		// Skip Header
		memoryStream.Seek( 6, SeekOrigin.Begin );

		// Read Character Length
		var charCount = memoryStream.ReadByte();
		
		// The count's high byte, which this does not read - nought in both shipped tables
		_ = memoryStream.ReadByte();

		for ( int i = 0; i < charCount; i++ )
		{
			var currentChar = memoryStream.ReadBytes(2);
			foreach(var character in Encoding.Unicode.GetChars( currentChar ) )
			{
				allCharacters.Add( character );
			}
		}
		return allCharacters;
	}

	public void ReadFile()
	{
		/*
		
		Header

		4 bytes: Magic number - "BFMU"
		2 bytes: Likely specifies the character encoding - usually 0x00
		2 bytes: Character count

		# For each character

		2 bytes - The character itself in either Unicode or multibyte form

		*/

		var encoding = memoryStream.ReadBytes( 2 );
		Log.Info($"Encoding: {encoding}", true );
		var charCount = memoryStream.ReadBytes( 2 );

		for ( int i = 0; i < memoryStream.Length - memoryStream.Position; i++ )
		{
			//Log.Info( $"Character: {GetCharacter( memoryStream.Position )} @ Position: {memoryStream.Position}", true );
			_= memoryStream.ReadByte();
		}

	}

	/// <summary>The table, decoded once: it never changes, and a string asks for it once per character.</summary>
	private List<char>? _characters;

	/// <summary>The number of characters in the table.</summary>
	public int Count => (_characters ??= CharacterArray()).Count;

	public char GetCharacter( int character )
	{
		_characters ??= CharacterArray();

		// Characters are offset by 0x01 in the BFMU!
		return _characters[character - 0x01];
	}

}
