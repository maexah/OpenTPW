using System.Collections.Concurrent;
using System.Text;

namespace OpenTPW;

internal sealed class BFSTReader : BaseFormat
{
	private readonly ExpandedMemoryStream memoryStream;
	public readonly byte[] buffer;

	/// <summary>The character table a <see cref="StringFile"/> read from a stream is decoded through, its folder unknown.</summary>
	public const string EnglishCharacterTable = "Language/English/MBToUni.dat";

	/// <summary>
	/// Each folder's multibyte-to-Unicode table, opened on its first use rather than when the type loads: a static
	/// initialiser that reads through the global file system fails the whole type if nothing has mounted one yet.
	/// The two shipped tables differ (the american one has no ’), so each is kept apart (FileFormats strings.md).
	/// </summary>
	private static readonly ConcurrentDictionary<string, Lazy<BFMUReader>> CharacterTables = new();

	private readonly string _characterTable;
	private BFMUReader? _lookupTable;

	/// <summary>This table's character table, opened on the first character decoded: an empty string needs none.</summary>
	private BFMUReader LookupTable => _lookupTable ??= OpenCharacterTable( _characterTable );

	/// <summary>The character table beside a <c>.str</c> file: the one in its own language folder.</summary>
	public static string CharacterTableFor( string path )
	{
		var folder = Path.GetDirectoryName( path.Replace( '\\', '/' ) )?.Replace( '\\', '/' );
		return string.IsNullOrEmpty( folder ) ? "MBToUni.dat" : $"{folder}/MBToUni.dat";
	}

	private static BFMUReader OpenCharacterTable( string path )
	{
		return CharacterTables.GetOrAdd( path, key => new Lazy<BFMUReader>( () =>
		{
			var table = new BFMUReader( key );
			Log?.Info( $"Character table: {key}, {table.Count} characters" );
			return table;
		} ) ).Value;
	}

	/// <summary>
	/// Reads a <c>.str</c> file, decoded through the character table in its own folder. Dead by CODE: nothing calls it,
	/// <see cref="StringFile"/> reads through the stream constructor.
	/// </summary>
	public BFSTReader( string path ) : this( ReadAll( path ), CharacterTableFor( path ) )
	{
	}

	/// <summary>Reads a table from a stream, decoded through <paramref name="characterTable"/>.</summary>
	public BFSTReader( Stream stream, string characterTable ) : this( ReadAll( stream ), characterTable )
	{
	}

	/// <summary>Takes the file's bytes. Nothing is parsed until <see cref="ReadFile"/> is called.</summary>
	private BFSTReader( byte[] bytes, string characterTable )
	{
		_characterTable = characterTable;
		buffer = bytes;
		memoryStream = new ExpandedMemoryStream( buffer );
	}

	public void Dispose()
	{
		memoryStream.Dispose();
	}

	public string[] ReadFile()
	{
		memoryStream.Seek( 0, SeekOrigin.Begin );

		// The layout, the header and each record, is FileFormats strings.md.

		var magicNumber = memoryStream.ReadString( 4 );

		if ( magicNumber != "BFST" )
			throw new Exception( $"Magic number did not match: {magicNumber}" );

		// A number from 1000 to 1020, one per table (FileFormats strings.md)
		_ = memoryStream.ReadInt32();


		//String Count
		var stringCount = memoryStream.ReadInt32();

		//Save Pos and create
		var initialMemPos = memoryStream.Position;
		List<string> outputList = new List<string>();
		var longest = 0;

		for ( int i = 0; i < stringCount; i++ )
		{
			// Go to next offset based on iteration
			memoryStream.Seek( 4 * (i), SeekOrigin.Current );

			// Find offset number
			int offset = memoryStream.ReadInt32();

			// go to offset address
			// need to account offset for inital 12 bytes
			memoryStream.Seek( offset + 12, SeekOrigin.Begin );

			// The part type - every shipped string starts with a text part, 0x1
			var unknownOne = memoryStream.ReadByte();
			if ( unknownOne != 1 )
			{
				throw new Exception( $"String Offset not working - offset set to {unknownOne} @ Pos:{memoryStream.Position}" );
			}

			// The length, the part's three bytes above its type, little-endian: four shipped strings are over 255
			var stringLength = memoryStream.ReadByte() | (memoryStream.ReadByte() << 8) | (memoryStream.ReadByte() << 16);
			longest = Math.Max( longest, stringLength );

			StringBuilder str = new StringBuilder();
			// Characters
			for ( int j = 0; j < stringLength; j++ )
			{
				var mtuPos = memoryStream.ReadByte();
				var readCharacter = LookupTable.GetCharacter( mtuPos );
				str.Append( readCharacter );
			}

			outputList.Add( str.ToString() );

			// Go back to intial position
			memoryStream.Seek( initialMemPos, SeekOrigin.Begin );

		}

		Log?.Info( $"String table: {outputList.Count} strings in {buffer.Length} bytes, the longest {longest} characters" );

		return outputList.ToArray();
	}

	/// <summary>
	/// Every string as its run of parts (FileFormats <c>strings.md</c>, "The note on parts"): a part is a four-byte
	/// word on a four-byte boundary, its type in the low byte and a value in the three above. Type 1 is text of
	/// that many characters, padded to four bytes; type 2 a parameter of that number, which the caller fills; type 0
	/// ends the string. A type this does not know ends the string where it stands.
	/// </summary>
	public StringPart[][] ReadParts()
	{
		var count = BitConverter.ToInt32( buffer, 8 );
		var strings = new StringPart[count][];

		for ( var row = 0; row < count; ++row )
		{
			var at = 12 + BitConverter.ToInt32( buffer, 12 + (4 * row) );
			var parts = new List<StringPart>();

			while ( at + 4 <= buffer.Length )
			{
				var word = BitConverter.ToUInt32( buffer, at );
				var type = (int)(word & 0xff);
				var value = (int)(word >> 8);
				at += 4;

				if ( type == 1 && at + value <= buffer.Length )
				{
					var text = new StringBuilder( value );

					for ( var index = 0; index < value; ++index )
						text.Append( LookupTable.GetCharacter( buffer[at + index] ) );

					parts.Add( new StringPart( text.ToString(), 0 ) );
					at += (value + 3) & ~3;
				}
				else if ( type == 2 )
				{
					parts.Add( new StringPart( null, value ) );
				}
				else
				{
					break;
				}
			}

			strings[row] = parts.ToArray();
		}

		return strings;
	}
}

/// <summary>One part of a string: its text, or with <see cref="Text"/> null the number of the parameter that goes there.</summary>
public readonly record struct StringPart( string? Text, int Parameter );
