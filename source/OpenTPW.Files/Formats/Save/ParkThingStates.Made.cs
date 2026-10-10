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
	/// A model record's two tables: a flag word a node, the lookup records' pairs, and their shared flags
	/// (<see cref="ParkModelTables"/>). The count of things attached is the pairs carrying <c>0x2</c>.
	/// </summary>
	public readonly record struct ModelTables( IReadOnlyList<uint> NodeWords,
		IReadOnlyList<(int Flags, int Handle)> Lookups, int Shared );

	/// <summary>
	/// A whole record for a model the file does not hold (FileFormats <c>saves.md</c>, "The ride system module"):
	/// its item, cell, footprint, <see cref="PlacedObjectFlags"/>, its thing's script, the hoarding's bits and
	/// progress, its angle, its two tables and its channels.
	///
	/// <para>
	/// <b>Without <paramref name="tables"/> it declares no node flag words and no lookup records.</b> The reader
	/// makes the model afresh and keeps the fresh model's own tables where the record's counts differ from them
	/// (<c>FUN_004647a0</c>), so a node its clip has hidden is shown again and none is marked as a clip's.
	/// </para>
	/// </summary>
	public static byte[] MadeRecord( int item, int cellX, int cellY, int across, int down, int scriptHandle,
		uint hoardingFlags, float hoardingProgress, int angle, IReadOnlyList<SavedChannel> channels,
		ModelTables? tables = null )
		=> Record( item, cellX, cellY, across, down, PlacedObjectFlags, scriptHandle, hoardingFlags, hoardingProgress,
			angle, tables?.NodeWords ?? [], tables?.Lookups ?? [], tables?.Shared ?? 0, channels );

	/// <summary>The flags a bumper ride's launch makes a car's model with (<c>Bumper_LaunchCar</c>, <c>0x00549fa5</c>).</summary>
	public const int CarFlags = 0x101;

	/// <summary>
	/// The record of a track car's model, or of its wake's (FileFormats <c>saves.md</c>, "A car's two model
	/// records"): the supplemental mesh's own item, on no cell and of no footprint, <see cref="CarFlags"/>, no script
	/// and no turn, its two tables and its one channel.
	/// </summary>
	public static byte[] CarRecord( int item, SavedChannel channel, ModelTables? tables )
		=> Record( item, 0, 0, 0, 0, CarFlags, 0, 0, 0f, 0, tables?.NodeWords ?? [], tables?.Lookups ?? [], tables?.Shared ?? 0,
			[channel] );

	/// <summary>The flags the retile makes a queue piece's model with (<c>FUN_005229e0</c>).</summary>
	public const int QueuePieceFlags = 0x33a;

	/// <summary>The item a queue piece's record names for the first of the eight pieces; a cell's tile index counts on from it.</summary>
	public const int FirstQueuePieceItem = 17000;

	/// <summary>How many pieces the table at <c>0x00763388</c> holds.</summary>
	public const int QueuePieces = 8;

	/// <summary>
	/// The node flag words a piece's record declares, by tile index: two for the dead end, the straight and the two
	/// bends, four for the end and the two bins, in every piece of the park files to hand (FileFormats
	/// <c>saves.md</c>, "A queue piece's record"). No file holds index 0, which names the dead end's model as index 1
	/// does.
	/// </summary>
	private static readonly int[] QueuePieceNodeWords = [2, 2, 2, 2, 2, 4, 4, 4];

	/// <summary>A queue piece's one channel in every file: nothing playing, nothing queued, every stamp nought.</summary>
	private static readonly SavedChannel QueuePieceChannel = new( NoRole, 0, 0, 0f, 0, 0, 0, NoRole, 0, 0, 0f );

	/// <summary>Whether <paramref name="item"/> is one of the eight queue pieces.</summary>
	public static bool IsQueuePiece( int item ) => item >= FirstQueuePieceItem && item < FirstQueuePieceItem + QueuePieces;

	/// <summary>
	/// The record of the model the retile makes for a queue cell (<c>FUN_005365d0</c>, <c>FUN_005229e0</c>;
	/// FileFormats <c>saves.md</c>, "A queue piece's record"): item 17000 plus the cell's tile index, on the cell,
	/// one cell square, <see cref="QueuePieceFlags"/>, no script, turned 360 less the tile's angle, its node words
	/// nought and its one channel idle. Each of the shipped park's four is these bytes.
	/// </summary>
	/// <exception cref="ArgumentOutOfRangeException">The tile index names none of the eight pieces.</exception>
	public static byte[] QueuePieceRecord( int cellX, int cellY, int tileIndex, int tileAngle )
	{
		if ( tileIndex < 0 || tileIndex >= QueuePieces )
			throw new ArgumentOutOfRangeException( nameof( tileIndex ), tileIndex, "no queue piece has this index" );

		return Record( FirstQueuePieceItem + tileIndex, cellX, cellY, 1, 1, QueuePieceFlags, 0, 0, 0f,
			(360 - tileAngle) % 360, new uint[QueuePieceNodeWords[tileIndex]], [], 0, [QueuePieceChannel] );
	}

	/// <summary>The item of the present record in <paramref name="slot"/>, or null where the slot is empty or past the table.</summary>
	public int? ItemIn( int slot )
	{
		foreach ( var thing in _things )
		{
			if ( thing.Slot == slot )
				return thing.CatalogueId;
		}

		return null;
	}

	private static byte[] Record( int item, int cellX, int cellY, int across, int down, int flags, int scriptHandle,
		uint hoardingFlags, float hoardingProgress, int angle, IReadOnlyList<uint> nodeWords,
		IReadOnlyList<(int Flags, int Handle)> lookups, int shared, IReadOnlyList<SavedChannel> channels )
	{
		var words = TailOffset + (lookups.Count * 8);
		var tail = words + (nodeWords.Count * 4);
		var record = new byte[tail + (channels.Count * ChannelDwords * 4)];

		record[0] = 1;
		PutInt32( record, IdOffset, item );
		PutInt32( record, 0x05, cellX );
		PutInt32( record, 0x09, cellY );
		PutInt32( record, 0x0d, across );
		PutInt32( record, 0x11, down );
		PutInt32( record, 0x15, flags );
		PutInt32( record, ScriptHandleOffset, scriptHandle );
		PutInt32( record, FlagsOffset, (int)(hoardingFlags & HoardingBits) );
		PutInt32( record, ProgressOffset, BitConverter.SingleToInt32Bits( hoardingProgress ) );
		PutInt32( record, 0x27, angle );
		BinaryPrimitives.WriteInt16LittleEndian( record.AsSpan( CountsOffset, 2 ), (short)nodeWords.Count );
		BinaryPrimitives.WriteInt16LittleEndian( record.AsSpan( CountsOffset + 2, 2 ), (short)lookups.Count );
		PutInt32( record, SharedOffset, shared );
		PutInt32( record, SharedOffset + 4, lookups.Count( lookup => (lookup.Flags & LookupAttached) != 0 ) );

		for ( var index = 0; index < lookups.Count; ++index )
		{
			PutInt32( record, TailOffset + (index * 8), lookups[index].Flags );
			PutInt32( record, TailOffset + (index * 8) + 4, lookups[index].Handle );
		}

		for ( var index = 0; index < nodeWords.Count; ++index )
			PutInt32( record, words + (index * 4), (int)nodeWords[index] );

		for ( var index = 0; index < channels.Count; ++index )
			PutChannel( record, tail + (index * ChannelDwords * 4), channels[index] );

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
