using System.Buffers.Binary;

namespace OpenTPW;

public sealed partial class ParkThingStates
{
	/// <summary>The flags the placer makes a bought object's model with (<c>FUN_00463060</c>'s second argument; FileFormats <c>saves.md</c>, "The ride system module").</summary>
	public const int PlacedObjectFlags = 0x32f;

	/// <summary>
	/// The slots the models made since the load take, the oldest first, once <paramref name="gone"/> are empty:
	/// each the lowest empty slot, or a new one past the last where none is (<c>FUN_00463060</c>'s cursor,
	/// <c>DAT_007aedf4</c>, which every park file to hand holds at its lowest empty slot).
	/// </summary>
	public int[] Plan( IReadOnlyCollection<int> gone, int made )
	{
		var empty = new SortedSet<int>();

		for ( var slot = 0; slot < _extents.Count; ++slot )
		{
			if ( !_extents[slot].Present || gone.Contains( slot ) )
				empty.Add( slot );
		}

		var slots = new int[made];
		var next = _extents.Count;

		for ( var i = 0; i < made; ++i )
		{
			if ( empty.Count > 0 )
			{
				slots[i] = empty.Min;
				empty.Remove( slots[i] );
			}
			else
				slots[i] = next++;
		}

		return slots;
	}

	/// <summary>
	/// A whole record for a model the file does not hold (FileFormats <c>saves.md</c>, "The ride system module"):
	/// its item, cell, footprint, <see cref="PlacedObjectFlags"/>, its thing's script, the hoarding's bits and
	/// progress, its angle and its channels.
	///
	/// <para>
	/// <b>It declares no node flag words and no lookup records.</b> The reader makes the model afresh and keeps
	/// the fresh model's own tables where the record's counts differ from them (<c>FUN_004647a0</c>), so a head
	/// hung on a node, or a node a script has hidden, is not in it.
	/// </para>
	/// </summary>
	public static byte[] MadeRecord( int item, int cellX, int cellY, int across, int down, int scriptHandle,
		uint hoardingFlags, float hoardingProgress, int angle, IReadOnlyList<SavedChannel> channels )
	{
		var record = new byte[TailOffset + (channels.Count * ChannelDwords * 4)];

		record[0] = 1;
		PutInt32( record, IdOffset, item );
		PutInt32( record, 0x05, cellX );
		PutInt32( record, 0x09, cellY );
		PutInt32( record, 0x0d, across );
		PutInt32( record, 0x11, down );
		PutInt32( record, 0x15, PlacedObjectFlags );
		PutInt32( record, ScriptHandleOffset, scriptHandle );
		PutInt32( record, FlagsOffset, (int)(hoardingFlags & HoardingBits) );
		PutInt32( record, ProgressOffset, BitConverter.SingleToInt32Bits( hoardingProgress ) );
		PutInt32( record, 0x27, angle );

		for ( var index = 0; index < channels.Count; ++index )
			PutChannel( record, TailOffset + (index * ChannelDwords * 4), channels[index] );

		return record;
	}

	/// <summary>One channel's eleven dwords, in the record's order.</summary>
	private static void PutChannel( byte[] body, int at, SavedChannel channel )
	{
		PutInt32( body, at, channel.Flags );
		PutInt32( body, at + 4, channel.Role );
		PutInt32( body, at + 8, channel.Entry );
		PutInt32( body, at + 12, (int)channel.StartTime );
		PutInt32( body, at + 16, (int)channel.Time );
		PutInt32( body, at + 20, (int)channel.NoPauseTime );
		PutInt32( body, at + 24, BitConverter.SingleToInt32Bits( channel.Speed ) );
		PutInt32( body, at + 28, channel.QueuedRole );
		PutInt32( body, at + 32, channel.QueuedEntry );
		PutInt32( body, at + 36, channel.QueuedFlags );
		PutInt32( body, at + 40, BitConverter.SingleToInt32Bits( channel.QueuedSpeed ) );
	}

	/// <summary>
	/// <paramref name="body"/> with the module built again: the slots of <paramref name="gone"/> emptied, each of
	/// <paramref name="made"/> in its slot (<see cref="Plan"/>'s), and the header's three counts kept - the
	/// present slots, the empty ones, and the cursor at the lowest empty slot or the count where none is.
	/// </summary>
	/// <exception cref="InvalidOperationException">The module was not read whole, or a slot is neither empty nor the next past the last.</exception>
	public byte[] Splice( byte[] body, IReadOnlyCollection<int> gone, IReadOnlyList<(int Slot, byte[] Record)> made )
	{
		if ( Problem != null || !ClosedOnEnd || _moduleAt < 0 )
			throw new InvalidOperationException( $"the park file's models were not read whole: {Problem}" );

		var slots = new List<byte[]?>();

		for ( var slot = 0; slot < _extents.Count; ++slot )
		{
			var (at, end, held) = _extents[slot];

			slots.Add( held && !gone.Contains( slot ) ? body.AsSpan( at, end - at ).ToArray() : null );
		}

		foreach ( var (slot, record) in made.OrderBy( entry => entry.Slot ) )
		{
			if ( slot == slots.Count )
				slots.Add( null );

			if ( slot < 0 || slot >= slots.Count || slots[slot] != null )
				throw new InvalidOperationException( $"model slot {slot} is taken, or past the table" );

			slots[slot] = record;
		}

		using var stream = new MemoryStream();
		using var writer = new BinaryWriter( stream );

		var present = slots.Count( slot => slot != null );
		var lowest = slots.FindIndex( slot => slot == null );

		writer.Write( present );
		writer.Write( slots.Count - present );
		writer.Write( lowest < 0 ? slots.Count : lowest );

		foreach ( var slot in slots )
		{
			if ( slot == null )
				writer.Write( (byte)0 );
			else
				writer.Write( slot );
		}

		writer.Flush();

		var module = stream.ToArray();
		var oldEnd = _moduleAt + 4 + BinaryPrimitives.ReadInt32LittleEndian( body.AsSpan( _moduleAt, 4 ) );
		var result = new byte[_moduleAt + 4 + module.Length + (body.Length - oldEnd)];

		body.AsSpan( 0, _moduleAt ).CopyTo( result );
		BinaryPrimitives.WriteInt32LittleEndian( result.AsSpan( _moduleAt, 4 ), module.Length );
		module.CopyTo( result, _moduleAt + 4 );
		body.AsSpan( oldEnd ).CopyTo( result.AsSpan( _moduleAt + 4 + module.Length ) );

		return result;
	}
}
