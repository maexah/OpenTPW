using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;

namespace OpenTPW;

/// <summary>
/// A park's placed objects, <c>scape.omp</c> in its level folder: the sounds and the particle effects that stand
/// in the land itself and belong to no thing. The level load reads it once (<c>FUN_00550e00</c>).
///
/// <para>
/// The file opens <c>OBJ_</c>, a record count and a record size, then the records, and ends in an <c>INCL</c>
/// chunk naming the headers it was built from, which the loader never reaches. A record is whole 32-bit words;
/// the loader reads the first fifteen of each into a buffer it has cleared, and steps over the rest of a longer
/// one. Every shipped record is 60 bytes.
/// </para>
/// <para>
/// Word 0 is the record's type. A type 2 is a particle effect: word 1 the effect, words 3, 4 and 5 where it is
/// started, x, the height and z in 1024ths of a park unit (<c>0x00551006</c>). Types 1 and 3 are sounds.
/// </para>
/// </summary>
public sealed class PlacedObjectsFile
{
	/// <summary>How many words of a record the loader keeps.</summary>
	public const int WordsKept = 15;

	/// <summary>The type of a record that starts a particle effect.</summary>
	public const int ParticleType = 2;

	/// <summary>The type of a record that starts a sound with a range of its own.</summary>
	public const int RangedSoundType = 1;

	private const uint Tag = 0x5f4a424f;

	/// <summary>One record: the fifteen words the loader keeps, nought where the file's record is shorter.</summary>
	public readonly record struct Record( int[] Words )
	{
		/// <summary>Word 0.</summary>
		public int Type => Words[0];

		/// <summary>A type 2's effect, word 1.</summary>
		public int Effect => Words[1];

		/// <summary>A type 2's place, words 3 to 5: x, the height and z, each 1024 to a park unit.</summary>
		public (int X, int Height, int Z) Place => (Words[3], Words[4], Words[5]);

		/// <summary>A sound's category, word 1: nought the global <c>cat_ambient</c>, anything else the level's own.</summary>
		public int Category => Words[1];

		/// <summary>A sound's effect in its category, word 2.</summary>
		public int Sound => Words[2];

		/// <summary>
		/// A type 1's place in whole park units, words 3 to 5 over 1024, each divided toward nought
		/// (<c>0x005510ba</c>).
		/// </summary>
		public (int X, int Height, int Z) SoundPlace => (Words[3] / 1024, Words[4] / 1024, Words[5] / 1024);

		/// <summary>A type 1's range in whole park units, word 6 doubled over 1024, divided toward nought.</summary>
		public int Range => Words[6] * 2 / 1024;
	}

	/// <summary>Every record, in the file's order. None where the file does not open <c>OBJ_</c>.</summary>
	public IReadOnlyList<Record> Records { get; }

	public PlacedObjectsFile( Stream stream )
	{
		using var memory = new MemoryStream();
		stream.CopyTo( memory );

		var data = memory.ToArray();
		var records = new List<Record>();

		Records = records;

		// A file that opens on anything else is left alone, as the loader leaves it.
		if ( data.Length < 12 || BinaryPrimitives.ReadUInt32LittleEndian( data ) != Tag )
			return;

		var count = BinaryPrimitives.ReadInt32LittleEndian( data.AsSpan( 4 ) );
		var size = BinaryPrimitives.ReadUInt32LittleEndian( data.AsSpan( 8 ) );
		var kept = (int)Math.Min( size, WordsKept * 4 );

		for ( long at = 12, index = 0; index < count && at + kept <= data.Length; ++index, at += size )
		{
			var bytes = new byte[WordsKept * 4];
			var words = new int[WordsKept];

			Array.Copy( data, at, bytes, 0, kept );

			for ( var word = 0; word < WordsKept; ++word )
				words[word] = BinaryPrimitives.ReadInt32LittleEndian( bytes.AsSpan( word * 4 ) );

			records.Add( new Record( words ) );
		}
	}
}
